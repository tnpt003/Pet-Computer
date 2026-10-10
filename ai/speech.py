"""Choose short speech lines from lines.json; standard library only."""
from __future__ import annotations

from collections import deque
import hashlib
import json
from pathlib import Path
import sys

LANGUAGES = ("vi", "en")
MAX_TEXT = 120
RECENT_LINES = 20


def clean_name(name) -> str:
    if not isinstance(name, str):
        return "Mochi"
    name = "".join(ch for ch in name if ch.isprintable()).strip()[:24]
    return name or "Mochi"


class Speech:
    def __init__(self, path: Path):
        self.lines: dict[str, dict[str, list[tuple[str, str, tuple]]]] = {}
        self.texts: dict[str, str] = {}
        self.recent: deque[str] = deque(maxlen=RECENT_LINES)
        try:
            if path.stat().st_size > 262144:
                raise ValueError("file too large")
            data = json.loads(path.read_text(encoding="utf-8"))
        except FileNotFoundError:
            data = {}
        except (OSError, ValueError) as exc:
            print(f"Ignoring unreadable {path.name}: {exc}", file=sys.stderr)
            data = {}
        if not isinstance(data, dict):
            data = {}
        for category, languages in data.items():
            if category.startswith("_") or not isinstance(languages, dict):
                continue
            for language in LANGUAGES:
                for entry in languages.get(language, []) if isinstance(languages.get(language), list) else []:
                    text, profiles = (entry, ()) if isinstance(entry, str) else (
                        (entry.get("text"), tuple(entry.get("personality") or ())) if isinstance(entry, dict) else (None, ()))
                    if not isinstance(text, str) or not text.strip() or len(text) > MAX_TEXT:
                        continue
                    # Stable ID from content, so reordering the file keeps learned scores.
                    line_id = f"{category}:{language}:{hashlib.sha1(text.encode('utf-8')).hexdigest()[:8]}"
                    self.lines.setdefault(category, {}).setdefault(language, []).append((line_id, text, profiles))
                    self.texts[line_id] = text

    def has(self, category: str) -> bool:
        return category in self.lines

    def pick(self, category, language, personality, name, weight, rng):
        """Weighted choice avoiding recently spoken lines; None if nothing fits."""
        language = language if language in LANGUAGES else "vi"
        options = self.lines.get(category, {}).get(language) or self.lines.get(category, {}).get("vi") or []
        options = [o for o in options if not o[2] or personality in o[2]]
        fresh = [o for o in options if o[0] not in self.recent] or [o for o in options if o[0] not in list(self.recent)[-3:]]
        if not fresh:
            return None
        line_id, text, _ = rng.choices(fresh, weights=[weight(o[0]) for o in fresh], k=1)[0]
        self.recent.append(line_id)
        return {"id": line_id, "text": text.replace("{name}", clean_name(name))[:MAX_TEXT]}

    def describe(self, line_id: str) -> str:
        text = self.texts.get(line_id, line_id)
        return "“" + (text if len(text) <= 28 else text[:27] + "…") + "”"
