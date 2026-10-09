# Real cat recordings

All recordings are from **Cat Purr & Meow** by **Kerzoven**, published April 16,
2022 on OpenGameArt:
https://opengameart.org/content/cat-purr-meow

License: **CC0 1.0 Universal** (public-domain dedication).
https://creativecommons.org/publicdomain/zero/1.0/
Legal text: https://creativecommons.org/publicdomain/zero/1.0/legalcode

Original files: `cat_softmew.wav`, `cat_mewfood.wav`, `cat_mewpurr.wav`,
`cat_mewpurr2.wav`, `cat_purractive_loop.wav`, `cat_purrsleepy_loop.wav`.

Changes: selected short purr excerpts; gentle high/low-pass and noise reduction;
volume matching, short fades, mono 22050 Hz PCM16 conversion. The stored source assets retain recorded pitch and timing. Runtime playback
selects short voiced excerpts and applies cartoon pitch glides, rounded dynamics,
volume variation, and fades to all six cue types. The author's page notes some background movement noise in
the meows. We do not imply the author endorses this application.

`sources.json` maps each bundled clip to its original download URL, source/output
SHA-256 hashes, and selected start/duration. `scripts/prepare-cat-audio.py` in the
source repository reproduces the preparation from downloaded originals using
the project's local ffmpeg. The application needs no downloads or audio codecs.

Interaction mapping: soft/meow-purr clips for attention, active purrs for treats
and petting, sleepy purrs for sleep, meow-purrs for play. Surprise and annoyance
use natural short/demanding meows, not hisses or aggressive growls. These are
application cue choices, not claims about the cat's emotion in the recording.
