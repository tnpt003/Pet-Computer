"""Day rhythm and special days from calendar.json; standard library only.

Named seasons.py (not calendar.py) so it never shadows the standard calendar module.
"""
from __future__ import annotations

import datetime as dt
import json
from pathlib import Path
import re
import sys

# The rhythm used before calendar.json existed, kept when the file is missing or broken.
DEFAULT_RHYTHM = ((22, 6, (0.6, 1.2, 1.8)), (7, 10, (1.2, 1.0, 0.7)))
DAY = re.compile(r"^(\d{2})-(\d{2})$")
ID = re.compile(r"^[a-z][a-z0-9_]{0,31}$")


def parse_day(text, year: int) -> dt.date | None:
    """MM-DD in the given year; 02-29 falls back to 02-28 outside leap years."""
    match = DAY.match(text) if isinstance(text, str) else None
    if not match:
        return None
    month, day = int(match.group(1)), int(match.group(2))
    if (month, day) == (2, 29):
        day = 29 if year % 4 == 0 and (year % 100 != 0 or year % 400 == 0) else 28
    try:
        return dt.date(year, month, day)
    except ValueError:
        return None


def _factor(value, default=1.0) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not 0.2 <= value <= 3:
        return default
    return float(value)


class Calendar:
    def __init__(self, path: Path):
        self.rhythm_ranges = DEFAULT_RHYTHM
        self.events: list[dict] = []
        try:
            if path.stat().st_size > 65536:
                raise ValueError("file too large")
            data = json.loads(path.read_text(encoding="utf-8"))
            if not isinstance(data, dict):
                raise ValueError("expected an object")
        except FileNotFoundError:
            return
        except (OSError, ValueError) as exc:
            print(f"Ignoring unreadable {path.name}: {exc}", file=sys.stderr)
            return
        rhythm = data.get("rhythm")
        if isinstance(rhythm, list):
            ranges = []
            for item in rhythm:
                if not isinstance(item, dict):
                    continue
                start, end = item.get("from"), item.get("to")
                if type(start) is not int or type(end) is not int or not 0 <= start < 24 or not 0 <= end <= 24 or start == end:
                    continue
                ranges.append((start, end, (_factor(item.get("active")), _factor(item.get("rest")), _factor(item.get("sleep")))))
            self.rhythm_ranges = tuple(ranges)
        for item in data.get("events") if isinstance(data.get("events"), list) else []:
            if not isinstance(item, dict) or not isinstance(item.get("id"), str) or not ID.match(item["id"]):
                continue
            start, end = item.get("from", 0), item.get("to", 0)
            if type(start) is not int or type(end) is not int or not -7 <= start <= end <= 7:
                continue
            special = item.get("special")
            dates = item.get("dates") if isinstance(item.get("dates"), dict) else None
            if special not in (None, "birthday", "anniversary") or (special is None and dates is None and not DAY.match(str(item.get("date")))):
                continue
            self.events.append({"id": item["id"], "special": special, "date": item.get("date"), "dates": dates,
                                "from": start, "to": end, "active": _factor(item.get("active"))})

    def rhythm(self, hour) -> tuple[float, float, float]:
        """(active, rest, sleep) weight multipliers for the computer's local hour.

        Only biases Python's suggestions; C# still owns sleep length, the sleep lock
        and the wake cooldown. Missing or invalid hours leave weights unchanged.
        """
        if isinstance(hour, bool) or not isinstance(hour, (int, float)) or not 0 <= hour < 24:
            return 1.0, 1.0, 1.0
        for start, end, factors in self.rhythm_ranges:
            if (start <= hour < end) if start < end else (hour >= start or hour < end):
                return factors
        return 1.0, 1.0, 1.0

    def _anchors(self, event, year: int, first_seen: dt.date | None, birthday) -> list[dt.date]:
        # Neighbouring years too, so an offset can cross New Year.
        days = []
        for y in (year - 1, year, year + 1):
            if event["special"] == "birthday":
                day = parse_day(birthday, y)
            elif event["special"] == "anniversary":
                day = parse_day(first_seen.strftime("%m-%d"), y) if first_seen and y > first_seen.year else None
            elif event["dates"] is not None:
                day = parse_day(event["dates"].get(str(y)), y)
            else:
                day = parse_day(event["date"], y)
            if day:
                days.append(day)
        return days

    def today(self, today: dt.date, first_seen: dt.date | None = None, birthday=None) -> tuple[str, float] | None:
        """(event id, liveliness factor) for the first event covering today, else None."""
        for event in self.events:
            for anchor in self._anchors(event, today.year, first_seen, birthday):
                if event["from"] <= (today - anchor).days <= event["to"]:
                    return event["id"], event["active"]
        return None
