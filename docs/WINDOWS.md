# Windows application

## Launch from File Explorer

The published executable in this workspace is:

```text
C:\Project VS\Pet Computer\artifacts\win-x64\DesktopPet.exe
```

1. Paste `C:\Project VS\Pet Computer\artifacts\win-x64` into File Explorer's address bar.
2. Double-click **DesktopPet.exe**. No Visual Studio, terminal, .NET installation,
   or Python installation is needed to run the published copy.
3. Only the desktop cat and tray icon appear. The transparent, borderless,
   always-on-top WPF window and existing input/physics remain in use.
4. Right-click the cat and choose **Exit pet**, or choose **Exit** from its tray icon.
   Exiting also stops the owned Python process. Pause freezes rather than hides it.

Keep **the entire `win-x64` folder together**. It includes .NET libraries plus `ai`
and `python` subfolders. Copy or ZIP the whole folder to share with another Windows
10/11 x64 computer, and extract before launching. A shortcut to the `.exe` is fine;
moving only the `.exe` will lose its dependencies.

## Build

Launching the executable again opens the existing pet's controls instead of creating
another cat. The last position is saved on normal exit. Saved positions and favorite
spots follow monitor working areas, with a primary-screen fallback when disconnected.

The pet settings dialog includes a master sound switch, volume, separate interaction,
emotion and sleep sound switches, and nine preview buttons. Previews play even when
automatic sounds are muted; volume zero silences previews too. Automatic sound
cooldowns and the no-overlap rule remain in effect.

Sleeping updates at approximately 30 frames per second, active interaction at 60,
and pause at 10. Debug text refreshes four times per second. Reusable effect geometry
and a bounded set of cartoon audio variants are cached. Toy chasing leads the ball
slightly and uses a stopping zone to reduce rapid direction changes.

With the .NET 10 SDK on PATH:

```powershell
cd 'C:\Project VS\Pet Computer'
dotnet build .\src\DesktopPet\DesktopPet.csproj -c Release
```

This workspace has a local SDK, so without installing an SDK globally you can use:

```powershell
& .\.tools\dotnet\dotnet.exe build .\src\DesktopPet\DesktopPet.csproj -c Release
```

The ordinary development executable is at
`src\DesktopPet\bin\Release\net10.0-windows\DesktopPet.exe`. That build still needs
the .NET Desktop Runtime and a discoverable Python interpreter for AI. Use the
published copy for standalone use.

## Publish the complete release

From the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

The script uses the installed or project-local SDK. If needed, it downloads the
official Python 3.13.15 x64 embeddable package, checks its pinned SHA-256 hash, and
unpacks it into `.tools\python`. It publishes and checks the output dependencies.
Internet access is needed on the build computer for missing .NET packs or Python.

Once `.tools\python` is prepared, the equivalent command is:

```powershell
dotnet publish .\src\DesktopPet\DesktopPet.csproj -c Release -p:PublishProfile=Windows-x64
```

Or replace `dotnet` with `& .\.tools\dotnet\dotnet.exe` in this workspace.
Both commands output `artifacts\win-x64\DesktopPet.exe`. Close the published pet
before republishing.

The profile `src\DesktopPet\Properties\PublishProfiles\Windows-x64.pubxml` provides:

- Release, Windows GUI executable (`WinExe`), `win-x64`.
- Self-contained .NET and WPF runtimes; no recipient runtime installation.
- Folder publication with trimming disabled to preserve WPF behavior.
- Python interpreter, standard library, native DLLs, and license.
- AI service and personality configuration in the `ai` folder.
- Cat artwork compiled into the C# renderer; no external image assets.

The C# app locates the bundled `python\python.exe` relative to the executable,
independent of the working directory. JSON-lines IPC and local fallback are
preserved. An explicitly set `PET_PYTHON` still overrides interpreter selection.
Memory stays under `%LOCALAPPDATA%\DesktopPet`.

This profile is for Intel/AMD x64 Windows. A native ARM64 release would need both
an ARM64 publish profile and Python bundle. This is a portable folder, not an
installer or an automatic-startup registration.

References: [.NET deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/),
[Python embedding](https://docs.python.org/3.13/using/windows.html#the-embeddable-package).

## Sleeping and waking

Both inactivity Sleepy and autonomous sleep enter Sleeping with a reaction lock.
Closed eyes, breathing, and Zs continue without other activities. Cursor movement,
petting, AI input, and unrelated timers cannot wake the pet early.

```text
Sleeping -> configured sleep duration -> Waking -> Idle -> normal activities
Sleeping -> direct click -> Waking or Dragging -> normal interaction
```

Sleep lasts **10 seconds** by default. Edit `SleepDurationSeconds` in
`src/DesktopPet/PetModel.cs` (`PetSettings`) to adjust it, then rebuild/publish.
For tests, use `new PetSettings { SleepDurationSeconds = 2 }`.
This setting applies to inactivity, local random sleep, and Python SLEEP; the
protocol duration field remains accepted but does not override this sleep setting.

After expiry, a 1.2-second Waking animation leads to idle and normal behavior.
Waking resets inactivity and stale emotions. Right-click wakes immediately and
opens the menu; left-click wakes before pickup and dragging. Pause freezes the
sleep clock, and AI toggles cannot restart it or wake the pet.

**Away nap (the one exception).** When nobody has touched the mouse or keyboard
for 5 minutes, the pet goes to sleep and its sleep clock is held, so it keeps
sleeping while you are gone. Your next input anywhere on the computer wakes it
with the normal 1.2 s waking animation and the 30 s wake cooldown. A click still
wakes it at once. Ordinary sleep is unchanged: 10 s, and the cursor cannot wake it.

The vector-drawing approach is retained. `PetAction` includes Idle, Walking, Sitting,
Jumping, Sleeping, Thrown, and Waking, plus Looking and Dragging. `ActionTime` resets
on state changes for state-specific animation; the original global `AnimationTime`
remains available. No sprite-sheet/image-loading system was introduced.

## Picking up the pet

A quick left-click gives attention without picking up or throwing the cat. Hold
the button and move at least **6 DIPs** to begin dragging. Clicking a sleeping cat
wakes it immediately, before the movement threshold is reached. Releasing an
actual drag still uses the existing throw velocity and gravity.

Walking accelerates gently and brakes near screen edges before turning inward.
This applies only to grounded walking; thrown and jumping physics are unchanged.
Neutral idle occasionally includes small ear twitches and glances; sleep and
emotional reactions suppress this idle detail.
Ear twitches now reach 8–9 degrees, with uneven gaps between gestures. Glances
move the eyes about 4 DIPs sideways and slightly up/down, accompanied by a small
head shift/tilt. Each glance eases in, holds about 1.4 seconds, and eases back.

After waking, a **30-second cooldown** blocks autonomous sleep from both Python
and local random behavior. Pause freezes it; toggling AI does not reset it.
`WakeCooldownSeconds`, `WalkAcceleration`, and `DragThreshold` are adjustable in
`PetSettings` in `src/DesktopPet/PetModel.cs`.

Right-click the cat and choose **Behavior debug view** to inspect action, emotion,
sleep elapsed time, wake cooldown, input state, and Python energy. Energy is the
last AI report, not a live measurement; it shows unavailable before a report or
when AI is disabled. Choose the item again or close the debug window to hide it.
This optional window does not change the pet's ordinary transparent desktop host.

Grab with the left mouse button. The cat eases into a position hanging below the
cursor, with dangling paws and a gentle pendulum sway. Quick movement adds head
wobble, dizzy eyes, and orbiting stars; intensity grows with cursor speed. Stop
moving to let the swing and dizziness decay smoothly. Release to throw as before.
Idle, walking, sleeping, and throw physics remain separate from these visual effects.

The window now reserves its transparent drawing margin in every state, so pickup
does not resize or shift the backing surface. Idle-to-pickup pose changes are eased,
and the full rotated body stays inside screen bounds. At an edge, the pet may sit
slightly away from the cursor to keep its feet and effects visible.

Swing has a stronger response with a 26-degree safety cap. Dizziness holds for about
1.2 seconds after quick movement stops, then slowly fades while held. Activation
requires smoothed speed above 1,800 DIPs/second plus a short accumulation of movement
intensity. Normal dangling does not trigger it. A 1,200 DIPs/second exit threshold
provides hysteresis around the trigger. The orbit,
larger stars, and bold floating Zs have dark outlines with white outer borders for
contrast on light, dark, or colorful desktops. Release still clears pickup effects.

The WPF checks write a visual comparison to
`artifacts\visual-review\animation-contrast.png` for checking effect readability.

## Treats, toys, and preferences

Right-click the cat to access the new optional controls:

- **Give treat:** a 4.2-second fish-shaped treat sequence: reveal, notice/reach,
  chewing, shrinking bites and subtle consumption sparkles, then a happy reaction.
  Feeding has a 20-second cooldown. Each completed treat adds 6 energy (capped at 100)
  when Python receives the observation; cancelled meals receive no reward. The
  visual interaction also works offline. Eating suppresses incidental cursor
  surprise, idle/sleep triggers and autonomous movement until it finishes.
- **Play with ball:** spawns a ball on the current monitor. The cat
  chases and bats it using C# movement. Click its right side to push left, its left
  side to push right, or its center to bounce upward. A padded hit area accepts
  clicks just beside the ball. Off-center distance controls push strength, with
  small variation. Gravity, rebounds, rolling stripes and friction make it settle
  naturally; hover/contact halos and a paw swipe show targeting and batting.
  Contact has a cooldown so one hit does not reapply an impulse every frame. Play ends after
  45 seconds, when you interact with the cat, or when sleep/protected reactions
  take priority. Pause freezes play. No ball is left behind when the app exits.
- **Favorite resting spot:** remember the cat's current grounded position, go
  there, or forget it. The pet also occasionally returns while idle to sit there.
  It walks along the current monitor's floor and never teleports between monitors.
  A disconnected monitor's spot falls back to the primary screen. A spot on another
  connected monitor is used when the pet is on that monitor.
- **Name and personality:** save a name and choose Balanced, Playful, Curious,
  or Calm. Profiles affect AI weights/durations, offline choices, and the timing of
  toy observation/recovery. Core physics, sleep timing and emotion locks remain unchanged.
  **Behavior tuning...** opens eight sliders with numeric entry and descriptions.
  Save persists user overrides in preferences; Python applies them on its next observation.
  **Restore defaults from personality.json** followed by Save clears overrides and
  restarts enabled Python AI without resetting the current sleep/wake action.
- **Quiet mode:** disables autonomous walking, jumping and travel to the resting spot.
  Idle/sleep animations, clicking, dragging, and throws still work. Since 2026-10-15
  games you start yourself (ball, laser pointer, Do now) still run in Quiet mode;
  turning Quiet on stops a game in progress. A fullscreen app still blocks them.
  The cat still reacts with short lines and thought bubbles in Quiet mode (no idle
  chatter), and the diary stays up to date.
- **Pet sounds:** off by default. All nine cues use a consistent cartoon-cat
  treatment of the embedded recordings: short voiced excerpts, expressive pitch
  glides, rounded dynamics, soft fades, and small voice/volume variations. Meows
  bend downward, play cues bounce, surprise rises, annoyed cues gently descend,
  and purr/sleep cues stay softer. Every cue is under 1.2 seconds; cooldowns prevent
  overlap and frequent repetition. Disable to stop playback. Original recording
  provenance remains in `Assets/Sounds/CREDITS.md` in the published folder.

Preferences are stored in `%LOCALAPPDATA%\DesktopPet\preferences.json`. No extra
packages or sound downloads are required. Treat/toy commands never break a sleep
lock; the direct right-click wakes the pet first, and commands become available
after the Waking animation. The existing 10-second sleep and 30-second wake
cooldown remain unchanged.

## Application icon

The executable, WPF window, and notification tray share the custom cat-face icon
in `src/DesktopPet/Assets/Pet.ico`. It contains native 16, 20, 24, 32, 40, 48, 64,
128, and 256 pixel images. Windows shortcuts inherit the executable icon.
The pet retains its borderless background-window behavior.

The editable vector source is `src/DesktopPet/Assets/PetIcon.xaml`. To regenerate
the ICO and its 512-pixel preview before building or publishing:

```powershell
& .\.tools\dotnet\dotnet.exe run --project .\tools\IconBuilder -c Release
```

## Verify the application

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Ui
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-publish.ps1
```

Run on an interactive Windows desktop. The publish check briefly starts the actual
release from an unrelated working directory, excludes installed Python from PATH,
overrides .NET discovery, confirms bundled runtimes are used, then closes the pet
normally and checks Python cleanup.

## Play, self-care and contextual sounds

Ball play has Observe, Crouch, Chase and Recover phases. Curious observes longer,
Playful prepares sooner, and Calm rests longer after a hit. An unreachable ball
causes a pause after six seconds of chasing. Click/drag, protected emotions and
sleep cancel the sequence and clear its crouch pose; Pause freezes it.

Local idle decisions occasionally choose Grooming (3.6 seconds: lick paw, wash face)
or Stretching (2.4 seconds). A shared 45-second cooldown prevents repeated self-care.
Quiet mode suppresses these autonomous activities. No world position or throw
physics is changed by the new drawing poses.

The three extra previews are ball_tap (brief contact cue), satisfied (treat completion)
and wake (entering Waking). They use processed embedded CC0 recordings, not new
external assets. ball_tap and satisfied use Interaction sounds; wake uses Sleep sounds.
They respect master volume, pause, per-cue cooldowns and the existing no-overlap rule;
if busy, the cue is dropped rather than played late.

The publish smoke test uses a fresh `artifacts/smoke-data/<run-id>` data directory.
`PET_DATA_DIRECTORY` may specify an absolute path for isolated preferences and AI
memory; ordinary launches still use `%LOCALAPPDATA%/DesktopPet`. Tests do not replace
personal preferences or memory. Smoke data is retained for troubleshooting.

## Motion polish (2026-10-08)

Walking strides and bob now follow actual horizontal speed through PetMotion;
slower acceleration means smaller, slower steps. Direction requests preserve the
intended direction while existing WalkAcceleration brakes the old velocity and
accelerates the other way. Presentation follows actual travel before a short turn.
No throw speed, gravity, bounce coefficient or drag threshold changed.

Standing/sitting/sleeping/waking poses ease through a rest blend around the same
floor anchor. Head/tail lag is bounded and settles after stopping. PetMotion owns
only presentation history, freezes on pause and yields to the pickup pose.

Autonomous jumps enter PreparingJump for 0.16 seconds before applying the original
jump impulse. Sleep, Quiet mode and direct input can cancel preparation; a mouse
throw never waits for this stage. Flight uses a small stretch; floor contact reports
impact speed to a 0.22-second visual squash without changing collision physics.

To export regression frames for inspection, set PET_RENDER_REVIEW to an absolute
folder before running scripts/test.ps1 -Ui. This run's samples are in artifacts/motion-review.

## Smooth motion, logs, startup and version (2026-10-09)

**Frame pacing.** A `DispatcherTimer` set to 16 ms measured only ~37 FPS on the
development PC, alternating 16 ms and 31 ms gaps, because the default Windows
timer granularity is 15.6 ms. That unevenness showed as a stutter while the
window followed a thrown cat. The timer now runs at half the target period and
skips ticks that arrive early (measured ~58 FPS, p90 gap ~20 ms). Targets are
unchanged: ~60 FPS active, ~30 sleeping, ~10 paused.

**Presentation only.** The window position blends between the last two fixed
1/120 s physics states, so frames with one or two physics steps no longer jump
unevenly. Reset, drag, DPI changes and monitor recovery snap without blending.
The window moves with one `SetWindowPos` call per frame instead of separate Left
and Top updates. Physics, throw speed, gravity and bounce are unchanged. The debug
view shows the measured FPS and the largest frame gap in the last second.

**Logs.** Diagnostics (AI fallback reasons, Python stderr, sound errors and
unhandled exceptions) are written to `%LOCALAPPDATA%\DesktopPet\logs\desktoppet.log`.
The file is capped at about 1 MB plus one `desktoppet.old.log`. With
`PET_DATA_DIRECTORY` set, logs go into that folder instead.

**Start with Windows.** Off by default. The cat's right-click menu toggles a per-user
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry named `DesktopPet` for
the running executable. Use it from the published `win-x64` copy, not a dev build.
The menu reads the registry each time, so removing the entry from Task Manager's
Startup page is respected.

**Thought bubbles and needs.** With Python AI on, the cat occasionally shows a small
bubble above its head: fish (hungry, give a treat), ball (bored, play), moon (low
energy) or heart (affection). The behavior debug view lists hunger, boredom,
affection, bond and the current bubble. See [docs/AI.md](AI.md) for the rules.
After three minutes without attention the cat also looks briefly sad.

**Speech.** The cat says short lines in a bubble above its head: greetings,
reactions to petting, treats, ball play or throws, needs and occasional chatter.
In the cat's menu, choose **name and personality…** and use the **Speech** box:
- turn talking on or off
- choose Tiếng Việt or English
- choose how often: Rarely (events only), Sometimes or Often
- **Forget what the pet learned…**

Defaults are on, Vietnamese and Sometimes. Over days, the cat favors actions and
lines you react to warmly (petting, treats, play) and uses fewer of those followed
by rough throws. The debug view lists what it has learned. Wording lives in
`ai/lines.json`.

**Version.** The version lives in `DesktopPet.csproj` (`<Version>`). **About Desktop
Pet** in the cat's menu shows the version, data folder and AI status.
`scripts/publish.ps1` also writes `artifacts\DesktopPet-<version>-win-x64.zip`
containing the whole folder.

## Appearance, break reminders and computer context (2026-10-13)

**Appearance.** In **name and personality…**, the **Appearance** box offers six coats
(Orange, the original, plus Gray, Black, White, Calico and Tabby) and four accessories
(none, collar with bell, bow, party hat). Save applies them at once. Everything is
vector drawing (`PetAppearance.cs` palettes, `PetRenderer.cs`). Patches and stripes are
clipped inside the outline. The bow, hat and collar move with the head. The window
size is unchanged, and the default orange cat renders pixel for pixel as before.

**Break reminders.** Off by default. In the **Break reminders** box, choose every 30, 45
or 60 minutes. Only time you actually use the computer counts: being away 5 minutes
counts as a break and restarts the clock. When a reminder is due, the cat says a line
such as "Sen ơi, uống ngụm nước đi nào!" and stretches. Python words the line (category
`break` in `ai/lines.json`). With Python off, or if no line arrives within 15 s, C#
uses a built-in line. Reminders show even if chatter is off. They wait while the cat
sleeps, is dragged or airborne, and while a fullscreen app is open. The debug view shows
the active minutes.

**Computer context** (`PetWindow.Context.cs`, checked once per second). It reads only
the foreground window's rectangle, style and class and the time of the last input,
never screen content.
- **Fullscreen app on the cat's monitor** (game, video, slides: a captionless window
  covering the whole monitor): the cat behaves as in Quiet mode, with no speech or
  thought bubbles and no sounds (previews still play). It stays visible. The saved
  Quiet setting is not changed. The desktop, taskbar and maximized captioned windows
  do not count.
- **Away** (no input for 5 minutes): away nap, see *Sleeping and waking*.
- **Back:** the cat wakes up, and Python greets you with a `back` line. The short
  "missing you" sad episode is postponed so it does not clash with the welcome.

Pause freezes all of this, like the sleep clock. Preferences: `Coat`, `Accessory`,
`BreakReminders`, `BreakMinutes`.

## Gestures, pending lines and diary (2026-10-13)

**New gestures** (vector, stationary, a few seconds each):
- **Kneading** after sustained petting: sits, paws press in turn, eyes closed.
- **Rolling over** to show the belly: only with a strong bond, right after petting.
- **Chasing the tail**: spins in place.
- **Scratching**: stretches up and rakes the air, leaving short marks.
- **Sneezing**: a small puff.

Angry also flattens the ears and lashes the tail. Click or drag stops any gesture at once,
and sleep, Quiet mode and fullscreen apps behave as before (kneading still answers
petting in Quiet mode). All existing poses render pixel for pixel as before.

**No lost lines.** If a line arrives while the cat is eating, flying or just spoke, it
waits up to 10 s and then appears. Sleep drops it.

**Diary.** Right-click the cat and choose **Mochi's diary** (or **Nhật ký của Mochi**,
following the speech language). It shows days together, totals (petting, treats, ball
games, clicks, rough throws), the bond with a 14-day line chart, the hours you usually
play as a 24-bar chart (hover for values), and what the cat has learned. With the AI off,
it shows only this session's numbers.

## Do now menu (2026-10-13)

Right-click the cat → **Do now** / **Làm ngay** (label follows the app language).

**Actions:** Sit, Look around, Jump, Sleep, Wave a paw, Groom, Stretch, Knead, Roll over,
Chase tail, Scratch, Sneeze. They start at once and skip cooldowns, the bond condition,
chance and Quiet mode, then run their normal length and return to Idle. A newer request
replaces the running one. Ball play, a treat or a walk to the resting spot is cancelled.
- Never interrupts drag, flight, the sleep lock or waking. The right-click already wakes a
  sleeping cat, so a request made from that menu waits for the 1.2 s waking and then runs
  (a waiting request is dropped after 3 s). Click or drag still stops an action at once.
- **Sleep** skips the 30 s wake cooldown; after that the normal sleep rules apply
  (locked, wakes by itself after ~10 s, a click wakes it at once).
- Pause disables the menu. Python is not involved and these requests are not counted as
  interactions or used for learning.

**Control emotions myself** (session only, off at every start): turns off automatic
emotions and their cooldowns (petting → Happy, click bursts → Angry, fast cursor →
Surprised, sad episodes, Sleepy → automatic sleep, AI mood hints). Then choose Normal,
Happy, Angry, Sad or Surprised; the cat holds it until you choose another. Drag, eating,
flight and sleep still take priority, Dizzy on a hard drag still works, and the chosen
emotion does not stop the cat from walking. Python sees Neutral (so it does not learn from
or comment on a performed emotion). Turning the switch off clears the emotion and restarts
the "alone" clocks, so the cat is not instantly sad or sleepy. Dizzy and Sleepy are not in
the menu (Dizzy stays a drag-only reaction; Sleepy is the Sleep action).

## One app language (2026-10-13)

**Language / Ngôn ngữ** is the first setting in the customize window. English (default)
or Tiếng Việt applies to the whole app: the cat's and tray's right-click menus, the
customize and behavior-tuning windows, dialogs, About, the AI status, the diary, the Do now
menu, the learned likes/dislikes and what the cat says. Menus switch as soon as you Save;
an already open diary or tuning window switches when reopened. The behavior debug view
stays English (state names match code and logs). Stored values (personality, coat...)
did not change, so older preference files keep working; a file without a language, or with
an unknown one, now means English.

## Laser pointer (2026-10-15)

Right-click the cat → **Laser pointer** / **Chơi đèn laser**. A small red dot follows the
mouse cursor (click-through, never takes focus, so you keep working normally). The cat
watches it, chases it along the floor, crouches when the dot rests nearby and pounces with
the normal jump; a pounce that lands on the dot plays the ball-tap cue. A dot high above the
cat or on another monitor is only watched (the dot is hidden on other monitors). Pacing
follows the personality like ball play.
- Ends after 60 s, or when you click/right-click/drag the cat, throw it, it falls asleep,
  Pause, turning Quiet mode on, a fullscreen app, Do now, ball play or a treat. It can be
  started while Quiet mode is on.
- While it runs, the cursor is the toy: passing over the cat is not petting and fast moves
  near it do not startle it. Python only hears that play started (like the ball).
