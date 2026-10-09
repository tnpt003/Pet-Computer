import io
import json
from pathlib import Path
import random
import subprocess
import sys
import tempfile
import unittest
from contextlib import redirect_stderr

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "ai"))
from service import Brain, COMMANDS, handle


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
        self.assertEqual(commands, COMMANDS - {"SEEK_ATTENTION"})

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


if __name__ == "__main__":
    unittest.main()
