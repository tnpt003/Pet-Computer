import datetime as dt
import io
import json
from pathlib import Path
import random
import subprocess
import sys
import tempfile
import time
import unittest
from contextlib import redirect_stderr

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "ai"))
from service import Brain, COMMANDS, day_rhythm, handle
from learning import Learner, context as learning_context
from seasons import Calendar
from speech import Speech


class BrainTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.memory = Path(self.temporary.name) / "memory.json"
        self.now = 100
        self.brain = Brain(self.memory, ROOT / "ai/personality.json", random.Random(4), lambda: self.now)

    def observation(self, idle=10, count=0):
        return {"state": "Idle", "grounded": True, "seconds_since_interaction": idle,
                "interaction_count": count, "last_event": "started", "last_applied_command": "LOCAL"}

    def test_attention_and_cooldown(self):
        self.assertEqual(self.brain.decide(self.observation(301))["command"], "SEEK_ATTENTION")
        self.assertNotEqual(self.brain.decide(self.observation(302))["command"], "SEEK_ATTENTION")
        self.now += 61
        self.assertEqual(self.brain.decide(self.observation(362))["command"], "SEEK_ATTENTION")

    def test_emotion_hints_are_based_on_attention(self):
        self.assertEqual(self.brain.decide(self.observation(10))["emotion"], "neutral")
        self.assertEqual(self.brain.decide(self.observation(125))["emotion"], "sleepy")
        self.assertEqual(self.brain.decide(self.observation(305))["emotion"], "sad")

    def test_reaction_context_chooses_a_calm_action(self):
        for emotion in ("Angry", "Surprised"):
            observation = {**self.observation(), "emotion": emotion}
            self.assertEqual(self.brain.decide(observation)["command"], "SIT")

    def test_commands_are_bounded_and_diverse(self):
        commands = set()
        for _ in range(300):
            decision = self.brain.decide(self.observation())
            self.assertIn(decision["command"], COMMANDS)
            self.assertTrue(0.5 <= decision["duration_seconds"] <= 12)
            commands.add(decision["command"])
        self.assertEqual(commands, COMMANDS - {"SEEK_ATTENTION", "ROLL_OVER"})  # Those need idle time / petting.

    def test_roll_over_needs_petting_trust_and_a_gap(self):
        def petted(count, bond, affection=80):
            self.brain.needs.update(bond=bond, affection=affection, hunger=0, boredom=0)
            return self.brain.decide({**self.observation(count=count), "session_id": "s",
                                      "interactions": {"petted": count}})["command"]
        petted(0, 70)  # Baseline for the session counts.
        self.assertNotEqual(petted(1, 40), "ROLL_OVER")  # Not trusting enough yet.
        self.assertEqual(petted(2, 70), "ROLL_OVER")
        self.assertNotEqual(petted(3, 70), "ROLL_OVER")  # At most every two minutes.
        self.now += 121
        self.assertEqual(petted(4, 70), "ROLL_OVER")
        self.assertNotEqual(petted(4, 70), "ROLL_OVER")  # No new petting, no roll.

    def test_boredom_brings_tail_chasing(self):
        def chases(boredom):
            brain = Brain(Path(self.temporary.name) / f"c{boredom}.json", ROOT / "ai/personality.json", random.Random(8), lambda: self.now)
            total = 0
            for _ in range(400):
                brain.needs.update(boredom=boredom, hunger=0)
                total += brain.decide(self.observation())["command"] == "CHASE_TAIL"
            return total
        self.assertGreater(chases(100), chases(0) * 1.4)

    def test_diary_counts_persist_and_old_memory_loads(self):
        wall = [1_700_000_000.0]
        self.memory.write_text(json.dumps({"bond": 42, "decisions": 3}), encoding="utf-8")  # Older file.
        brain = Brain(self.memory, ROOT / "ai/personality.json", random.Random(1), lambda: self.now, lambda: wall[0])
        def observe(**counts):
            return brain.decide({**self.observation(), "session_id": "s", "interactions": counts})
        first = observe()["diary"]
        self.assertEqual((first["days"], first["petted"], first["bond_history"]), (1, 0, [42.0]))
        observe(petted=3, treats=1, toy=1, ball_clicks=2, rough_throws=1)
        wall[0] += 86400 * 2
        brain.save()
        again = Brain(self.memory, ROOT / "ai/personality.json", random.Random(1), lambda: self.now, lambda: wall[0])
        diary = again.decide({**self.observation(), "session_id": "t", "interactions": {"petted": 5}})["diary"]
        self.assertEqual((diary["days"], diary["petted"], diary["treats"], diary["play"], diary["rough_throws"]), (3, 3, 1, 3, 1))
        self.assertEqual(len(diary["bond_history"]), 2)  # One value per local day.
        self.assertEqual(len(diary["habits"]), 24)
        self.assertLess(len(json.dumps(diary)), 1500)
        for bad in ({"totals": "x", "bond_history": [1], "first_seen": -5}, {"totals": {"petted": "many"}, "bond_history": {"bad": 1}}):
            self.memory.write_text(json.dumps(bad), encoding="utf-8")
            broken = Brain(self.memory, ROOT / "ai/personality.json", random.Random(1), lambda: self.now, lambda: wall[0])
            self.assertEqual(broken.decide(self.observation())["diary"]["petted"], 0)

    def test_local_hour_biases_sleep_without_breaking_variety(self):
        def sleep_share(hour):
            brain = Brain(Path(self.temporary.name) / f"m{hour}.json", ROOT / "ai/personality.json", random.Random(9), lambda: self.now)
            picks = [brain.decide({**self.observation(), "local_hour": hour})["command"] for _ in range(400)]
            return picks.count("SLEEP") / len(picks), set(picks)
        night, night_commands = sleep_share(23)
        morning, _ = sleep_share(8)
        self.assertGreater(night, morning)
        self.assertIn("WALK_LEFT", night_commands)  # Night only biases, never forbids activity.
        self.assertEqual(day_rhythm(None), (1.0, 1.0, 1.0))
        self.assertEqual(day_rhythm(True), (1.0, 1.0, 1.0))
        self.assertEqual(day_rhythm(25), (1.0, 1.0, 1.0))

    def interacting(self, idle=10, **counts):
        base = {key: 0 for key in ("petted", "clicked", "treats", "toy", "ball_clicks", "ball_hits", "rough_throws")}
        return {**self.observation(idle), "session_id": "s1", "interactions": {**base, **counts}}

    def test_needs_grow_over_time_and_report(self):
        first = self.brain.decide(self.observation())
        self.assertEqual(set(first["needs"]), {"hunger", "boredom", "affection", "bond"})
        hunger, boredom = first["needs"]["hunger"], first["needs"]["boredom"]
        for _ in range(120):  # One hour of decisions, 30 s apart.
            self.now += 30
            decision = self.brain.decide(self.observation())
        self.assertAlmostEqual(decision["needs"]["hunger"], min(100, hunger + 25), delta=0.5)
        self.assertEqual(decision["needs"]["boredom"], 100)
        self.now += 100000  # A long suspend/pause is capped, not counted as starving time.
        self.assertLessEqual(self.brain.decide(self.observation())["needs"]["hunger"], hunger + 26)

    def test_interactions_feed_needs_once_per_session_count(self):
        self.brain.needs.update(hunger=80, boredom=60)
        self.brain.decide(self.interacting())  # Baseline.
        after = self.brain.decide(self.interacting(treats=1, petted=2, toy=1))["needs"]
        self.assertAlmostEqual(after["hunger"], 45, delta=0.1)
        self.assertLess(after["boredom"], 60 - 30)
        repeat = self.brain.decide(self.interacting(treats=1, petted=2, toy=1))["needs"]
        self.assertAlmostEqual(repeat["hunger"], after["hunger"], delta=0.1)  # Same totals: no double credit.
        restarted = self.brain.decide({**self.interacting(treats=5), "session_id": "s2"})["needs"]
        self.assertAlmostEqual(restarted["hunger"], after["hunger"], delta=0.1)  # New session sets a baseline.

    def test_bond_changes_are_capped_per_day_and_recoverable(self):
        start = self.brain.needs["bond"]
        self.brain.decide(self.interacting())
        self.brain.decide(self.interacting(petted=100))
        self.assertAlmostEqual(self.brain.needs["bond"], start + 3, delta=0.01)
        self.brain.decide(self.interacting(petted=100, rough_throws=100))
        self.assertAlmostEqual(self.brain.needs["bond"], start + 1, delta=0.01)  # Loss capped at 2 per day.
        self.assertGreater(self.brain.needs["bond"], 0)

    def test_hunger_and_boredom_ask_with_thought_bubbles(self):
        self.brain.needs.update(hunger=90)
        decision = self.brain.decide(self.observation(idle=50))
        self.assertEqual((decision["command"], decision.get("thought")), ("SEEK_ATTENTION", "hungry"))
        self.now += 5
        self.assertNotIn("thought", self.brain.decide(self.observation(idle=55)))  # Bubble cooldown.
        self.brain.needs.update(hunger=10, boredom=90)
        self.now += 61
        decision = self.brain.decide(self.observation(idle=70))
        self.assertEqual((decision["command"], decision.get("thought")), ("SEEK_ATTENTION", "bored"))

    def test_ambient_bubbles_are_rare(self):
        self.brain.needs.update(boredom=70, hunger=0)
        thoughts = []
        for _ in range(60):  # Ten minutes of decisions with a recently touched pet.
            self.now += 10
            self.brain.needs.update(boredom=70)
            thoughts.append(self.brain.decide(self.observation(idle=5)).get("thought"))
        self.assertLessEqual(sum(t == "bored" for t in thoughts), 7)
        self.assertGreaterEqual(sum(t == "bored" for t in thoughts), 5)

    def test_no_thought_or_need_action_while_busy(self):
        self.brain.needs.update(hunger=95, boredom=95)
        for state in ("Sleeping", "Dragging", "Thrown"):
            decision = self.brain.decide({**self.observation(idle=100), "state": state, "grounded": state != "Thrown"})
            self.assertEqual(decision["command"], "IDLE")
            self.assertNotIn("thought", decision)

    def test_petting_earns_a_heart_when_affectionate(self):
        self.brain.needs.update(affection=70, hunger=0, boredom=0)
        self.brain.decide(self.interacting())
        self.assertEqual(self.brain.decide(self.interacting(petted=1)).get("thought"), "love")

    def test_boredom_shifts_choices_toward_play(self):
        def jumps(boredom):
            brain = Brain(Path(self.temporary.name) / f"b{boredom}.json", ROOT / "ai/personality.json", random.Random(3), lambda: self.now)
            picks = []
            for _ in range(400):
                brain.needs.update(boredom=boredom, hunger=0)
                picks.append(brain.decide(self.observation())["command"])
            return picks.count("JUMP") + picks.count("FOLLOW_MOUSE")
        self.assertGreater(jumps(100), jumps(0) * 1.3)

    def test_greets_after_long_absence_with_bond(self):
        wall = [1_000_000.0]
        self.brain.wall_clock = lambda: wall[0]
        self.brain.needs.update(bond=60)
        self.brain.decide(self.observation())
        self.brain.save()
        wall[0] += 7200
        other = Brain(self.memory, ROOT / "ai/personality.json", random.Random(1), lambda: self.now, lambda: wall[0])
        greeting = other.decide(self.observation())
        self.assertEqual((greeting["command"], greeting.get("thought")), ("FOLLOW_MOUSE", "love"))
        self.assertNotEqual(other.decide(self.observation()).get("thought"), "love")  # Only once.
        self.assertEqual(other.needs["bond"], 60)  # Needs persist across restarts.

    def test_favorite_hour_needs_history(self):
        self.assertFalse(self.brain.favorite_hour(20))
        self.brain.decide(self.interacting())
        self.brain.decide({**self.interacting(petted=20), "local_hour": 20.5})
        self.assertTrue(self.brain.favorite_hour(20))
        self.assertFalse(self.brain.favorite_hour(9))

    def test_memory_survives_restart(self):
        self.brain.decide(self.observation(count=1))
        self.brain.decide(self.observation(count=4))
        self.brain.save()
        other = Brain(self.memory, ROOT / "ai/personality.json")
        self.assertEqual(other.memory["decisions"], 2)
        self.assertEqual(other.memory["interactions"], 3)
        self.assertEqual(other.energy, self.brain.energy)
        self.assertEqual(other.recent, self.brain.recent)

    def test_no_consecutive_action_families(self):
        previous = None
        for _ in range(300):
            command = self.brain.decide(self.observation())["command"]
            family = self.brain.family(command)
            self.assertNotEqual(family, previous)
            previous = family

    def test_energy_uses_executed_activity_only(self):
        totals = dict(walking=0, playing=0, sleeping=0, resting=0)
        obs = {**self.observation(), "activity": totals}
        self.brain.decide(obs)
        for _ in range(20):
            self.brain.decide(obs)
        self.assertEqual(self.brain.energy, 80)  # Recommendations cost nothing.
        totals.update(walking=20, playing=2)
        self.brain.decide(obs)
        self.assertEqual(self.brain.energy, 67)
        totals.update(sleeping=10)
        self.brain.decide(obs)
        self.assertEqual(self.brain.energy, 100)
        for _ in range(30):
            self.assertNotEqual(self.brain.decide(obs)["command"], "SLEEP")
        totals.update(walking=float("nan"))
        self.brain.decide(obs)
        self.assertEqual(self.brain.energy, 100)

    def test_low_energy_favors_rest(self):
        class Capture:
            weights = None
            def choices(self, commands, weights, k):
                self.weights = dict(zip(commands, weights))
                return ["IDLE"]
            def uniform(self, low, high):
                return low
        capture = Capture()
        self.brain.rng = capture
        self.brain.energy = 10
        self.brain.decide(self.observation())
        low = capture.weights
        self.brain.recent = []
        self.brain.energy = 90
        self.brain.decide(self.observation())
        high = capture.weights
        self.assertGreater(low["SLEEP"], high["SLEEP"])
        self.assertLess(low["JUMP"], high["JUMP"])

    def test_sleep_and_waking_never_request_activity(self):
        for state in ("Sleeping", "Waking", "Dragging", "Thrown"):
            self.assertEqual(self.brain.decide({**self.observation(400), "state": state})["command"], "IDLE")
        self.assertEqual(self.brain.decide({**self.observation(400), "emotion": "Sleepy"})["command"], "IDLE")

    def test_treat_rewards_survive_restart_without_double_credit(self):
        obs = {**self.observation(), "session_id": "test-session", "activity":
               dict(walking=0, playing=0, sleeping=0, resting=0, treats=1)}
        self.assertEqual(self.brain.decide(obs)["energy"], 86)
        self.assertEqual(self.brain.decide(obs)["energy"], 86)
        self.brain.save()
        other = Brain(self.memory, ROOT / "ai/personality.json")
        self.assertEqual(other.decide(obs)["energy"], 86)
        obs["activity"]["treats"] = 2
        self.assertEqual(other.decide(obs)["energy"], 92)

    def test_personality_changes_policy_weights(self):
        class Capture:
            def choices(self, commands, weights, k):
                self.weights = dict(zip(commands, weights))
                return ["IDLE"]
            def uniform(self, low, high): return low
        capture = Capture()
        self.brain.rng = capture
        results = {}
        for profile in ("Balanced", "Playful", "Curious", "Calm"):
            self.brain.recent = []
            self.brain.decide({**self.observation(), "personality": profile})
            results[profile] = capture.weights
        self.assertGreater(results["Playful"]["JUMP"], results["Balanced"]["JUMP"])
        self.assertGreater(results["Curious"]["FOLLOW_MOUSE"], results["Balanced"]["FOLLOW_MOUSE"])
        self.assertGreater(results["Calm"]["SIT"], results["Balanced"]["SIT"])

    def test_corrupt_memory_is_recoverable(self):
        for content in ("broken json", '[]', '{"last_command": [], "decisions": "oops"}'):
            self.memory.write_text(content)
            with redirect_stderr(io.StringIO()):
                brain = Brain(self.memory, ROOT / "ai/personality.json")
            self.assertIn(brain.decide(self.observation())["command"], COMMANDS)

    def test_unwritable_memory_does_not_block_decisions(self):
        self.memory.mkdir()
        with redirect_stderr(io.StringIO()):
            self.assertIn(self.brain.decide(self.observation())["command"], COMMANDS)

    def test_airborne_is_idle(self):
        observation = self.observation(400)
        observation.update(grounded=False, state="Thrown")
        self.assertEqual(self.brain.decide(observation)["command"], "IDLE")

    def test_protocol_and_invalid_input(self):
        request = {"version": 1, "id": 7, "type": "observe", "observation": self.observation()}
        self.assertEqual(handle(request, self.brain)["id"], 7)
        for field, value in (("version", 9), ("id", True), ("type", "move_window"), ("observation", [])):
            with self.assertRaises(ValueError):
                handle({**request, field: value}, self.brain)

    def test_live_tuning_is_validated_and_not_sticky(self):
        self.brain.decide({**self.observation(), "tuning": {"playfulness": 0.12, "curiosity": 9, "sleepiness": True}})
        self.assertEqual(self.brain.playfulness, 0.12)
        self.assertEqual(self.brain.curiosity, 1)
        self.assertEqual(self.brain.sleepiness, 0.25)
        self.brain.decide(self.observation())
        self.assertEqual(self.brain.playfulness, 0.7)
        for invalid in ([], "bad", None):
            self.assertIn(self.brain.decide({**self.observation(), "tuning": invalid})["command"], COMMANDS)

    def test_live_energy_settings_apply_to_executed_activity(self):
        totals = dict(walking=0, playing=0, sleeping=0, resting=0)
        obs = {**self.observation(), "activity": totals, "tuning": {"walking_energy_per_second": 2}}
        self.brain.decide(obs)
        totals["walking"] = 5
        self.assertEqual(self.brain.decide(obs)["energy"], 70)

    def test_personality_changes_duration_and_attention_timing(self):
        class Fixed:
            def choices(self, commands, weights, k): return ["LOOK"]
            def uniform(self, low, high): return 4
        self.brain.rng = Fixed()
        normal = self.brain.decide(self.observation())["duration_seconds"]
        curious = self.brain.decide({**self.observation(), "personality": "Curious"})["duration_seconds"]
        self.assertGreater(curious, normal)
        self.assertNotEqual(self.brain.decide({**self.observation(350), "personality": "Calm"})["command"], "SEEK_ATTENTION")
        self.assertEqual(self.brain.decide({**self.observation(350), "personality": "Playful"})["command"], "SEEK_ATTENTION")

    def test_care_and_sleep_do_not_seek_attention(self):
        for state in ("Sleeping", "PreparingJump", "Grooming", "Stretching", "SeekingAttention"):
            self.assertEqual(self.brain.decide({**self.observation(1000), "state": state})["command"], "IDLE")

    def test_stdio_and_eof_shutdown(self):
        request = {"version": 1, "id": 1, "type": "observe", "observation": self.observation()}
        result = subprocess.run([sys.executable, "-u", str(ROOT / "ai/service.py"), "--memory", str(self.memory)],
                                input="bad json\n" + json.dumps(request) + "\n", text=True,
                                capture_output=True, timeout=5)
        self.assertEqual(result.returncode, 0)
        lines = result.stdout.splitlines()
        self.assertEqual(len(lines), 2)
        self.assertEqual(json.loads(lines[0])["type"], "error")
        self.assertEqual(json.loads(lines[1])["type"], "decision")
        self.assertTrue(self.memory.exists())


class SpeechAndLearningTests(unittest.TestCase):
    KEYS = ("petted", "clicked", "treats", "toy", "ball_clicks", "ball_hits", "rough_throws")

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.memory = Path(self.temporary.name) / "memory.json"
        self.now = 1000.0
        self.wall = [1_700_000_000.0]
        self.brain = self.make()
        self.counts = {key: 0 for key in self.KEYS}

    def make(self, seed=5):
        return Brain(self.memory, ROOT / "ai/personality.json", random.Random(seed), lambda: self.now, lambda: self.wall[0])

    def obs(self, state="Idle", language="vi", chattiness=1, **extra):
        return {"state": state, "grounded": state not in ("Thrown", "Jumping", "Dragging"), "seconds_since_interaction": 5,
                "interaction_count": sum(self.counts.values()), "last_event": "started", "last_applied_command": "LOCAL",
                "session_id": "s", "interactions": dict(self.counts), "speech_enabled": True, "language": language,
                "chattiness": chattiness, "pet_name": "Mochi", "personality": "Balanced", **extra}

    def step(self, seconds=3, **extra):
        self.now += seconds
        return self.brain.decide(self.obs(**extra))

    def test_learned_lines_follow_the_ui_language(self):
        speech = Speech(ROOT / "ai/lines.json")
        vi_id = speech.lines["hello"]["vi"][0][0]
        self.assertTrue(speech.describe(vi_id, "vi").startswith("“"))
        self.assertEqual(speech.describe(vi_id, "en"), "a greeting")  # No Vietnamese text in the English UI.
        self.assertEqual(self.brain.describe_learned("act:night/hungry:CHASE_TAIL", "en"), "Chasing its tail · night, hungry")
        self.assertEqual(self.brain.describe_learned("act:day/ok:SIT", "vi"), "Ngồi · ban ngày")

    def test_japanese_falls_back_to_english_until_translated(self):
        speech = Speech(ROOT / "ai/lines.json")
        rng = random.Random(2)
        picked = speech.pick("hungry", "ja", "Balanced", "Mochi", lambda _: 1, rng)
        self.assertTrue(picked["id"].startswith(("hungry:ja:", "hungry:en:")))
        en_id = speech.lines["hello"]["en"][0][0]
        self.assertTrue(speech.describe(en_id, "ja").startswith("“"))  # The English line it spoke is quoted.
        vi_id = speech.lines["hello"]["vi"][0][0]
        self.assertFalse(speech.describe(vi_id, "ja").startswith("“"))  # A Vietnamese line is named, not quoted.
        self.assertTrue(self.brain.describe_learned("act:night/hungry:CHASE_TAIL", "ja"))
        self.assertTrue(self.step(chattiness=0, language="ja")["speech"]["id"].startswith(("hello:ja:", "hello:en:")))

    def test_lines_file_is_complete_for_both_languages(self):
        speech = Speech(ROOT / "ai/lines.json")
        ids = []
        for category, languages in speech.lines.items():
            for language in ("vi", "en"):
                options = languages.get(language, [])
                self.assertGreaterEqual(len(options), 5, f"{category}/{language}")
                ids += [line_id for line_id, text, _ in options]
                self.assertTrue(all(len(text) <= 70 for _, text, _ in options))
        self.assertEqual(len(ids), len(set(ids)))
        self.assertGreaterEqual(len(speech.lines), 15)
        self.assertTrue(any("sen" in text for _, text, _ in speech.lines["hungry"]["vi"]))
        self.assertTrue(any("human" in text.lower() for _, text, _ in speech.lines["hungry"]["en"]))

    def test_pick_avoids_repeats_filters_personality_and_names_pet(self):
        speech = Speech(ROOT / "ai/lines.json")
        rng = random.Random(1)
        picks = [speech.pick("hungry", "vi", "Balanced", "Bơ\n", lambda _: 1, rng) for _ in range(6)]
        self.assertEqual(len({p["id"] for p in picks}), 6)
        self.assertTrue(all("{name}" not in p["text"] and "\n" not in p["text"] for p in picks))
        self.assertTrue(any("Bơ" in p["text"] for p in picks))
        calm = {speech.pick("petted", "en", "Calm", "Mochi", lambda _: 1, rng)["text"] for _ in range(40)}
        self.assertNotIn("You're so good at this! More!", calm)
        self.assertIsNone(speech.pick("nonexistent", "vi", "Balanced", "Mochi", lambda _: 1, rng))

    def test_silent_without_opt_in_and_hello_once(self):
        self.now += 3
        self.assertNotIn("speech", self.brain.decide({**self.obs(), "speech_enabled": False}))
        brain = self.make()
        self.brain = brain
        first = self.step()
        self.assertTrue(first["speech"]["id"].startswith("hello:vi:"))
        self.assertTrue(all(not d.get("speech", {}).get("id", "").startswith("hello") for d in [self.step(10) for _ in range(5)]))

    def test_event_lines_follow_reactions_in_chosen_language(self):
        self.step(chattiness=0)  # hello
        self.counts["petted"] += 1
        petted = self.step(10, language="en", chattiness=0)
        self.assertTrue(petted["speech"]["id"].startswith("petted:en:"))
        self.counts["treats"] += 1
        self.assertTrue(self.step(10, chattiness=0)["speech"]["id"].startswith("treat_thanks:vi:"))

    def test_battle_results_speak_once_and_fill_the_diary(self):
        battles = {"won": 0, "lost": 0, "combos": 0, "records": 0}
        first = self.step(chattiness=0, battles=dict(battles))  # hello; the first report is a baseline
        self.assertEqual((first["diary"]["battles_won"], first["diary"]["combos"]), (0, 0))
        needs = dict(self.brain.needs)
        battles.update(won=1, combos=2, records=1)
        record = self.step(10, chattiness=0, battles=dict(battles))
        self.assertTrue(record["speech"]["id"].startswith("battle_record:vi:"))  # A record beats a plain win.
        self.assertEqual(self.brain.needs["bond"], needs["bond"])  # Battles never change bond.
        self.assertNotIn("speech", self.step(10, chattiness=0, battles=dict(battles)))  # Counted once.
        battles["lost"] = 1
        lost = self.step(10, language="en", chattiness=0, battles=dict(battles))
        self.assertTrue(lost["speech"]["id"].startswith("battle_lost:en:"))
        self.assertEqual((lost["diary"]["battles_won"], lost["diary"]["battles_lost"], lost["diary"]["combos"]), (1, 1, 2))
        self.brain.save()
        again = self.make()  # A restarted AI sees the same session's counts as a baseline only.
        diary = again.decide(self.obs(battles=dict(battles)))["diary"]
        self.assertEqual((diary["battles_won"], diary["battles_lost"], diary["combos"]), (1, 1, 2))
        for bad in ("x", {"won": "many", "lost": -3}, None):
            self.assertEqual(again.decide(self.obs(battles=bad))["diary"]["battles_won"], 1)

    def test_event_waits_until_the_pet_can_talk(self):
        self.step(chattiness=0)
        self.counts["rough_throws"] += 1
        self.assertNotIn("speech", self.step(8, state="Thrown", chattiness=0))
        landed = self.step(2, emotion="Angry", chattiness=0)
        self.assertTrue(landed["speech"]["id"].startswith("thrown_rough:vi:"))
        self.counts["rough_throws"] += 1
        self.step(1, state="Thrown", chattiness=0)
        self.assertNotIn("speech", self.step(20, chattiness=0))  # Too late to still react.

    def test_chattiness_controls_ambient_lines(self):
        def ambient(chattiness, quiet=False):
            self.brain = self.make(seed=chattiness + 10)
            self.step(chattiness=chattiness, quiet=quiet)  # hello
            chatter = ("idle:", "morning:", "night:")
            total = 0
            for _ in range(120):
                self.brain.needs.update(hunger=0, boredom=0)
                total += self.step(15, chattiness=chattiness, quiet=quiet).get("speech", {}).get("id", "").startswith(chatter)
            return total
        self.assertEqual(ambient(0), 0)
        self.assertEqual(ambient(2, quiet=True), 0)
        rarely, often = ambient(1), ambient(2)
        self.assertGreater(rarely, 0)
        self.assertGreater(often, rarely)

    def test_need_lines_are_paced_by_chattiness(self):
        def need_lines(chattiness):
            self.brain = self.make(seed=3)
            self.step(chattiness=chattiness)
            total = 0
            for _ in range(80):  # 20 minutes while staying bored.
                self.brain.needs.update(boredom=90, hunger=0)
                total += self.step(15, chattiness=chattiness).get("speech", {}).get("id", "").startswith("bored:")
            return total
        rarely, often = need_lines(0), need_lines(2)
        self.assertLessEqual(rarely, 4)
        self.assertGreater(often, rarely)

    def test_large_learned_memory_still_loads(self):
        for i in range(500):
            self.brain.learner.reward(f"say:treat_thanks:vi:{i:08x}-" + "x" * 80, 1)
        self.brain.needs.update(bond=77)
        self.step(chattiness=0)
        self.brain.save()
        self.assertGreater(self.memory.stat().st_size, 65536)  # Larger than the old limit.
        restored = self.make()
        self.assertEqual(len(restored.learner.values), 500)
        self.assertEqual(restored.needs["bond"], 77)

    def test_learner_is_bounded_decays_and_clears(self):
        learner = Learner({}, lambda: self.wall[0])
        for _ in range(50):
            learner.reward("k", 1)
        self.assertAlmostEqual(learner.weight("k"), 2, delta=0.01)
        for _ in range(50):
            learner.reward("bad", -1)
        self.assertAlmostEqual(learner.weight("bad"), 0.5, delta=0.01)
        learner.reward("k", 0)
        self.wall[0] += 7 * 86400
        self.assertAlmostEqual(learner.score("k"), 0.5, delta=0.01)  # Half-life of a week.
        learner.clear()
        self.assertEqual(learner.weight("k"), 1)

    def test_reactions_credit_the_shown_action_and_line(self):
        decision = self.step(chattiness=0)
        command, line = decision["command"], decision["speech"]["id"]
        self.counts["petted"] += 1
        self.step(4, chattiness=0, last_applied_command=command, last_speech={"id": line, "age": 3.5})
        ctx = learning_context(None, self.brain.needs, self.brain.energy)
        self.assertGreater(self.brain.learner.score("say:" + line), 0)
        self.assertGreater(max(self.brain.learner.score(k) for k in self.brain.learner.values if k.startswith("act:")), 0)
        self.counts["rough_throws"] += 1
        self.step(30, chattiness=0, last_speech={"id": line, "age": 40})  # Stale line gets no blame.
        self.assertGreater(self.brain.learner.score("say:" + line), 0)
        self.assertTrue(ctx)

    def test_learning_shapes_choices_persists_and_resets(self):
        ctx = learning_context(None, self.brain.needs, self.brain.energy)
        for _ in range(30):
            self.brain.learner.reward(self.brain.action_key(ctx, "JUMP"), 1)
        summary = self.step(chattiness=0)["learned"]
        self.assertTrue(summary["likes"] and summary["likes"][0].startswith("Nhảy"))
        english = self.step(chattiness=0, language="en")["learned"]
        self.assertTrue(english["likes"][0].startswith("Jumping") and "JUMP" not in english["likes"][0])
        self.brain.save()
        restored = self.make()
        self.assertGreater(restored.learner.score(restored.action_key(ctx, "JUMP")), 0.9)
        def jumps(brain):
            self.brain = brain
            total = 0
            for _ in range(300):
                brain.needs.update(hunger=0, boredom=0)  # Keep the learned situation fixed.
                total += self.step(5, chattiness=0)["command"] == "JUMP"
            return total
        learned = jumps(restored)
        fresh = Brain(Path(self.temporary.name) / "other.json", ROOT / "ai/personality.json", random.Random(5),
                      lambda: self.now, lambda: self.wall[0])
        self.assertGreater(learned, jumps(fresh) * 1.15)  # Variety rules still cap repeats.
        self.brain = restored
        self.step(chattiness=0, learning_reset="r1")
        self.assertEqual(restored.learner.values, {})
        restored.learner.reward("x", 1)
        self.step(chattiness=0, learning_reset="r1")  # Same reset token is applied only once.
        self.assertIn("x", restored.learner.values)

    def test_break_reminder_speaks_first_and_even_with_chatter_off(self):
        self.step(chattiness=0)  # hello
        self.counts["petted"] += 1
        reminder = self.step(10, chattiness=0, break_due=True)
        self.assertTrue(reminder["speech"]["id"].startswith("break:vi:"))
        self.assertNotIn("{name}", reminder["speech"]["text"])
        self.assertNotIn("speech", self.step(1, chattiness=0, break_due=True))  # Still paced.
        self.now += 10
        quiet = self.brain.decide({**self.obs(chattiness=0, language="en"), "speech_enabled": False, "break_due": True})
        self.assertTrue(quiet["speech"]["id"].startswith("break:en:"))
        self.now += 10
        self.assertNotIn("speech", self.brain.decide({**self.obs(chattiness=2), "speech_enabled": False}))
        self.assertNotIn("speech", self.step(10, state="Sleeping", chattiness=0, break_due=True))

    def test_fullscreen_silences_everything(self):
        self.brain = self.make(seed=12)
        self.assertNotIn("speech", self.step(chattiness=2, context={"fullscreen": True, "away": False, "returned": False}))
        self.counts["petted"] += 1
        for _ in range(40):
            self.assertNotIn("speech", self.step(15, chattiness=2, break_due=True,
                                                 context={"fullscreen": True, "away": False, "returned": False}))
        self.assertTrue(self.step(15, chattiness=2, break_due=True)["speech"]["id"].startswith("break:"))  # Talks again afterwards.

    def test_returning_user_is_welcomed_back(self):
        self.step(chattiness=0)  # hello
        self.now += 400
        back = self.step(3, chattiness=0, context={"fullscreen": False, "away": False, "returned": True})
        self.assertTrue(back["speech"]["id"].startswith("back:vi:"))
        self.assertNotIn("speech", self.step(10, chattiness=0, context={"fullscreen": False, "away": False, "returned": False}))
        for bad in ("yes", 1, None, [], {"returned": "true"}):
            self.now += 30
            self.assertFalse(self.brain.decide(self.obs(chattiness=0, context=bad)).get("speech", {}).get("id", "").startswith("back:"))

    def at(self, year, month, day):
        return time.mktime((year, month, day, 12, 0, 0, 0, 0, -1))

    def test_special_days_change_the_greeting(self):
        self.wall[0] = self.at(2026, 12, 25)
        self.assertTrue(self.step()["speech"]["id"].startswith("christmas:vi:"))
        self.brain = self.make()
        self.assertTrue(self.step(birthday="12-25", language="en")["speech"]["id"].startswith("birthday:en:"))  # Birthday wins.
        self.assertEqual(self.step(birthday="12-25")["special_day"], "birthday")  # C# picks the holiday accessory from it.
        self.wall[0] = self.at(2026, 11, 3)
        self.brain = self.make()
        ordinary = self.step(birthday="12-25")
        self.assertTrue(ordinary["speech"]["id"].startswith("hello:vi:"))  # Ordinary day.
        self.assertNotIn("special_day", ordinary)

    def test_special_day_chatter_mixes_in(self):
        self.wall[0] = self.at(2026, 10, 31)
        self.step(chattiness=2)
        ids = [self.step(40, chattiness=2).get("speech", {}).get("id", "") for _ in range(40)]
        self.assertTrue(any(i.startswith("halloween:") for i in ids))
        self.assertTrue(any(i.startswith(("idle:", "morning:", "night:")) for i in ids))  # Still ordinary chatter too.


class CalendarTests(unittest.TestCase):
    def setUp(self):
        self.calendar = Calendar(ROOT / "ai/calendar.json")

    def test_rhythm_matches_the_old_built_in_hours(self):
        def old(hour):
            if hour >= 22 or hour < 6:
                return 0.6, 1.2, 1.8
            if 7 <= hour < 10:
                return 1.2, 1.0, 0.7
            return 1.0, 1.0, 1.0
        for quarter in range(96):
            self.assertEqual(self.calendar.rhythm(quarter / 4), old(quarter / 4), quarter / 4)
        self.assertEqual(day_rhythm(23), (0.6, 1.2, 1.8))

    def test_holidays_birthday_and_anniversary(self):
        today = lambda *ymd, **kw: (self.calendar.today(dt.date(*ymd), **kw) or (None,))[0]
        self.assertEqual(today(2027, 2, 5), "tet")  # New Year's Eve.
        self.assertEqual(today(2027, 2, 8), "tet")  # Third day.
        self.assertIsNone(today(2027, 2, 9))
        self.assertIsNone(today(2051, 2, 11))  # Lunar dates beyond the table (to 2050) are simply not known.
        # Vietnamese calendar (UTC+7): a day before China's when the new moon falls 23:00-24:00 here.
        self.assertEqual((today(2030, 2, 2), today(2034, 9, 26), today(2034, 9, 27)), ("tet", "mid_autumn", None))
        self.assertEqual((today(2050, 1, 23), today(2050, 9, 30)), ("tet", "mid_autumn"))
        self.assertEqual((today(2027, 1, 1), today(2027, 3, 8), today(2026, 10, 20)), ("new_year", "womens_day", "vn_womens_day"))
        self.assertEqual(today(2026, 9, 25), "mid_autumn")
        self.assertEqual(today(2026, 10, 31), "halloween")
        self.assertEqual((today(2026, 12, 24), today(2026, 12, 25), today(2026, 12, 26)), ("christmas", "christmas", None))
        self.assertEqual(today(2026, 12, 25, birthday="12-25"), "birthday")
        self.assertEqual(today(2027, 2, 28, birthday="02-29"), "birthday")  # Leap-day birthdays still come.
        self.assertIsNone(today(2026, 5, 4, birthday="13-40"))
        met = dt.date(2026, 10, 13)
        self.assertIsNone(today(2026, 10, 13, first_seen=met))  # The first day is not an anniversary.
        self.assertEqual(today(2027, 10, 13, first_seen=met), "anniversary")
        self.assertGreater(self.calendar.today(dt.date(2027, 2, 6))[1], 1)  # Livelier on Tết.

    def test_every_calendar_event_has_lines(self):
        lines = json.loads((ROOT / "ai/lines.json").read_text(encoding="utf-8"))
        for event in json.loads((ROOT / "ai/calendar.json").read_text(encoding="utf-8"))["events"]:
            for language in ("vi", "en"):
                self.assertTrue(lines.get(event["id"], {}).get(language), (event["id"], language))

    def test_broken_or_partial_file_falls_back_safely(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        folder = Path(temporary.name)
        broken = folder / "broken.json"
        broken.write_text("{ nope", encoding="utf-8")
        with redirect_stderr(io.StringIO()):
            fallback = Calendar(broken)
        self.assertEqual(fallback.rhythm(23), (0.6, 1.2, 1.8))
        self.assertIsNone(fallback.today(dt.date(2026, 12, 25)))
        partial = folder / "partial.json"
        partial.write_text(json.dumps({"rhythm": [{"from": 30, "to": 2}, {"from": 9, "to": 12, "active": 99}],
                                       "events": [{"id": "Bad Id", "date": "12-25"}, {"id": "x", "date": "1225"},
                                                  {"id": "ok", "date": "05-04", "from": 9}, {"id": "fine", "date": "05-04"}]}),
                           encoding="utf-8")
        loaded = Calendar(partial)
        self.assertEqual(loaded.rhythm(10), (1.0, 1.0, 1.0))  # Out-of-range factor is ignored.
        self.assertEqual(loaded.rhythm(1), (1.0, 1.0, 1.0))
        self.assertEqual(loaded.today(dt.date(2026, 5, 4)), ("fine", 1.0))
        self.assertIsNone(loaded.today(dt.date(2026, 12, 25)))


if __name__ == "__main__":
    unittest.main()
