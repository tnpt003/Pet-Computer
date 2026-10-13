"""Desktop Pet decisions over JSON lines; standard library only, Python 3.10+.

stdout is exclusively protocol traffic. Diagnostics go to stderr.
This module deliberately has no window, input, rendering, or physics APIs.
"""
from __future__ import annotations

import argparse
import json
import math
import os
from pathlib import Path
import random
import sys
import time
import uuid

# Embedded Python's ._pth file does not add the script folder to sys.path.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from learning import CREDIT_WINDOW, Learner, context as learning_context  # noqa: E402
from speech import Speech  # noqa: E402

VERSION = 1
MAX_MESSAGE = 8192
COMMANDS = {"LOOK", "IDLE", "WALK_LEFT", "WALK_RIGHT", "SIT", "JUMP", "SLEEP", "FOLLOW_MOUSE", "SEEK_ATTENTION",
            "ROLL_OVER", "CHASE_TAIL", "SCRATCH"}
# C# actions that own their duration; mirrors PetActionRules.RejectsDecisions.
BUSY_STATES = frozenset({"Dragging", "Thrown", "Jumping", "PreparingJump", "Sleeping", "Waking", "Grooming", "Stretching", "SeekingAttention",
                         "Kneading", "RollingOver", "ChasingTail", "Scratching", "Sneezing"})
THOUGHTS = ("hungry", "bored", "sleepy", "love")  # Priority order for thought bubbles.
NEED_KEYS = ("hunger", "boredom", "affection", "bond")
INTERACTION_KEYS = ("petted", "clicked", "treats", "toy", "ball_clicks", "ball_hits", "rough_throws")
# Effect of one interaction on (hunger, boredom, affection, bond).
INTERACTION_EFFECTS = {
    "petted": (0, -8, 6, 0.4),
    "clicked": (0, -3, 1.5, 0.1),
    "treats": (-35, 0, 5, 0.5),
    "toy": (0, -15, 0, 0),
    "ball_clicks": (0, -4, 1, 0.1),
    "ball_hits": (0, -3, 0, 0),
    "rough_throws": (0, 0, -4, -0.6),
}
BOND_DAILY_GAIN, BOND_DAILY_LOSS = 3.0, 2.0  # Bond changes slowly and recoverably.
THOUGHT_COOLDOWN = 90  # Ambient bubbles; requests are paced by the attention cooldown.
# Reaction value of one interaction when crediting what the pet just did or said.
REWARDS = {"petted": 1.0, "treats": 1.0, "ball_clicks": 0.5, "toy": 0.5, "clicked": 0.3, "rough_throws": -1.0}
# States in which the pet can say something (C# applies its own display rules too).
SILENT_STATES = frozenset({"Dragging", "Thrown", "Jumping", "PreparingJump", "Sleeping", "Waking"})
EVENT_LINE_GAP, LONELY_LINE_GAP = 6, 60
NEED_LINE_GAP = {0: 300, 1: 120, 2: 60}  # By chattiness: repeated "I am hungry" must not nag.
CHATTER = {1: (90, 0.25), 2: (35, 0.5)}  # chattiness: (min seconds since last line, chance)
ROLL_OVER_GAP = 120  # Showing the belly is special; at most every two minutes.
BOND_HISTORY_DAYS = 30


def read_object(path: Path) -> dict:
    try:
        # Generous cap: a rejected memory file would silently reset needs, bond and
        # learning, while the worst-case learned table is only ~40 KB.
        if path.stat().st_size > 524288:
            raise ValueError("file too large")
        value = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(value, dict):
            raise ValueError("expected an object")
        return value
    except FileNotFoundError:
        return {}
    except (OSError, ValueError) as exc:
        print(f"Ignoring unreadable {path.name}: {exc}", file=sys.stderr)
        return {}


def day_rhythm(hour) -> tuple[float, float, float]:
    """(active, rest, sleep) weight multipliers for the computer's local hour.

    Only biases Python's suggestions; C# still owns sleep length, the sleep lock
    and the wake cooldown. Missing or invalid hours leave weights unchanged.
    """
    if isinstance(hour, bool) or not isinstance(hour, (int, float)) or not 0 <= hour < 24:
        return 1.0, 1.0, 1.0
    if hour >= 22 or hour < 6:
        return 0.6, 1.2, 1.8  # Night: drowsier, fewer bursts of activity.
    if 7 <= hour < 10:
        return 1.2, 1.0, 0.7  # Morning: livelier, less napping.
    return 1.0, 1.0, 1.0


def number(value, default, low, high):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        return default
    return max(low, min(high, value))


class Brain:
    """Replace decide() with a future policy/LLM adapter; preserve its command contract."""

    def __init__(self, memory_path: Path, personality_path: Path, rng=None, clock=time.monotonic, wall_clock=time.time,
                 lines_path: Path | None = None):
        self.memory_path = memory_path
        self.rng = rng or random.Random()
        self.clock = clock
        self.wall_clock = wall_clock
        config = read_object(personality_path)
        self.config = config
        self.configure({})
        memory = read_object(memory_path)
        self.memory = {
            "version": 1,
            "decisions": int(number(memory.get("decisions"), 0, 0, 1e12)),
            "interactions": int(number(memory.get("interactions"), 0, 0, 1e12)),
            "last_command": memory.get("last_command") if isinstance(memory.get("last_command"), str)
                            and memory.get("last_command") in COMMANDS else "IDLE",
            "mood": memory.get("mood") if isinstance(memory.get("mood"), str)
                    and memory.get("mood") in {"content", "playful", "bored", "sleepy"} else "content",
        }
        # Counts from C# are per app session; the first snapshot establishes a baseline.
        self.last_interaction_count = None
        self.last_attention = self.clock() - 60
        self.last_save = self.clock() - 30
        self.dirty = False
        self.energy = number(memory.get("energy"), 80, 0, 100)
        self.treat_session = memory.get("treat_session", "")
        self.treat_count = number(memory.get("treat_count"), 0, 0, 1e12)
        history = memory.get("recent_actions", [])
        self.recent = [cmd for cmd in history[-4:] if isinstance(cmd, str) and cmd in COMMANDS] if isinstance(history, list) else []
        self.activity = None
        self.sleep_cooldown_until = self.clock()
        # Needs and relationship persist; missing keys start from gentle defaults.
        defaults = {"hunger": 30, "boredom": 20, "affection": 50, "bond": 30}
        self.needs = {key: number(memory.get(key), defaults[key], 0, 100) for key in NEED_KEYS}
        habits = memory.get("habits")
        self.habits = [number(v, 0, 0, 1000) for v in habits] if isinstance(habits, list) and len(habits) == 24 else [0.0] * 24
        day = memory.get("bond_day")
        self.bond_day = day if isinstance(day, str) and len(day) <= 10 else ""
        self.bond_gain_today = number(memory.get("bond_gain_today"), 0, 0, BOND_DAILY_GAIN)
        self.bond_loss_today = number(memory.get("bond_loss_today"), 0, 0, BOND_DAILY_LOSS)
        self.last_seen = number(memory.get("last_seen"), 0, 0, 1e11)
        self.greeted = False
        self.interaction_session = ""
        self.interactions = None
        self.last_need_update = None
        self.last_thought = self.clock() - THOUGHT_COOLDOWN
        self.speech = Speech(lines_path or Path(__file__).with_name("lines.json"))
        self.learner = Learner(memory.get("learned"), self.wall_clock)
        reset = memory.get("learning_reset")
        self.learning_reset = reset if isinstance(reset, str) and len(reset) <= 64 else ""
        self.sent = None  # (command, learning key, time) of the last suggested action.
        self.last_spoke = self.clock() - 60
        self.pending_event = None  # (category, time) waiting for a moment the pet can speak.
        self.just_woke = False
        self.previous_emotion = "Neutral"
        self.roll_ready = self.clock()
        # Diary: lifetime totals, first day together and bond per local day.
        self.first_seen = number(memory.get("first_seen"), 0, 0, 1e11) or self.wall_clock()
        totals = memory.get("totals") if isinstance(memory.get("totals"), dict) else {}
        self.totals = {key: number(totals.get(key), 0, 0, 1e12) for key in INTERACTION_KEYS}
        history = memory.get("bond_history") if isinstance(memory.get("bond_history"), dict) else {}
        self.bond_history = {day: number(value, 0, 0, 100) for day, value in sorted(history.items())[-BOND_HISTORY_DAYS:]
                             if isinstance(day, str) and len(day) == 10}

    def configure(self, overrides):
        if not isinstance(overrides, dict):
            overrides = {}
        self.playfulness = number(overrides.get("playfulness"), number(self.config.get("playfulness"), 0.7, 0, 1), 0, 1)
        self.curiosity = number(overrides.get("curiosity"), number(self.config.get("curiosity"), 0.6, 0, 1), 0, 1)
        self.sleepiness = number(overrides.get("sleepiness"), number(self.config.get("sleepiness"), 0.25, 0, 1), 0, 1)
        self.attention_after = number(overrides.get("attention_after_seconds"), number(self.config.get("attention_after_seconds"), 300, 10, 86400), 10, 86400)
        self.walk_cost = number(overrides.get("walking_energy_per_second"), number(self.config.get("walking_energy_per_second"), 0.5, 0, 10), 0, 10)
        self.play_cost = number(overrides.get("playing_energy_per_second"), number(self.config.get("playing_energy_per_second"), 1.5, 0, 10), 0, 10)
        self.sleep_gain = number(overrides.get("sleep_energy_per_second"), number(self.config.get("sleep_energy_per_second"), 5, 0, 20), 0, 20)
        self.rest_gain = number(overrides.get("rest_energy_per_second"), number(self.config.get("rest_energy_per_second"), 0.15, 0, 5), 0, 5)
        self.hunger_rate = number(self.config.get("hunger_per_hour"), 25, 0, 100) / 3600
        self.boredom_rate = number(self.config.get("boredom_per_minute"), 2, 0, 50) / 60
        self.affection_decay = number(self.config.get("affection_decay_per_hour"), 10, 0, 100) / 3600

    def change_bond(self, amount):
        """Apply a bond change limited per local calendar day in each direction."""
        today = time.strftime("%Y-%m-%d", time.localtime(self.wall_clock()))
        if today != self.bond_day:
            self.bond_day, self.bond_gain_today, self.bond_loss_today = today, 0.0, 0.0
        if amount > 0:
            amount = min(amount, BOND_DAILY_GAIN - self.bond_gain_today)
            self.bond_gain_today += amount
        else:
            amount = -min(-amount, BOND_DAILY_LOSS - self.bond_loss_today)
            self.bond_loss_today -= amount
        self.needs["bond"] = max(0, min(100, self.needs["bond"] + amount))

    def observe_needs(self, observation, now, profile) -> dict:
        """Advance needs over time and apply new interactions; returns interaction deltas."""
        elapsed = 0 if self.last_need_update is None else max(0, min(30, now - self.last_need_update))
        self.last_need_update = now  # Capped: pauses and suspends do not starve the pet.
        pace = 1.4 if profile == "Playful" else 0.6 if profile == "Calm" else 1
        self.needs["hunger"] += self.hunger_rate * elapsed
        self.needs["boredom"] += self.boredom_rate * pace * elapsed
        self.needs["affection"] -= self.affection_decay * elapsed
        deltas = {key: 0 for key in INTERACTION_KEYS}
        counts = observation.get("interactions")
        session = observation.get("session_id")
        if isinstance(counts, dict) and isinstance(session, str) and 0 < len(session) <= 64:
            current = {key: number(counts.get(key), 0, 0, 1e12) for key in INTERACTION_KEYS}
            if self.interactions is not None and session == self.interaction_session:
                deltas = {key: max(0, current[key] - self.interactions[key]) for key in INTERACTION_KEYS}
            self.interaction_session, self.interactions = session, current
        for key, count in deltas.items():
            if count:
                self.totals[key] += count
                hunger, boredom, affection, bond = INTERACTION_EFFECTS[key]
                self.needs["hunger"] += hunger * count
                self.needs["boredom"] += boredom * count
                self.needs["affection"] += affection * count
                self.change_bond(bond * count)
        if self.activity is not None and isinstance(observation.get("activity"), dict):
            played = number(observation["activity"].get("playing"), 0, 0, 1e12) - self.activity.get("playing", 0)
            self.needs["boredom"] -= max(0, played)  # Jumping around is fun too.
        for key in ("hunger", "boredom", "affection"):
            self.needs[key] = max(0, min(100, self.needs[key]))
        hour = observation.get("local_hour")
        user_events = sum(deltas[k] for k in INTERACTION_KEYS if k != "ball_hits")
        if user_events and not isinstance(hour, bool) and isinstance(hour, (int, float)) and 0 <= hour < 24:
            self.habits = [round(v * 0.98 ** user_events, 3) for v in self.habits]
            self.habits[int(hour)] += user_events
        return deltas

    def favorite_hour(self, hour) -> bool:
        """True when the user usually plays at this hour (enough history required)."""
        if isinstance(hour, bool) or not isinstance(hour, (int, float)) or not 0 <= hour < 24 or sum(self.habits) < 10:
            return False
        return self.habits[int(hour)] >= 0.6 * max(self.habits)

    @staticmethod
    def family(command):
        if command in {"WALK_LEFT", "WALK_RIGHT", "FOLLOW_MOUSE"}:
            return "WALK"
        if command in {"JUMP", "SEEK_ATTENTION"}:
            return "PLAY"
        return command

    def observe_activity(self, observation, now):
        totals = observation.get("activity")
        if not isinstance(totals, dict):
            return  # Older clients remain compatible; no guessed exertion.
        session = observation.get("session_id")
        if isinstance(session, str) and 0 < len(session) <= 64:
            count = number(totals.get("treats"), 0, 0, 1e12)
            previous = self.treat_count if session == self.treat_session else 0
            self.energy = min(100, self.energy + max(0, count - previous) * 6)
            self.treat_session, self.treat_count = session, count
            self.memory.update(treat_session=session, treat_count=count)
        keys = ("walking", "playing", "sleeping", "resting")
        if any(isinstance(totals.get(k), bool) or not isinstance(totals.get(k), (int, float))
               or not math.isfinite(totals[k]) or totals[k] < 0 for k in keys):
            return
        current = {k: totals[k] for k in keys}
        if self.activity is not None:
            delta = {k: max(0, current[k] - self.activity[k]) for k in keys}
            self.energy = max(0, min(100, self.energy - delta["walking"] * self.walk_cost
                - delta["playing"] * self.play_cost + delta["sleeping"] * self.sleep_gain
                + delta["resting"] * self.rest_gain))
            if delta["sleeping"] > 0:
                self.sleep_cooldown_until = now + 30
                self.just_woke = observation.get("state") != "Sleeping"
        self.activity = current

    @staticmethod
    def action_key(ctx, command):
        return f"act:{ctx}:{'WALK' if command in {'WALK_LEFT', 'WALK_RIGHT'} else command}"

    def describe_learned(self, key):
        if key.startswith("say:"):
            return self.speech.describe(key[4:])
        _, ctx, action = key.split(":", 2) if key.count(":") >= 2 else ("", "", key)
        return f"{action} @{ctx}"

    def learn_from_reactions(self, observation, deltas, now):
        """Credit the user's reaction to the action and line the pet just showed."""
        reset = observation.get("learning_reset")
        if isinstance(reset, str) and 0 < len(reset) <= 64 and reset != self.learning_reset:
            self.learner.clear()
            self.learning_reset = reset
        reward = sum(REWARDS.get(key, 0) * count for key, count in deltas.items())
        emotion = observation.get("emotion")
        if emotion == "Angry" and self.previous_emotion != "Angry" and not deltas["rough_throws"]:
            reward -= 0.5  # Rapid clicking upset the pet.
        self.previous_emotion = emotion if isinstance(emotion, str) else "Neutral"
        reward = max(-1.0, min(1.0, reward))
        if reward == 0:
            return
        if self.sent and observation.get("last_applied_command") == self.sent[0] and now - self.sent[2] <= CREDIT_WINDOW:
            self.learner.reward(self.sent[1], reward)
        spoken = observation.get("last_speech")
        if isinstance(spoken, dict) and isinstance(spoken.get("id"), str) and len(spoken["id"]) <= 80:
            age = number(spoken.get("age"), -1, -1, 1e9)
            if 0 <= age <= CREDIT_WINDOW:
                self.learner.reward("say:" + spoken["id"], reward)

    def choose_speech(self, observation, deltas, thought, greeting, now):
        """Pick at most one line: a due break reminder, events, needs, loneliness, then chatter."""
        context = observation.get("context") if isinstance(observation.get("context"), dict) else {}
        enabled = observation.get("speech_enabled") is True
        # Break reminders are a separate opt-in, so they speak even with chatter off.
        break_due = observation.get("break_due") is True
        if context.get("fullscreen") is True or not (enabled or break_due):
            self.pending_event = None  # Never talk over a fullscreen game, video or slides.
            return None
        if enabled:
            event = ("thrown_rough" if deltas["rough_throws"] else "treat_thanks" if deltas["treats"] else
                     "petted" if deltas["petted"] else "play" if deltas["ball_clicks"] or deltas["toy"] else
                     "thrown_fun" if observation.get("last_event") == "released" and self.interaction_delta else
                     "back" if context.get("returned") is True else
                     "wake_up" if self.just_woke else greeting)
            self.just_woke = False
            if event:
                self.pending_event = (event, now)
        if observation["state"] in SILENT_STATES or not observation["grounded"] or observation.get("emotion") == "Sleepy":
            return None  # Keep a fresh event for the next moment the pet can talk.
        category = None
        if break_due and now - self.last_spoke >= EVENT_LINE_GAP:
            category = "break"
        elif not enabled:
            return None
        elif self.pending_event and now - self.pending_event[1] <= 12 and now - self.last_spoke >= EVENT_LINE_GAP:
            category = self.pending_event[0]
        elif thought in ("hungry", "bored", "sleepy") and now - self.last_spoke >= NEED_LINE_GAP.get(observation.get("chattiness"), 120):
            category = thought
        elif observation.get("emotion") == "Sad" and now - self.last_spoke >= LONELY_LINE_GAP:
            category = "lonely"
        else:
            chattiness = observation.get("chattiness")
            gap, chance = CHATTER.get(chattiness if type(chattiness) is int else 1, (None, 0))
            if gap is not None and observation.get("quiet") is not True and now - self.last_spoke >= gap and self.rng.random() < chance:
                part = learning_context(observation.get("local_hour"), self.needs, self.energy).split("/")[0]
                category = part if part in ("morning", "night") and self.rng.random() < 0.5 else "idle"
        if self.pending_event and (category == self.pending_event[0] or now - self.pending_event[1] > 12):
            self.pending_event = None  # Spoken, or too old to still make sense.
        if category is None:
            return None
        line = self.speech.pick(category, observation.get("language"), observation.get("personality", "Balanced"),
                                observation.get("pet_name"), lambda line_id: self.learner.weight("say:" + line_id), self.rng)
        if line is not None:
            self.last_spoke = now
        return line

    def decide(self, observation: dict) -> dict:
        self.configure(observation.get("tuning"))
        profile = observation.get("personality", "Balanced")
        idle = number(observation.get("seconds_since_interaction"), 0, 0, 86400 * 365)
        count = observation.get("interaction_count")
        if type(count) is not int or count < 0:
            raise ValueError("interaction_count must be a nonnegative integer")
        if type(observation.get("grounded")) is not bool or not isinstance(observation.get("state"), str):
            raise ValueError("state and grounded are required")
        self.interaction_delta = 0
        if self.last_interaction_count is not None:
            self.interaction_delta = max(0, count - self.last_interaction_count)
            self.memory["interactions"] += self.interaction_delta
        self.last_interaction_count = count
        now = self.clock()
        deltas = self.observe_needs(observation, now, profile)  # Before activity updates its baseline.
        self.observe_activity(observation, now)
        self.learn_from_reactions(observation, deltas, now)
        ctx = learning_context(observation.get("local_hour"), self.needs, self.energy)
        greeting = None
        hunger, boredom = self.needs["hunger"], self.needs["boredom"]
        busy = not observation["grounded"] or observation["state"] in BUSY_STATES or observation.get("emotion") == "Sleepy"
        upset = observation.get("emotion") in ("Angry", "Surprised")
        attention_ready = now - self.last_attention >= (100 if profile == "Calm" else 60)
        thought = None
        wall = self.wall_clock()
        if not self.greeted and not busy and not upset:
            self.greeted = True
            # Welcome back after a long break, only once a real bond exists.
            if self.last_seen and wall - self.last_seen >= 3600 and self.needs["bond"] >= 50:
                thought = "love"
            greeting = "welcome_back" if thought == "love" else "hello"
        self.last_seen = wall
        if busy:
            command, mood = "IDLE", "content"
        elif upset:
            command, mood = "SIT", "content"  # Give an upset/startled pet a calm action.
        elif thought == "love":
            command, mood = "FOLLOW_MOUSE", "playful"
        elif deltas["petted"] and self.needs["bond"] >= 60 and self.needs["affection"] >= 60 and now >= self.roll_ready:
            command, mood = "ROLL_OVER", "playful"  # Trusting enough to show the belly.
            self.roll_ready = now + ROLL_OVER_GAP
        elif attention_ready and hunger >= 70 and idle >= 45:
            command, mood, thought = "SEEK_ATTENTION", "bored", "hungry"  # Asks for a treat.
            self.last_attention = now
        elif attention_ready and boredom >= 75 and idle >= 60 and self.energy >= 30:
            command, mood, thought = "SEEK_ATTENTION", "bored", "bored"
            self.last_attention = now
        elif idle >= self.attention_after * (1.8 if profile == "Calm" else 0.8 if profile == "Playful" else 1) and attention_ready and self.energy >= 30:
            command, mood = "SEEK_ATTENTION", "bored"
            self.last_attention = now
        else:
            commands = ["IDLE", "WALK_LEFT", "WALK_RIGHT", "SIT", "JUMP", "SLEEP", "FOLLOW_MOUSE", "LOOK", "CHASE_TAIL", "SCRATCH"]
            vigor = 0.25 + self.energy / 100
            rest = 1 + (100 - self.energy) / 35
            profile = observation.get("personality", "Balanced")
            play = self.playfulness * (1.6 if profile == "Playful" else 0.35 if profile == "Calm" else 1)
            curiosity = self.curiosity * (1.7 if profile == "Curious" else 0.4 if profile == "Calm" else 1)
            if profile == "Calm": rest *= 1.6
            weights = [rest, 2 * vigor, 2 * vigor, rest, play * 3 * vigor,
                       self.sleepiness * 2 * rest, curiosity * 2 * vigor, 0.3 + curiosity * 2,
                       play * 0.8 * vigor, 0.4]
            active, calm, sleepy = day_rhythm(observation.get("local_hour"))
            scale = {"WALK_LEFT": active, "WALK_RIGHT": active, "JUMP": active, "FOLLOW_MOUSE": active, "CHASE_TAIL": active,
                     "IDLE": calm, "SIT": calm, "SLEEP": sleepy}
            weights = [weight * scale.get(cmd, 1.0) for cmd, weight in zip(commands, weights)]
            # Needs reshape intent: boredom seeks play, warmth seeks the user.
            b = boredom / 100
            warmth = max(self.needs["affection"], self.needs["bond"]) / 100
            habit = 1.15 if self.favorite_hour(observation.get("local_hour")) else 1.0
            needs_scale = {"JUMP": (1 + 1.2 * b) * habit, "FOLLOW_MOUSE": (0.6 + 0.8 * warmth + b) * habit,
                           "CHASE_TAIL": 1 + 1.5 * b,
                           "IDLE": 1 - 0.4 * b, "SIT": 1 - 0.4 * b,
                           "WALK_LEFT": habit, "WALK_RIGHT": habit,
                           "LOOK": 1.2 if self.needs["bond"] < 20 else 1.0}
            weights = [weight * needs_scale.get(cmd, 1.0) for cmd, weight in zip(commands, weights)]
            # What the user rewarded in this situation before (x0.5..x2).
            weights = [weight * self.learner.weight(self.action_key(ctx, cmd)) for cmd, weight in zip(commands, weights)]
            # Penalize recent action families, not just alternating walk direction.
            families = [self.family(cmd) for cmd in self.recent]
            weights = [weight * 0.35 ** families.count(self.family(cmd))
                       for cmd, weight in zip(commands, weights)]
            if self.recent:
                weights = [0 if self.family(cmd) == self.family(self.recent[-1]) else weight
                           for cmd, weight in zip(commands, weights)]
            if now < self.sleep_cooldown_until:
                weights[commands.index("SLEEP")] = 0
            command = self.rng.choices(commands, weights=weights, k=1)[0]
            mood = "sleepy" if command == "SLEEP" else "playful" if command in {"JUMP", "FOLLOW_MOUSE", "CHASE_TAIL"} else "content"
        if thought is None and not busy and not upset:
            # A bubble shows the strongest current need; fresh affection earns a heart.
            fresh_love = deltas["petted"] + deltas["treats"] > 0 and self.needs["affection"] >= 60
            wanted = {"hungry": hunger >= 70, "bored": boredom >= 65,
                      "sleepy": self.energy <= 25 and command != "SLEEP", "love": fresh_love}
            thought = next((name for name in THOUGHTS if wanted[name]), None)
        ambient = thought is not None and thought != "love" and command != "SEEK_ATTENTION"
        if ambient and now - self.last_thought < THOUGHT_COOLDOWN:
            thought = None
        if thought is not None:
            self.last_thought = now
        if not busy and not upset:
            self.sent = (command, self.action_key(ctx, command), now)
        speech = self.choose_speech(observation, deltas, thought, greeting, now)
        duration =self.rng.uniform(7, 12) if command == "SLEEP" else self.rng.uniform(2, 6)
        if thought == "love" and command == "FOLLOW_MOUSE": duration = 4.0
        # Personality changes the pace and intent of an activity, not physics.
        if profile == "Curious" and command in {"IDLE", "LOOK", "FOLLOW_MOUSE"}: duration = min(8, duration * 1.4)
        if profile == "Calm" and command in {"IDLE", "SIT"}: duration = min(9, duration * 1.5)
        if profile == "Playful" and command in {"WALK_LEFT", "WALK_RIGHT"}: duration = max(1.5, duration * 0.8)
        self.memory.update(last_command=command, mood=mood, decisions=self.memory["decisions"] + 1)
        if observation["grounded"] and observation["state"] not in BUSY_STATES and observation.get("emotion") not in {"Sleepy", "Angry", "Surprised"}:
            self.recent = (self.recent + [command])[-4:]
        self.bond_history[time.strftime("%Y-%m-%d", time.localtime(wall))] = round(self.needs["bond"], 1)
        self.bond_history = dict(sorted(self.bond_history.items())[-BOND_HISTORY_DAYS:])
        self.memory.update(first_seen=round(self.first_seen), totals={k: int(v) for k, v in self.totals.items()},
                           bond_history=self.bond_history)
        self.memory.update(energy=round(self.energy, 3), recent_actions=self.recent,
                           habits=self.habits, bond_day=self.bond_day, bond_gain_today=round(self.bond_gain_today, 3),
                           bond_loss_today=round(self.bond_loss_today, 3), last_seen=round(self.last_seen),
                           learned=self.learner.export(), learning_reset=self.learning_reset,
                           **{key: round(self.needs[key], 3) for key in NEED_KEYS})
        self.dirty = True
        if now - self.last_save >= 30:
            self.save()
            self.last_save = now
        # Emotion hints are grounded in attention history, not the random action.
        emotion = "sad" if idle >= 300 else "sleepy" if idle >= 120 else "neutral"
        decision = {"command": command, "duration_seconds": round(duration, 2), "mood": mood, "emotion": emotion,
                    "energy": round(self.energy, 1), "needs": {key: round(self.needs[key], 1) for key in NEED_KEYS}}
        if thought is not None:
            decision["thought"] = thought
        if speech is not None:
            decision["speech"] = speech
        decision["learned"] = self.learner.summary(self.describe_learned)
        decision["diary"] = self.diary(wall)
        return decision

    def diary(self, wall) -> dict:
        """Small summary for the C# diary window; C# only displays it."""
        t = self.totals
        return {"days": int(max(0, wall - self.first_seen) // 86400) + 1,
                "since": time.strftime("%Y-%m-%d", time.localtime(self.first_seen)),
                "petted": int(t["petted"]), "treats": int(t["treats"]), "play": int(t["toy"] + t["ball_clicks"]),
                "rough_throws": int(t["rough_throws"]), "clicks": int(t["clicked"]), "bond": round(self.needs["bond"], 1),
                "bond_history": [value for _, value in sorted(self.bond_history.items())[-14:]],
                "habits": [round(value, 1) for value in self.habits]}

    def save(self):
        if not self.dirty:
            return
        temporary = self.memory_path.with_name(self.memory_path.name + f".{uuid.uuid4().hex}.tmp")
        try:
            self.memory_path.parent.mkdir(parents=True, exist_ok=True)
            temporary.write_text(json.dumps(self.memory, indent=2), encoding="utf-8")
            os.replace(temporary, self.memory_path)
            self.dirty = False
        except OSError as exc:
            print(f"Memory save skipped: {exc}", file=sys.stderr)
        finally:
            try:
                temporary.unlink(missing_ok=True)
            except OSError:
                pass


def handle(request: dict, brain: Brain) -> dict:
    if not isinstance(request, dict):
        raise ValueError("expected request object")
    if request.get("version") != VERSION or request.get("type") != "observe":
        raise ValueError("unsupported version or request type")
    if type(request.get("id")) is not int or request["id"] < 1:
        raise ValueError("id must be a positive integer")
    observation = request.get("observation")
    if not isinstance(observation, dict):
        raise ValueError("observation must be an object")
    return {"version": VERSION, "id": request["id"], "type": "decision", **brain.decide(observation)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    default_memory = Path(os.environ.get("LOCALAPPDATA", str(Path.home()))) / "DesktopPet" / "ai-memory.json"
    parser.add_argument("--memory", type=Path, default=default_memory)
    parser.add_argument("--personality", type=Path, default=Path(__file__).with_name("personality.json"))
    args = parser.parse_args()
    brain = Brain(args.memory, args.personality)
    try:
        while True:
            line = sys.stdin.readline(MAX_MESSAGE + 1)
            if not line:
                break  # C# exited/closed stdin: no orphan service stays running.
            if len(line) > MAX_MESSAGE:
                print("Request exceeded message limit; closing service.", file=sys.stderr)
                break
            request = None
            try:
                request = json.loads(line)
                response = handle(request, brain)
            except (ValueError, TypeError, KeyError) as exc:
                print(f"Invalid request: {exc}", file=sys.stderr)
                response = {"version": VERSION, "id": request.get("id") if isinstance(request, dict) else None,
                            "type": "error", "error": str(exc)}
            print(json.dumps(response, allow_nan=False), flush=True)
    except (BrokenPipeError, KeyboardInterrupt):
        pass
    finally:
        brain.save()


if __name__ == "__main__":
    main()
