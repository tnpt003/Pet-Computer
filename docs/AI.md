# Python AI layer

## Energy and activity variety

The optional observation `personality` selects Balanced, Playful, Curious, or Calm
weights. It does not override C# action locks. `activity.treats` is a cumulative
count paired with `session_id`; Python awards 6 energy per new treat and stores
the credited session/count to avoid repeat rewards after a service restart.
The standard-library service remains compatible with observations omitting these
fields. Toy physics, treat animations, resting-spot navigation, sounds, and quiet
mode stay in C#. Quiet mode rejects autonomous locomotion even if AI suggests it.

Python remembers four recent autonomous recommendations. It excludes the previous
action family from its next random choice and reduces the weight of other recent
families. Walking left/right/following count as one family; jumping and attention
seeking count as another. Safety responses (such as sitting while angry) take
priority over variety.

Energy ranges from 0 to 100 (initially 80). C# sends optional cumulative `activity`
seconds for walking, jumping (`playing`), sleeping, and resting. Python updates
energy from differences between these observations, not from commands it proposes.
Paused time and user dragging/throwing do not consume energy. A fresh Python process
establishes a new baseline, avoiding double-counting previous activity.

Low energy favors resting/sleeping and reduces active choices. Sleep restores
energy; Python avoids recommending another sleep for 30 seconds after observing
sleep recovery. C# also enforces a configurable 30-second wake cooldown for local
random sleep and Python sleep commands; pausing freezes the cooldown.
The energy and recent recommendations persist in the existing AI memory file.

Tune `walking_energy_per_second`, `playing_energy_per_second`,
`sleep_energy_per_second`, and `rest_energy_per_second` in `ai/personality.json`.
Defaults are 0.5, 1.5, 5, and 0.15 respectively. Restart the AI after editing;
republish to update the standalone copy. No new dependency or protocol version is
required; older observations without activity counters still work.

These preferences never change physics, input, or emotion priorities. The
configured 10-second sleep, sleep reaction lock, automatic waking, click-to-wake,
and existing offline C# fallback remain in charge. A suggestion may be rejected
by C# if input or a protected state takes priority.

## Design and inspected engine

The original app remains C# / WPF. `PetWindow` owns the transparent topmost window,
mouse capture, global cursor sampling, monitor selection, and update loop.
`PetRenderer` provides procedural poses. `PetPhysics` owns velocity, gravity,
bounces, and settling. `ThrowTracker` owns release velocity. These last three
components are unchanged. The original random decisions in `PetBehavior` remain
available at all times.

```text
WPF: window / renderer / mouse / fixed-step physics
                    |
              PetBehavior (C# command execution + local fallback)
                    |
              IPetDecisionSource
                    |
         PythonDecisionSource (child lifecycle + JSON-lines pipes)
                    |
        Python Brain (personality / mood / memory / decisions)
```

The connection uses UTF-8 **newline-delimited JSON over redirected stdin/stdout**.
There are no network ports, HTTP server dependencies, firewall rules, or separate
service launch steps. C# starts Python without a console window, serializes one
request at a time, reads stdout asynchronously, and drains stderr separately.
All IPC is off the WPF dispatcher; model changes happen only back on that dispatcher.

The original local behavior picks an action immediately when its timer expires.
It also requests an AI decision. A valid, timely AI response can replace that
local action and set its duration. If the local action has already jumped, that
response is ignored until the next decision opportunity. Input and physics win.
This keeps the cat responsive while Python starts, hangs, crashes, or is absent.

## Files and responsibilities

| File | Responsibility |
|---|---|
| `ai/service.py` | Standard-library Python service and replaceable `Brain.decide` policy |
| `ai/personality.json` | Playfulness, curiosity, sleepiness, attention interval |
| `src/DesktopPet/AI/AiProtocol.cs` | Transport interface, observation/decision records, response validation |
| `src/DesktopPet/AI/PythonDecisionSource.cs` | Python discovery, process ownership, async IPC, timeout/restart |
| `src/DesktopPet/AI/DecisionGuard.cs` | Reject stale replies and preserve input/physics priority |
| `src/DesktopPet/PetBehavior.cs` | Execute high-level commands using existing actions and movement settings |
| `tests/ai/` | Python unit tests and subprocess fault injection |
| `tests/DesktopPet.Tests/AiTests.cs` | Behavior, protocol, fallback, and real-process integration tests |

## Start the pet on this computer

From PowerShell:

```powershell
cd 'C:\Project VS\Pet Computer'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run.ps1
```

The script detects the project-local .NET SDK and Python runtime under `.tools`
that were installed during development. C# starts the AI process automatically
when the first decision is due (about two seconds after startup).

Right-click the notification-area/tray icon to see `Python AI: connected (...)`
or a local-fallback reason. The status is refreshed when you open that menu.
Use **Enable Python AI** to stop/restart the AI independently of the pet.
No LLM, microphone, camera, screen capture, or external AI service is used.

To explicitly start with the original local random behavior:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run.ps1 -NoAi
```

## Stop the pet from appearing

**Right-click the cat and select `Exit pet`.** Alternatively, right-click its
system-tray icon (possibly inside the hidden-icons arrow) and select **Exit**.
This closes the desktop pet, stops its owned Python child, and removes the tray icon.
Closing the PowerShell window used to launch it does not stop the background app.
Pause only freezes the pet; it does not hide or exit it. Turning off Python AI
keeps the cat visible and switches to local behavior.

If an app instance becomes unresponsive, use Task Manager to end **DesktopPet**.
The current Python service also exits when its parent closes the stdin pipe.
Do not end unrelated Python processes.

## Setup on another Windows computer

1. Install the .NET 10 SDK for development (as described in the main README).
2. Install Python 3.10 or newer from <https://www.python.org/downloads/windows/>.
   No `pip install` is required. A recent maintained Python release is preferable.
3. Run `py -3 --version` or `python --version` to verify an actual interpreter.
4. Run `scripts/run.ps1`. For a custom interpreter, set the executable path first:

```powershell
$env:PET_PYTHON = 'C:\Path\To\Python\python.exe'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run.ps1
```

`PET_PYTHON` contains only an executable path, not a command plus arguments. Paths
with spaces are supported. If unset, the bridge tries `python\python.exe` next
to the application, then `py -3`, then `python`. The run script also discovers
`.tools\python\python.exe` for this development workspace. A failed interpreter
that actually starts but exits is treated as a service failure; use `PET_PYTHON`
to select the correct interpreter if the launcher or PATH is misconfigured.

Directly double-clicking the built `.exe` does not run the PowerShell discovery
step. It uses the bridge's bundled/interpreter/PATH discovery described above.
The app still runs with local behavior if no Python interpreter can be found.

## Run Python by itself

```powershell
py -3 -u .\ai\service.py
```

Or use `& .\.tools\python\python.exe -u .\ai\service.py` in this workspace.
The service waits silently for JSON lines. Paste this as **one line**:

```json
{"version":1,"id":1,"type":"observe","observation":{"state":"Idle","grounded":true,"seconds_since_interaction":301,"interaction_count":0,"last_event":"started","last_applied_command":"LOCAL"}}
```

It returns a decision such as:

```json
{"version":1,"id":1,"type":"decision","command":"SEEK_ATTENTION","duration_seconds":3.5,"mood":"bored"}
```

Duration varies. Press Ctrl+C to stop this standalone test. A manually started
service is not attached to the desktop app: the app owns its own private child.

## Protocol v1

Each message is a single JSON object followed by a newline, at most 8,192
characters. stdout must contain **only protocol responses**; debug output belongs
on stderr. There is one in-flight request, matched using `version` and `id`.

An `observe` request contains:

| Observation field | Meaning |
|---|---|
| `state` | Existing C# action name, e.g. `Idle`, `Walking`, `Thrown` |
| `grounded` | Whether C# physics considers the pet on the floor |
| `seconds_since_interaction` | Time since the user last interacted with the pet |
| `interaction_count` | Monotonic count during this app session |
| `last_event` | Latest pet event: reset, drag_started, released, drag_cancelled, paused, resumed |
| `last_applied_command` | Last accepted AI command, or `LOCAL` |
| `emotion` | Current visible/reaction emotion, such as `Happy`, `Angry`, or `Neutral` |
| `local_hour` | Optional computer-local time as fractional hours (0–24), e.g. `23.5` |

`local_hour` gives Python a gentle day rhythm: at night (22:00–06:00) active
commands are weighted ×0.6, IDLE/SIT ×1.2 and SLEEP ×1.8; in the morning
(07:00–10:00) active commands ×1.2 and SLEEP ×0.7. It only biases choices;
activity is never forbidden, and C# still owns sleep length, the sleep lock and
the wake cooldown. A missing or invalid value leaves the weights unchanged.
Python's `BUSY_STATES` must match `PetActionRules.RejectsDecisions` in C#; a C#
check fails if the two lists drift apart.

These are periodic snapshots with an event summary, not a lossless event queue.
Interactions occurring between requests are reflected in the counter. The idle
timer measures interactions **with this pet**, not all computer keyboard/mouse
activity. The first observation establishes a counter baseline after each Python
restart; memory counts subsequent observed interactions rather than a perfect
historical total.

A `decision` response contains `command`, `duration_seconds` (0.5–12), and `mood`
(`content`, `playful`, `bored`, or `sleepy`). C# validates all of them before use.
An optional `emotion` hint (`neutral`, `sleepy`, `sad`) is based on attention history.
Replies without it remain valid. Immediate input reactions and their cooldowns live
in C#, with local fallback if Python is offline. See [the emotion guide](EMOTIONS.md).
Sleep remains locked against background reactions until the configured
`PetSettings.SleepDurationSeconds` expires (default 10 seconds), or the pet is
clicked directly. The SLEEP duration field remains accepted for compatibility;
the C# setting owns sleep timing. Automatic wake resets inactivity and returns
through Waking to Idle. Clicking clears the lock before normal Waking or pickup.

| Command | Execution in C# |
|---|---|
| `IDLE` | Stop horizontal movement; existing idle animation |
| `LOOK` | Grounded looking animation; Curious tends to observe longer |
| `WALK_LEFT` / `WALK_RIGHT` | Set facing and existing walk speed |
| `SIT` | Existing sitting pose |
| `JUMP` | PreparingJump for 0.16 seconds, then the unchanged jump impulse |
| `SLEEP` | Existing sleep animation |
| `FOLLOW_MOUSE` | Walk toward the cursor horizontally, stop within 18 DIPs; screen bounds still apply |
| `SEEK_ATTENTION` | Look and raise a paw for 2.4 seconds; 45-second C# cooldown |
| `ROLL_OVER` | Roll onto the back for 3.5 s (C# duration); refused in Quiet mode |
| `CHASE_TAIL` | Spin in place after the tail for 3 s; refused in Quiet mode |
| `SCRATCH` | Stretch up and scratch for 2.4 s; refused in Quiet mode |

Duration is a grounded behavior timer, as in the original engine; time in the air
does not forcibly terminate a jump. Python never sends position, velocity, gravity,
window commands, code to execute, or animation frames.

## Reliability and priority

- Missing interpreter/script, EOF, invalid JSON, wrong ID/version, invalid values,
  oversized replies, and pipe failures return `null` so local behavior continues.
- Requests have a **2-second I/O deadline**, including the first service reply.
  A hung child is terminated and replaced on a later decision attempt. Operating
  system process creation itself is not cancellable, but runs off the UI thread.
- Retry delays grow **2, 4, 8, 16, 30 seconds** and reset after success. Retries
  happen at eligible behavior decisions, not at frame rate. There is no busy loop.
- A user interaction increments a revision. Replies from earlier revisions are
  discarded even if the drag/throw has already finished by the time they arrive.
- AI cannot override dragging, airborne motion, jumping, throwing, or pause.
- Disabling AI invalidates pending responses and stops the owned child. Local
  action selection continues; any current airborne physics completes normally.
- Shutdown closes stdin and gives Python up to 250 ms to flush memory, then kills
  the owned process tree if necessary. stderr is drained concurrently to avoid
  pipe deadlocks. Diagnostics go to .NET Trace; current status is available in the tray.
- There is no independent heartbeat. A crash is discovered at the next decision,
  so the tray's last status can remain visible until that request.

## Personality, mood, and memory

Edit `ai/personality.json` and rebuild/restart (or toggle AI off/on when editing
the copied output file). Playfulness weights jumps, curiosity weights following,
and sleepiness weights sleep. Defaults favor walking. After five minutes without
pet interaction, it seeks attention at most once a minute. Repeated commands get
a lower weight.

Memory is stored at `%LOCALAPPDATA%\DesktopPet\ai-memory.json`. It retains the last
recommended command, mood, decision count, and observed interaction count. Writes
are atomic, batched at most every 30 seconds, and flushed on graceful EOF. A crash
can lose the last batch. Missing, corrupt, or unwritable memory does not prevent
decisions. Standalone tests can use `--memory PATH` and `--personality PATH`.
Multiple separately launched pets currently share this simple file (last writer
wins); per-pet IDs/storage are a future extension.

## Packaging

Use `scripts/publish.ps1` for a complete self-contained Windows release. It bundles
both .NET and Python automatically. Double-click `artifacts\win-x64\DesktopPet.exe`
and keep the complete folder together. See [the Windows guide](WINDOWS.md).

The project copies `ai/service.py` and `ai/personality.json` into the build and
publish output. Even a .NET single-file publish needs this `ai` folder alongside
the executable, so distribute the whole output directory.

For a recipient without Python, include the official Windows embeddable Python
distribution in an adjacent `python` directory (with `python.exe`, standard-library
ZIP, DLLs, and license). Its architecture must match the target Windows computer.
No third-party Python packages are needed. Do not copy only `python.exe`.

Official embedding documentation:
<https://docs.python.org/3.13/using/windows.html#the-embeddable-package>

## Tests and future features

Run `scripts/test.ps1` for the original physics checks, command/priority checks,
Python unit tests, and subprocess integration tests. Without Python it reports
the skipped integration tests. `PET_TEST_PYTHON` selects an interpreter when
running the C# test project directly.

To also run the automated WPF checks on an interactive Windows desktop:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Ui
```

This briefly shows a test pet window and checks mouse capture/release, cursor-following
updates, delayed-reply rejection during drag, movement, pause/resume, rendered
animation frames, transparent pixels, and cleanup. It does not move the system
cursor or synthesize global mouse clicks. Mouse capture requires an interactive
desktop and can fail in a restricted sandbox/headless session.

For UI regression checks, launch the pet and verify transparent rendering,
idle/walk/sleep animations, dragging, throws, boundaries, and tray exit with AI on
and off. During a drag, no response should make the cat jump away from the cursor.
Stop just the child Python process in Task Manager; the cat should keep moving
locally and reconnect at a later decision. The automated subprocess tests also
exercise crashes, timeouts, malformed/oversized output, stderr flooding, and recovery.

Future LLM integration belongs behind `Brain.decide`, with its own bounded timeout
and safe command validation. Voice or vision should become Python observations
or policy inputs. To change IPC, implement `IPetDecisionSource` without touching
the renderer or physics. Long-running LLM jobs should be queued/cached in Python
so this small decision endpoint can keep answering within its deadline.

## Interaction polish (2026-10-07)

Offline C# choices exclude the last local activity and downweight the previous four.
This history survives interaction resets. Python continues to own high-level policy.
SEEK_ATTENTION now executes a grounded look/head-tilt/paw gesture for 2.4 seconds,
with a 45-second C# cooldown; another AI reply cannot interrupt that gesture.
Direct input, sleep and emotion locks still take priority. Its time counts as resting.
Gentle petting adds a directional head lean to the Happy expression; pickup clears it.
Emotion audio is attempted once on transition. A muted or blocked cue is discarded,
not retried later. Cached sound variants explicitly alternate source excerpts and
use bounded RMS gain before soft limiting, retaining cartoon pitch and envelopes.
No new audio dependency or downloaded asset was introduced.

## Needs, bond and thought bubbles (2026-10-09)

Python keeps four 0–100 values in `ai-memory.json`. Existing memory files gain
these keys with defaults. Energy and history are preserved.

| Need | Rises | Falls | Effect |
|---|---|---|---|
| hunger (start 30) | `hunger_per_hour` (25) while the app runs | treat −35 | ≥70 and idle ≥45 s: SEEK_ATTENTION + `hungry` |
| boredom (start 20) | `boredom_per_minute` (2; Playful ×1.4, Calm ×0.6) | petting −8, click −3, ball −15/−4/−3, jumping | more JUMP/FOLLOW_MOUSE, fewer IDLE/SIT; ≥75: asks with `bored` |
| affection (start 50) | petting +6, treat +5, click +1.5 | `affection_decay_per_hour` (10), rough throw −4 | more FOLLOW_MOUSE; fresh petting at ≥60: `love` |
| bond (start 30) | petting/treats/ball, slowly | rough throw −0.6 | more FOLLOW_MOUSE; ≥50 greets after ≥1 h away |

Bond changes are capped each local day: +3 at most and −2 at most, so it always
recovers. Time is counted at no more than 30 s per observation. Pauses, suspends
and closed-app time therefore never starve the pet. The three rates are optional
keys in `ai/personality.json`. They are not exposed in the tuning editor.

The optional observation field `interactions` carries cumulative per-session counts:
`petted`, `clicked`, `treats`, `toy`, `ball_clicks`, `ball_hits`, `rough_throws`.
A rough throw uses the existing ≥1600 DIPs/s release. Python credits only increases
within the same `session_id`, so repeats and restarts are never double counted.
Python also keeps a decayed 24-hour histogram of interactions. At the user's usual
play hour (enough history required), active choices get ×1.15.

Decisions may add `needs` (for the debug view) and `thought`
(`hungry`, `bored`, `sleepy`, `love`). C# drops an invalid thought or needs
object; it does not reject the whole decision. Ambient bubbles are at least 90 s
apart in Python. Bubbles attached to SEEK_ATTENTION follow the attention cooldown.
C# shows a bubble for 2.5 s with a 20 s per-kind cooldown and an 8 s gap between
bubbles. It only shows one when the pet is grounded and not asleep, waking,
dragged, airborne, preparing to jump, eating or Angry/Surprised/Dizzy/Sleepy.
Bubbles are drawing only. With Python off there are no needs or bubbles, and
local behavior is unchanged.

## Speech bubbles and learning from reactions (2026-10-10)

**Lines.** `ai/lines.json` holds short lines per situation in Vietnamese (`vi`) and
English (`en`). The pet refers to itself by `{name}` and calls the user "sen"
("human" in English). Situations: hello, welcome_back, petted, treat_thanks, play,
thrown_rough, thrown_fun, back, break, wake_up, sleepy, lonely, hungry, bored,
morning, night and idle. A line can be limited to personalities with `{"text": ..., "personality": [...]}`.
Edit the file to change wording; restart the AI, or publish again for the standalone copy.
`ai/speech.py` picks lines without repeating the last 20. Line IDs come from the
text, so reordering keeps learned scores.

**When it talks.** Order of priority:
1. Events: rough or fun throw, treat, petting, ball, waking or greeting. An event
   that happens while the pet is airborne is kept for up to 12 s until the pet can talk.
2. Needs, alongside the thought: at least 300, 120 or 60 s apart for chattiness 0, 1 or 2.
3. Loneliness during the short Sad episode, at least 60 s apart.
4. Chatter: none at chattiness 0, ≥90 s with a 25% chance at 1, and ≥35 s with a
   50% chance at 2. There is no chatter in Quiet mode.

Python stays silent unless the observation says `speech_enabled: true`.

**Protocol.** These are optional, additive observation fields:

| Field | Meaning |
|---|---|
| `speech_enabled` | Whether the pet may talk |
| `language` | `vi` or `en` |
| `chattiness` | 0, 1 or 2 |
| `pet_name` | The pet's name, inserted for `{name}` |
| `quiet` | Quiet mode is on |
| `last_speech` | `{id, age}` of the last line C# actually displayed |
| `learning_reset` | A new token asks Python to forget learned scores |

A decision may add `speech` (`{id, text}`) and `learned` (likes/dislikes summaries).
C# strips control characters and ignores invalid values without rejecting the
decision. Speech is presentation only, so C# may show it even when it drops the
command because the user interacted meanwhile. The line must arrive within 2.5 s.

**Learning (`ai/learning.py`).** The context combines part of day with the main
need, such as `evening/bored`. In the 15 s after an action the AI suggested and
C# applied, or after a displayed line, each reaction scores as follows:

| Reaction | Score |
|---|---|
| Petting or treat | +1 |
| Ball | +0.5 |
| Click | +0.3 |
| Rough throw | −1 |
| Clicking until the pet is Angry | −0.5 |

Points are clamped to ±1 per observation, and no reaction teaches nothing. Each
score moves toward the reward (learning rate 0.25) and decays with a 7-day half-life.
It becomes a weight multiplier from ×0.5 to ×2. Variety rules still apply, so the
pet never repeats one choice endlessly. At most 500 scores are kept, stored as
`learned` in `ai-memory.json`. **Forget what the pet learned…** in the settings
sends a new `learning_reset` token. Python clears its scores once; needs and bond stay.

**Display (C#).** `PetSpeech` decides timing: 2.5–6 s depending on length, at least
12 s between lines. It shows nothing while asleep, waking, dragged, airborne,
preparing to jump or eating; a reaction may appear while the pet is upset. A
speech line replaces the icon thought. The bubble is a separate small topmost window
(`PetWindow.Speech.cs`). It is click-through and never takes focus. It sits over
the head and moves with the interpolated pet position. It stays inside the work
area, and the pet window keeps its fixed size. A meow cue plays if sounds are on,
under the existing cooldowns. With Python off, the pet does not talk.

## Break reminders and computer context (2026-10-13)

C# owns the clocks and window checks. Python only picks the words.

| Observation field | Meaning |
|---|---|
| `break_due` | A break reminder is due and the pet can show it now |
| `context` | `{fullscreen, away, returned}`. `returned` is true once, on the first observation after the user came back |

- `break_due: true` → a `break` line comes first, even if `speech_enabled` is false (the
  user enabled reminders separately), at least 6 s after the previous line.
- `context.fullscreen: true` → no line at all, and a pending event is dropped. C# also
  sends `quiet: true`, and the pet shows no bubbles while fullscreen.
- `context.returned: true` → a `back` event, after direct reactions (throw, treat,
  petting, ball) and before `wake_up` or the greeting.

C# accepts a line whose ID starts with `break:` only while the reminder is due.
After showing it, C# resets the break clock and starts a stretch if the pet is free.
If Python is off, or no line arrives within 15 s, C# shows a built-in line (`break:local:…`).
The away nap (`PetBehavior.StartAwayNap`/`EndAwayNap`) is decided entirely in C#.
Python sees only `Sleeping` and makes no requests while the pet sleeps.

## Gestures, pending lines and the diary (2026-10-13)

**Gestures.** Five short, stationary poses: Kneading, RollingOver, ChasingTail, Scratching
and Sneezing. Each owns a fixed C# duration (3.2 / 3.5 / 3 / 2.4 / 1 s). They reject AI
commands, click or drag cancels them at once, and they never run during sleep. They are
listed in `BUSY_STATES` and in `PetActionRules`, and a test keeps the two lists in sync.
- Kneading is a C# reaction: two strokes within 10 s start it (20 s cooldown). It also
  works in Quiet mode and without Python.
- Python sends `ROLL_OVER` right after petting when bond ≥ 60 and affection ≥ 60, at most
  every 120 s. `CHASE_TAIL` is a weighted choice that grows with boredom and energy;
  `SCRATCH` is a small constant weight. Learning applies to both like any other action.
- The local fallback can also choose tail chasing, scratching and sneezing. These share
  the 45 s cooldown with grooming and stretching.
- Angry now also flattens the ears and lashes the tail. This changes the drawing only.

**Pending lines.** A line that arrives while the pet cannot talk (eating, airborne, inside
the 12 s gap) waits up to 10 s and appears as soon as the pet is free. A newer line
replaces it. Sleep, a fullscreen app or turning speech off drops it.
Exception: the once-per-session greeting (`hello`, `welcome_back`) survives sleep and
waits up to 30 s. It stays hidden while the pet sleeps or wakes, so it never wakes the
pet, and it is said once the pet is awake. A `wake_up` line does not replace it; newer
reaction lines still do. A longer nap (for example an away nap) lets it expire.
`last_speech` reports only lines that were actually shown.

**Diary.** Python keeps `first_seen`, lifetime `totals` per interaction kind, and
`bond_history` (one value per local day, last 30 days) in `ai-memory.json`. Older files
load with defaults. Each decision may carry:

```json
"diary": {"days": 12, "since": "2026-10-01", "petted": 40, "treats": 9, "play": 7,
          "rough_throws": 1, "clicks": 30, "bond": 61.5, "bond_history": [50, 55.5, 61.5],
          "habits": [24 numbers]}
```

`bond_history` sends the last 14 days. An invalid diary is ignored without rejecting the
decision. The C# diary window (`PetWindow.Diary.cs`) only displays this summary.

## Short sad episodes (C#, works offline)

`PetEmotions` keeps a user clock that the pet's own sleep/wake reset does not
clear. After 180 s without a touch (click, petting, treat, ball, throw), the pet
looks sad for about 6 s and then returns to normal. Episodes are at least 180 s
apart. An episode waits for a free moment: never during sleep, drag, flight or
eating, and never over another reaction. Any touch ends it at once. The 2-minute
inactivity sleep is unchanged.

## Personality editor and pacing

The observation accepts optional `tuning` with the eight snake_case keys supported
by `ai/personality.json`: playfulness, curiosity, sleepiness, attention_after_seconds,
walking_energy_per_second, playing_energy_per_second, sleep_energy_per_second,
rest_energy_per_second. Missing or invalid values use validated file defaults.
Overrides apply per observation and never become sticky after a missing/null field.
Saved user values live in preferences, leaving the packaged JSON untouched. Numeric
entry rejects nonfinite/out-of-range values. Cancel/close discards an unsaved draft.
Restore defaults then Save clears overrides and reloads an enabled Python service.
When editing JSON directly, restart AI to reload it; GUI overrides take precedence.
Publish again to copy edits from the source JSON into the standalone application.

Curious observes/follows longer and can choose LOOK. Calm rests longer and waits
1.8 times the attention delay, with a 100-second attention cooldown. Playful uses
shorter walk bouts and 0.8 times the attention delay. Actual input, sleep restrictions,
C# attention cooldown and physics always win. Grooming/Stretching are local C#
self-care actions; they and SeekingAttention reject incoming decisions/hints until
finished or interrupted by input/protected states. These actions count as resting.

PreparingJump is a protected C# action: Python treats it as busy, and delayed AI
commands/hints cannot replace it. Time spent preparing counts as resting, while
actual airborne Jumping keeps its previous activity accounting. Input, sleep and
Quiet mode may cancel preparation. PetMotion.cs contains no AI policy or physics.
