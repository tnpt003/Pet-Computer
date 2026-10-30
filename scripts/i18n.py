"""The UI text keys: the English text of every L("vi", "en") / LF($"vi", $"en") call in src,
and of the data files (battle.json, coats.json, accessories.json, achievements.json: nameEn, descEn, ... and "en" names).

Other languages translate that English text (src/DesktopPet/Assets/i18n/<language>.json, read by
PetText.cs); interpolated text is keyed by its format, $"Wave {b.Wave} cleared!" -> "Wave {0} cleared!".
Run it to list what a table is missing:  .tools/python/python.exe scripts/i18n.py [ja]
tests/ai/test_i18n.py fails while a key has no translation.
"""
from pathlib import Path
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
ESCAPES = {'n': '\n', 't': '\t', '"': '"', '\\': '\\', 'r': '\r', '0': '\0', "'": "'"}


def starts_string(s, i):
    return s[i] == '"' or (s[i] in '$@' and s[i + 1] in '"$@')


def read_string(s, i):
    """Parse a C# string literal at s[i]; return (format text, end). Holes become {n[,align][:fmt]}."""
    interp = verb = False
    while s[i] in '$@':
        interp |= s[i] == '$'
        verb |= s[i] == '@'
        i += 1
    assert s[i] == '"'
    i += 1
    out, hole = [], 0
    while True:
        c = s[i]
        if c == '"':
            if verb and s[i + 1] == '"':
                out.append('"'); i += 2; continue
            return ''.join(out), i + 1
        if c == '\\' and not verb:
            n = s[i + 1]
            if n == 'u':
                out.append(chr(int(s[i + 2:i + 6], 16))); i += 6; continue
            out.append(ESCAPES.get(n, n)); i += 2; continue
        if interp and c == '{':
            if s[i + 1] == '{':
                out.append('{{'); i += 2; continue
            j, depth, cut, question = i + 1, 0, None, 0
            while True:
                d = s[j]
                if starts_string(s, j):
                    _, j = read_string(s, j); continue
                if d == "'":
                    j = s.index("'", j + (3 if s[j + 1] == '\\' else 2)) + 1; continue
                if d in '([{':
                    depth += 1
                elif d in ')]':
                    depth -= 1
                elif d == '}':
                    if depth == 0:
                        break
                    depth -= 1
                elif depth == 0 and cut is None:
                    if d == '?' and s[j + 1] not in '?.' and s[j - 1] != '?':
                        question += 1
                    elif d == ':':
                        if question:
                            question -= 1
                        else:
                            cut = j
                    elif d == ',':
                        cut = j
                j += 1
            out.append('{%d%s}' % (hole, s[cut:j] if cut else ''))
            hole += 1
            i = j + 1
            continue
        if interp and c == '}' and s[i + 1] == '}':
            out.append('}}'); i += 2; continue
        out.append(c); i += 1


def read_concat(s, i):
    """Literals joined by +; (None, i) if the argument is not only literals."""
    parts = []
    while True:
        while s[i].isspace():
            i += 1
        if not starts_string(s, i):
            return None, i
        t, i = read_string(s, i)
        parts.append(t)
        k = i
        while s[k].isspace():
            k += 1
        if s[k] == '+':
            k += 1
            while s[k].isspace():
                k += 1
            if starts_string(s, k):
                i = k; continue
            return None, i
        if s[k] in ',)':
            return ''.join(parts), k
        return None, i


def extract(paths):
    keys, dynamic = {}, []
    pat = re.compile(r'(?<![\w.])(?:pet\.|window\.)?(LF?)\(')
    for p in paths:
        s = Path(p).read_text(encoding='utf-8-sig')
        for m in pat.finditer(s):
            line = s.count('\n', 0, m.start()) + 1
            where = f'{Path(p).name}:{line}: ' + s[m.start():m.start() + 100].splitlines()[0]
            vi, i = read_concat(s, m.end())
            if vi is None or s[i] != ',':
                dynamic.append(where); continue
            en, i = read_concat(s, i + 1)
            if en is None or s[i] != ')':
                dynamic.append(where); continue
            keys.setdefault(en, vi)
    return keys, dynamic



def code_keys(root=ROOT):
    """English keys of literal L/LF calls in the app's C# sources, and the calls that are not literal."""
    files = sorted(p for p in (root / "src").rglob("*.cs") if p.name != "PetText.cs" and "obj" not in p.parts and "bin" not in p.parts)
    return extract(files)


DATA_FILES = ("battle.json", "coats.json", "accessories.json", "achievements.json")


def strip_comments(text):
    """battle.json allows // and /* */ comments and trailing commas (as the app reads it)."""
    out, i, n = [], 0, len(text)
    while i < n:
        c = text[i]
        if c == '"':
            j = i + 1
            while text[j] != '"':
                j += 2 if text[j] == '\\' else 1
            out.append(text[i:j + 1]); i = j + 1
        elif text.startswith('//', i):
            i = text.find('\n', i) if '\n' in text[i:] else n
        elif text.startswith('/*', i):
            i = text.index('*/', i) + 2
        else:
            out.append(c); i += 1
    return re.sub(r',(\s*[}\]])', r'\1', ''.join(out))


def data_keys(root=ROOT):
    """English text of the data files (nameEn/descEn/... and {"vi", "en"} names), with its Vietnamese."""
    keys = {}

    def walk(node):
        if isinstance(node, dict):
            if isinstance(node.get("en"), str) and isinstance(node.get("vi"), str):
                keys.setdefault(node["en"], node["vi"])
            for name, value in node.items():
                if isinstance(value, str) and name.endswith("En") and value:
                    keys.setdefault(value, node.get(name[:-2] + "Vi", ""))
                else:
                    walk(value)
        elif isinstance(node, list):
            for item in node:
                walk(item)

    for name in DATA_FILES:
        walk(json.loads(strip_comments((root / "src/DesktopPet/Assets" / name).read_text(encoding="utf-8-sig"))))
    return keys


def table(language, root=ROOT):
    data = json.loads((root / "src/DesktopPet/Assets/i18n" / f"{language}.json").read_text(encoding="utf-8"))
    return {k: v for k, v in data.items() if not k.startswith("_")}


HOLE = re.compile(r"\{(\d+)[^{}]*\}")


def holes(text):
    return {m.group(0) for m in HOLE.finditer(text.replace("{{", "").replace("}}", ""))}


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    language = sys.argv[1] if len(sys.argv) > 1 else "ja"
    keys, _ = code_keys()
    keys.update({k: v for k, v in data_keys().items() if k not in keys})
    translated = table(language)
    missing = [k for k in keys if k not in translated]
    for key in missing:
        print(json.dumps(key, ensure_ascii=False), "  # vi:", json.dumps(keys[key], ensure_ascii=False))
    print(f"{len(keys)} keys, {len(missing)} without {language} text")
