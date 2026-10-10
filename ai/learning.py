"""Learn from the user's reactions; standard library only.

Each learned key holds a score in [-1, 1] that moves toward observed rewards and
decays back toward 0 (half-life about a week). Scores become weight multipliers
between x0.5 and x2, so the pet favors what the user likes without ever dropping
or repeating a choice outright.
"""
from __future__ import annotations

import math

HALF_LIFE_DAYS = 7.0
LEARNING_RATE = 0.25
MAX_KEYS = 500
CREDIT_WINDOW = 15.0  # Seconds after an action/line during which reactions count.


def finite(value, default=0.0):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        return default
    return float(value)


def day_part(hour) -> str:
    hour = finite(hour, -1)
    if 6 <= hour < 11:
        return "morning"
    if 11 <= hour < 17:
        return "day"
    if 17 <= hour < 22:
        return "evening"
    return "night" if 0 <= hour < 24 else "day"


def context(hour, needs: dict, energy: float) -> str:
    """Coarse situation: part of day plus the most pressing need."""
    if needs.get("hunger", 0) >= 70:
        need = "hungry"
    elif needs.get("boredom", 0) >= 65:
        need = "bored"
    elif energy <= 30:
        need = "tired"
    else:
        need = "ok"
    return f"{day_part(hour)}/{need}"


class Learner:
    def __init__(self, stored, wall_clock):
        self.wall_clock = wall_clock
        self.values: dict[str, list[float]] = {}
        if isinstance(stored, dict):
            for key, pair in list(stored.items())[:MAX_KEYS]:
                if isinstance(key, str) and len(key) <= 120 and isinstance(pair, list) and len(pair) == 2:
                    score, stamp = finite(pair[0]), finite(pair[1])
                    self.values[key] = [max(-1.0, min(1.0, score)), stamp]

    def score(self, key: str) -> float:
        pair = self.values.get(key)
        if pair is None:
            return 0.0
        days = max(0.0, self.wall_clock() - pair[1]) / 86400
        return pair[0] * 0.5 ** (days / HALF_LIFE_DAYS)

    def weight(self, key: str) -> float:
        return 2.0 ** self.score(key)

    def reward(self, key: str, value: float):
        value = max(-1.0, min(1.0, finite(value)))
        if value == 0:
            return  # No reaction teaches nothing.
        current = self.score(key)
        self.values[key] = [round(current + LEARNING_RATE * (value - current), 4), round(self.wall_clock())]
        if len(self.values) > MAX_KEYS:
            weakest = min(self.values, key=lambda k: abs(self.score(k)))
            del self.values[weakest]

    def clear(self):
        self.values.clear()

    def export(self) -> dict:
        return {key: list(pair) for key, pair in self.values.items()}

    def summary(self, describe, count=3) -> dict:
        """Strongest likes and dislikes as short human-readable strings."""
        scored = [(self.score(key), key) for key in self.values]
        likes = sorted((s, k) for s, k in scored if s >= 0.15)[::-1][:count]
        dislikes = sorted((s, k) for s, k in scored if s <= -0.15)[:count]
        return {"likes": [f"{describe(k)} {s:+.2f}"[:60] for s, k in likes],
                "dislikes": [f"{describe(k)} {s:+.2f}"[:60] for s, k in dislikes]}
