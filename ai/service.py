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

VERSION = 1
MAX_MESSAGE = 8192
COMMANDS = {"LOOK", "IDLE", "WALK_LEFT", "WALK_RIGHT", "SIT", "JUMP", "SLEEP", "FOLLOW_MOUSE", "SEEK_ATTENTION"}


def read_object(path: Path) -> dict:
    try:
        if path.stat().st_size > 65536:
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


def number(value, default, low, high):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        return default
    return max(low, min(high, value))


class Brain:
    """Replace decide() with a future policy/LLM adapter; preserve its command contract."""

    def __init__(self, memory_path: Path, personality_path: Path, rng=None, clock=time.monotonic):
        self.memory_path = memory_path
        self.rng = rng or random.Random()
        self.clock = clock
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
        self.activity = current

    def decide(self, observation: dict) -> dict:
        self.configure(observation.get("tuning"))
        profile = observation.get("personality", "Balanced")
        idle = number(observation.get("seconds_since_interaction"), 0, 0, 86400 * 365)
        count = observation.get("interaction_count")
        if type(count) is not int or count < 0:
            raise ValueError("interaction_count must be a nonnegative integer")
        if type(observation.get("grounded")) is not bool or not isinstance(observation.get("state"), str):
            raise ValueError("state and grounded are required")
        if self.last_interaction_count is not None:
            self.memory["interactions"] += max(0, count - self.last_interaction_count)
        self.last_interaction_count = count
        now = self.clock()
        self.observe_activity(observation, now)
        if not observation["grounded"] or observation["state"] in {"Dragging", "Thrown", "Jumping", "PreparingJump", "Sleeping", "Waking", "Grooming", "Stretching", "SeekingAttention"} or observation.get("emotion") == "Sleepy":
            command, mood = "IDLE", "content"
        elif observation.get("emotion") in ("Angry", "Surprised"):
            command, mood = "SIT", "content"  # Give an upset/startled pet a calm action.
        elif idle >= self.attention_after * (1.8 if profile == "Calm" else 0.8 if profile == "Playful" else 1) and now - self.last_attention >= (100 if profile == "Calm" else 60) and self.energy >= 30:
            command, mood = "SEEK_ATTENTION", "bored"
            self.last_attention = now
        else:
            commands = ["IDLE", "WALK_LEFT", "WALK_RIGHT", "SIT", "JUMP", "SLEEP", "FOLLOW_MOUSE", "LOOK"]
            vigor = 0.25 + self.energy / 100
            rest = 1 + (100 - self.energy) / 35
            profile = observation.get("personality", "Balanced")
            play = self.playfulness * (1.6 if profile == "Playful" else 0.35 if profile == "Calm" else 1)
            curiosity = self.curiosity * (1.7 if profile == "Curious" else 0.4 if profile == "Calm" else 1)
            if profile == "Calm": rest *= 1.6
            weights = [rest, 2 * vigor, 2 * vigor, rest, play * 3 * vigor,
                       self.sleepiness * 2 * rest, curiosity * 2 * vigor, 0.3 + curiosity * 2]
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
            mood = "sleepy" if command == "SLEEP" else "playful" if command in {"JUMP", "FOLLOW_MOUSE"} else "content"
        duration = self.rng.uniform(7, 12) if command == "SLEEP" else self.rng.uniform(2, 6)
        # Personality changes the pace and intent of an activity, not physics.
        if profile == "Curious" and command in {"IDLE", "LOOK", "FOLLOW_MOUSE"}: duration = min(8, duration * 1.4)
        if profile == "Calm" and command in {"IDLE", "SIT"}: duration = min(9, duration * 1.5)
        if profile == "Playful" and command in {"WALK_LEFT", "WALK_RIGHT"}: duration = max(1.5, duration * 0.8)
        self.memory.update(last_command=command, mood=mood, decisions=self.memory["decisions"] + 1)
        if observation["grounded"] and observation["state"] not in {"Dragging", "Thrown", "Jumping", "PreparingJump", "Sleeping", "Waking", "Grooming", "Stretching", "SeekingAttention"} and observation.get("emotion") not in {"Sleepy", "Angry", "Surprised"}:
            self.recent = (self.recent + [command])[-4:]
        self.memory.update(energy=round(self.energy, 3), recent_actions=self.recent)
        self.dirty = True
        if now - self.last_save >= 30:
            self.save()
            self.last_save = now
        # Emotion hints are grounded in attention history, not the random action.
        emotion = "sad" if idle >= 300 else "sleepy" if idle >= 120 else "neutral"
        return {"command": command, "duration_seconds": round(duration, 2), "mood": mood, "emotion": emotion,
                "energy": round(self.energy, 1)}

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
