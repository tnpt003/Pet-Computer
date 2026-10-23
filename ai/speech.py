"""Choose short speech lines from lines.json; standard library only."""
from __future__ import annotations

from collections import deque
import hashlib
import json
from pathlib import Path
import sys

LANGUAGES = ("vi", "en", "ja")  # mirrors PetLanguage.All in C#
# A language without its own lines for a situation (yet) speaks these instead.
FALLBACK = {"ja": "en"}
# How a learned line from the other language is named (the UI shows one language only).
CATEGORY_NAMES = {
    "hello": {"vi": "câu chào", "en": "a greeting"},
    "welcome_back": {"vi": "câu chào khi gặp lại", "en": "a welcome back"},
    "petted": {"vi": "câu khi được vuốt", "en": "a petting line"},
    "treat_thanks": {"vi": "câu cảm ơn đồ ăn", "en": "a treat thank-you"},
    "play": {"vi": "câu khi chơi", "en": "a play line"},
    "thrown_rough": {"vi": "câu khi bị ném mạnh", "en": "a rough-throw line"},
    "thrown_fun": {"vi": "câu khi được tung", "en": "a fun-throw line"},
    "back": {"vi": "câu khi sen quay lại", "en": "a you're-back line"},
    "battle_won": {"vi": "câu khi thắng trận", "en": "a battle-won line"},
    "battle_lost": {"vi": "câu khi thua trận", "en": "a battle-lost line"},
    "battle_record": {"vi": "câu khi có kỷ lục mới", "en": "a new-record line"},
    "break": {"vi": "câu nhắc nghỉ", "en": "a break reminder"},
    "wake_up": {"vi": "câu khi thức dậy", "en": "a wake-up line"},
    "sleepy": {"vi": "câu buồn ngủ", "en": "a sleepy line"},
    "lonely": {"vi": "câu nhớ sen", "en": "a lonely line"},
    "hungry": {"vi": "câu đòi ăn", "en": "a hungry line"},
    "bored": {"vi": "câu chán", "en": "a bored line"},
    "morning": {"vi": "câu buổi sáng", "en": "a morning line"},
    "night": {"vi": "câu buổi tối", "en": "a night line"},
    "idle": {"vi": "câu nói vu vơ", "en": "a chatter line"},
    "birthday": {"vi": "câu chúc sinh nhật", "en": "a birthday wish"},
    "anniversary": {"vi": "câu kỷ niệm ngày gặp nhau", "en": "an anniversary line"},
    "tet": {"vi": "câu chúc Tết", "en": "a Lunar New Year wish"},
    "mid_autumn": {"vi": "câu Trung thu", "en": "a Mid-Autumn line"},
    "halloween": {"vi": "câu Halloween", "en": "a Halloween line"},
    "christmas": {"vi": "câu Giáng sinh", "en": "a Christmas line"},
}
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
        by_language = self.lines.get(category, {})
        options = by_language.get(language) or by_language.get(FALLBACK.get(language, "vi")) or by_language.get("vi") or []
        options = [o for o in options if not o[2] or personality in o[2]]
        fresh = [o for o in options if o[0] not in self.recent] or [o for o in options if o[0] not in list(self.recent)[-3:]]
        if not fresh:
            return None
        line_id, text, _ = rng.choices(fresh, weights=[weight(o[0]) for o in fresh], k=1)[0]
        self.recent.append(line_id)
        return {"id": line_id, "text": text.replace("{name}", clean_name(name))[:MAX_TEXT]}

    def describe(self, line_id: str, language: str | None = None) -> str:
        """Quote a learned line; a line in the other language is named by its situation instead."""
        category, _, rest = line_id.partition(":")
        line_language = rest.partition(":")[0]
        if language in LANGUAGES and line_language in LANGUAGES and line_language not in (language, FALLBACK.get(language)):
            names = CATEGORY_NAMES.get(category, {})
            return names.get(language) or names.get(FALLBACK.get(language, "")) or ("một câu nói" if language == "vi" else "a line")
        text = self.texts.get(line_id, line_id)
        return "“" + (text if len(text) <= 28 else text[:27] + "…") + "”"
