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

The **Sounds** tab of the settings window has a master sound switch, separate
interaction, emotion and sleep sound switches (greyed out while sounds are off), volume
with a percentage, and a preview picker with nine cues and a Play button. Previews play even when
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

Right-click the cat and choose **More → Behavior debug view** to inspect action, emotion,
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
The smoke test also sets `PET_CONTEXT_PROBE=off`, so a fullscreen app or an idle
machine during the run never quiets the pet or starts an away nap.

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
throw never waits for this stage. Flight uses a small stretch. Landings (floor, window
edge, slime cushion) share one rule with the keepsake (PetImpact.cs): the same damped
squash/tilt springs (a squash that grows with the fall, rebounds into a little stretch and
wobbles out in ~0.4 s; the cat shows 0.75× of the squash and at most 12° of tilt), the
same dust (from 120 DIPs/s, anchored where the feet touched down) and the same cartoon
"squish" cue (from 300 DIPs/s, once per 0.4 s so a bounce series sounds once). Bouncing off
a screen side mid-air squeezes and tips the cat. The cat also reacts to the jolt (full at
1200 DIPs/s, fading in ~0.45 s): ears flatten, front paws splay and the tail flicks up; from
1000 DIPs/s (a fall of ~330 DIPs) its eyes squeeze shut "> <" for 0.3 s. This is not the
Dizzy emotion. A sleeping cat lands softly: no squash, dust, sound or reaction. None of it changes collision physics.

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

**Start with Windows.** Off by default. **Settings… → General** toggles a per-user
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry named `DesktopPet` for
the running executable (written on Save, only if it changed). Use it from the published
`win-x64` copy, not a dev build. The settings window reads the registry each time it
opens, so removing the entry from Task Manager's Startup page is respected.

**Thought bubbles and needs.** With Python AI on, the cat occasionally shows a small
bubble above its head: fish (hungry, give a treat), ball (bored, play), moon (low
energy) or heart (affection). The behavior debug view lists hunger, boredom,
affection, bond and the current bubble. See [docs/AI.md](AI.md) for the rules.
After three minutes without attention the cat also looks briefly sad.

**Speech.** The cat says short lines in a bubble above its head: greetings,
reactions to petting, treats, ball play or throws, needs and occasional chatter.
In the cat's menu, choose **Settings…** and use the **Speech** tab:
- turn talking on or off
- choose Tiếng Việt or English
- choose how often: Rarely (events only), Sometimes or Often
- **Forget what the pet learned…**

Defaults are on, Vietnamese and Sometimes. Over days, the cat favors actions and
lines you react to warmly (petting, treats, play) and uses fewer of those followed
by rough throws. The debug view lists what it has learned. Wording lives in
`ai/lines.json`.

**Version.** The version lives in `DesktopPet.csproj` (`<Version>`). **More → About Desktop
Pet** in the cat's menu shows the version with the build's commit, data folder and AI status;
the log's "started" line names it too, e.g. `1.0.0 (18cb227)` or `1.0.0 (18cb227, uncommitted
changes)`. The SDK puts the commit into the informational version (`1.0.0+<commit>`);
`scripts/publish.ps1` adds `.dirty` when the working tree has uncommitted changes. It also writes
`artifacts\DesktopPet-<version>-<commit>-win-x64.zip` (plus `-dirty-<time>` for an uncommitted
build) containing the whole folder, so packages of different sources never replace each other.
The version number itself, a changelog and tags are still the user's call when preparing a release.

## Appearance, break reminders and computer context (2026-10-13)

**Appearance.** In **Settings… → Pet**, the coat and accessory pickers offer ten coats
(Orange, the original, plus Gray, Black, White, Calico, Tabby, Ginger tabby, Tuxedo with
white chest and mittens, Russian blue, and Siamese with dark ears, mask, tail and paws)
and twelve accessories (none, collar with bell, bow, party hat, scarf, round glasses,
flower, crown, and the four holiday ones below). Each coat in the list has a colour dot. A preview next to the pickers
shows the choice at once with a gentle idle animation (only while the Pet tab is open);
the pet on screen changes on Save. Everything is
vector drawing: coat colors and names in `Assets/coats.json` (read by `PetCoats.cs`/`PetAppearance.cs`),
accessories as vector shapes in `Assets/accessories.json` (read by `PetAccessories.cs`, drawn by
`PetRenderer.DrawAccessory`); both are embedded at build time. A broken `coats.json` leaves only the
orange cat, a broken `accessories.json` leaves no accessories; both are logged. Since 2026-10-16 there
are four holiday accessories too (Santa hat, witch hat, apricot blossom, lantern). With **Dress up for
holidays** (on by default, under the accessory picker) the pet wears the matching one on Christmas,
Halloween, Tết and Mid-Autumn, and the party hat on the user's birthday; the saved accessory is never
changed, the settings preview shows the user's own choice, and without the AI there is no outfit. Patches and stripes are
clipped inside the outline. The bow, hat and collar move with the head. The window
size is unchanged, and the default orange cat renders pixel for pixel as before.

**Break reminders.** Off by default. In **Settings… → General**, choose every 30, 45
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
`BreakReminders`, `BreakMinutes`, `Birthday` (`MM-dd` or empty), `HolidayAccessories`.

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

Right-click the cat → **Emotions** / **Cảm xúc** → **Control emotions myself** (session only, off at every start): turns off automatic
emotions and their cooldowns (petting → Happy, click bursts → Angry, fast cursor →
Surprised, sad episodes, Sleepy → automatic sleep, AI mood hints). Then choose Normal,
Happy, Angry, Sad or Surprised; the cat holds it until you choose another. Drag, eating,
flight and sleep still take priority, Dizzy on a hard drag still works, and the chosen
emotion does not stop the cat from walking. Python sees Neutral (so it does not learn from
or comment on a performed emotion). Turning the switch off clears the emotion and restarts
the "alone" clocks, so the cat is not instantly sad or sleepy. Dizzy and Sleepy are not in
the menu (Dizzy stays a drag-only reaction; Sleepy is the Sleep action).

## One app language (2026-10-13)

**Language / Ngôn ngữ** is the first setting in the **General** tab of the settings window. English (default)
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

## Menu and settings layout (2026-10-15)

The cat's right-click menu is grouped, with Windows' own icon font (no new assets). Since
2026-10-23 it is kept short (10 rows, 2 separators); games, the cat's pages and rarely used items
sit in submenus:

1. **Give treat, Play › (Play with ball / Put ball away, Laser pointer), Battle Mode, Do now ›,
   Emotions ›**: playing with the cat. While the ball is out, **Put ball away** also shows on the
   main menu right under Play (one click). Play is dimmed when neither game can start.
2. **Quiet mode** (tooltip), **{name} ›** ({name}'s status, {name}'s diary, Favorite resting spot ›),
   **Settings…**, **More ›** (Sounds, Bring the pet to the main screen, Behavior debug view, About).
   Sounds is also the master switch in Settings → Sounds.
3. **Exit pet**.

**Settings…** (also in the tray menu) opens one window with four tabs:
- **Pet:** name, personality, the user's birthday (day and month, optional; the pet wishes a happy
  birthday that day), coat and accessory with a live preview, link to Advanced behavior tuning…
- **Speech:** talking on/off, how often (greyed out while off), link to forget what was learned.
- **Sounds:** master switch, three kinds, volume, preview picker + Play.
- **General:** Language / Ngôn ngữ, Start with Windows, break reminders.

Nothing changes until **Save**; **Cancel**, Esc or closing the window keeps the old
settings. Save and Cancel stay at the bottom; a tab scrolls on its own on a small screen.
The tray menu calls the AI switch **Smart pet (AI)** / **Mèo thông minh (AI)** and adds
**Settings…**.

The right-click menu (not the tray menu, which is a WinForms menu) has its own look
(`PetMenuStyle.cs`, 2026-10-16): a rounded white panel with a soft shadow, rounded hover
rows, a fixed icon column (a check mark when a checked item has no icon, a tinted square
behind a checked item's icon), submenu arrows and dimmed disabled items. **{name}'s diary**
opens with days together, four number tiles (petted, treats, ball games, bond) and cards
for the bond chart, play hours and what was learned. **Behavior tuning** groups its eight
values into Personality, Attention and Energy cards, each with the exact number, a slider
and a hint; Restore defaults, Cancel and Save stay at the bottom.

## Status card and warm dialogs (2026-10-21)

**{name}'s status** / **Trạng thái của {name}** (right-click menu → {name} ›, before the diary) opens a
small rounded card above the cat (below it when the cat is near the top of the screen): what
the cat is doing (or "In a battle · HP", "Paused") and its emotion, refreshed twice a second,
then (2026-10-22) the newer of its last speech bubble ("Last said: “…” · 3 min ago") and last
thought bubble ("Last thought: hungry · just now"), kept for the session even after the bubble
faded; the line is hidden until the cat has said or thought something. And, when the Python AI has reported them, five bars where full is always good: Energy, Fed
(100 − hunger), Entertained (100 − boredom), Loved (affection) and Bond. With the AI off the
bars are hidden and a hint points to **Smart pet (AI)** in the tray. The card is display only:
opening it never wakes, moves or interrupts the cat and is not an interaction. Clicking
elsewhere, Esc or the × closes it (`PetWindow.Status.cs`). Press and drag anywhere on the
card (except ×) to move it; it stays where it is dropped. It is placed with the pet's own
monitor scale (MoveWindow), like the battle notice, so it opens next to the cat at any DPI.

The forget-habits question, About and the two "could not save" notes use `PetDialog`
instead of the grey Windows MessageBox: a cream window with a round orange icon, the
message and pill buttons (Cancel on the left, the main button on the right; Esc cancels).

Battle, Adventure and Expedition panels use the shared type scale (`PetUi` 11/12/13/14/16/20)
instead of half sizes (10.5–15.5 → the next step up; the 22 banner → 20).

## Battle hub and shared game panels (2026-10-22)

- **Menu:** with no battle, **Battle Mode** is one item (no submenu) that opens the hub; it is off,
  with the reason, while the pet is paused or a fullscreen app is open. During a battle it is a
  submenu with **Pause / Resume** and **End battle** only. **Start battle** (the free battle) is gone.
- **Hub** (`PetWindow.BattleHub.cs`, window "Pet battle hub", like the maps: clickable, never
  activated, no taskbar button, mouse only, hidden by a fullscreen app, holds the pet's free time):
  this cat holding its weapon, "Battle Mode", and three cards with a small vector picture each
  (`ModeArt`: a path over hills to a flag; a snowy peak at dusk; a gold ∞ in a starry night), the
  mode's name, one line about it, where the user is (Adventure: chapter · stages won with a bar;
  Expedition: locked until the Slime King is won / run under way with step and HP / keepsakes;
  Infinity: locked until the Slime King is won / the furthest wave). Adventure and Expedition
  open their map in its place, Infinity the setup panel (its ‹ Hub comes back);
  the maps' **‹ Hub** button comes back here, the hub's **Close** goes back to the desktop.
- **Rewards** (2026-10-22, `PetWindow.Rewards.cs`): the hub's **Rewards** button (it says
  "· n new" when rewards were won since last shown) shows, in the hub's window in place of the
  cards (the button turns into **‹ Hub**): a tile per Adventure accessory (this cat wearing it) and
  per Expedition keepsake (as on the shelf), won ones in gold with ✓, the others greyed with a lock;
  hovering a tile says where it shows or what wins it; a **New** badge on rewards not shown before
  (then saved as seen: `RewardsSeen` in adventure-progress.json, no version change; an older file
  shows every held reward as new once). Then **Records**: each Adventure stage (wins, fastest) and
  the Expedition per difficulty (furthest step, runs won, fastest), over every weapon.
- **Bag** (2026-10-22, `PetWindow.Inventory.cs`, window "Pet bag", view only): a floating window
  for the mode being played, opened from the battle HUD's bag button (between the skills and
  Pause/End; the battle pauses while it is open, `BattlePause.Bag`, also over a card offer, and
  only that reason is removed on closing), the Adventure map's **Bag** (the build carried into the
  chosen stage) and the Expedition map's **Bag** (the run; also while a choice waits; off while a
  capsule opens, and it does not refresh until the capsule has popped). Dragged by its header;
  where it was put is kept for the session (on the cat's monitor), otherwise top right under the
  HUD. 440 DIPs wide, at most 540 tall (or the monitor's height less 24), the content scrolls.
  Never takes focus, no taskbar button, hidden by a fullscreen app; closes with ×, the same button
  again, or when its battle/map goes away. Three tabs: **Stats** (icons and bars: max HP, damage
  per hit, attack speed, reach, knock-back, running speed, damage taken, Healing fish; cream = the
  weapon with no cards, gold = what cards/relics add, red = what they take; numbers from the
  battle's own rules, in a battle the battle itself; warnings: each relic's price, low HP, Reckless
  on; **Skills**: each skill's cooldown and the fish's heal after the relics, in a battle how long
  until each is ready; **This battle** (in a battle only): enemies defeated and what the build has
  done: hits reflected, echo hits, Turtle shell blocks, charged, opener and counter hits, each shown
  once the build has that effect; **If you take** (over a card offer): the card under the mouse,
  or else the picked one, with what it does, the synergy it would switch on and → the numbers after
  it in blue; the Cards tab shows the same card on top), **Cards** (tiles with the card picture, ×n, ◆ rare, group colours; hover for the text; each
  group's points as pips with ★ synergies on and "n more → ★ …"), **Relics** (Expedition: slots
  n/max, each relic's ▲ gain and ▼ price, the run log latest first; elsewhere: where relics come
  from and the chapter's stages). The run log is `Log` in the run (expedition.json, no version
  change, at most 60 lines): won battles, left battles and each choice with what HP did.
- **Expedition setup:** before a run the map's main button is **Prepare the run**: the same setup
  panel as an Adventure stage (preview cat, weapon cards with stats, difficulty), titled for the
  run; **Set out** starts the run (no battle yet, so only Pause/fullscreen keep it off) and comes
  back to the map, **‹ Map** comes back without one. The map's old weapon/difficulty chips are gone.
- **Shared pieces** (`PetWindow.GamePanel.cs`): the panel frame (cocoa, cream rim, content that
  scrolls when taller than the cat's monitor minus 24 DIPs, buttons docked below; now also the
  Adventure map), the title with the mode's round picture, the orange main button, the details
  card, the chosen look (orange, white rim, tick). Difficulty chips show one to three gold stars.

## Expedition step gacha and capsule machine (2026-10-22)

- **Step gacha:** the built-in Expedition draws each step's nodes when the run gets there. The map
  shows each step not drawn yet as one "?" node (the step to go now with a gold rim) and the boss.
  The details card shows a capsule, "Step N/6 · Step machine", the odds per node (fight 45 %,
  event 20 %, elite 20 %, rest 15 %; step 1: no elite or rest) and the main button **Spin**. Spin
  draws and saves the step at once; the capsule then shakes, pops (the "pop" cue) and opens
  (about 1 s, cosmetic: nothing can be clicked meanwhile), and the step's 2–3 nodes appear.
  Events now show "!" ("?" means not drawn yet).
- **Capsule machine:** after a won fight node (not an elite or the boss) the result panel says the
  machine waits; the map shows two capsule cards (plain · free, gold · −12 % of max HP, each with
  its odds from what the run can still get) and Skip. Picking one changes and saves the run at
  once; the capsule opens on the map and its prize is said when it pops ("sparkle" for a relic or
  rare card) and stays as the map's note line.
- Draws come from the run's seed and are kept, so a double click, closing the app or spinning
  later never draws again. `rulesVersion` 5: a run from before ends when the map opens, with a
  note; older records stay apart. `expedition.json` version 4.

The Expedition map is at most the cat's monitor height minus 24 DIPs (set when it opens). At
its tallest (three relics and a rest's cards) it is about 574 DIPs, so on a low or high-DPI
screen (1366×768 at 125 %, 1920×1080 at 175 %, 1280×720 at 150 %) its content scrolls with a
thin warm scroll bar and the Close / Give up / main buttons stay at the bottom. Big enough
screens see no change. The battle setup panel does the same (about 519 DIPs for a stage with a
long goal; How to play / Stage tip / Close or Map / Ready stay at the bottom). Measured with the
built-in data in both languages (UiChecks `PanelHeightChecks`): the Adventure map (~331 DIPs)
and the result panel (~449 DIPs at its fullest) fit a 480-DIP screen as they are.

## Keepsake display corner (2026-10-21, Expedition milestone 4)

**Settings… → Pet → Keepsake display corner** (on by default; nothing shows until a
keepsake is earned). A won Expedition run gives keepsakes once each: a bronze, silver or
gold cup for the first win on Easy, Normal or Hard, a slime cushion for the first win on
Normal or Hard, and the Expedition scarf (an accessory, then offered in Settings → Pet).
They are kept in `expedition.json` (version 3).

- The cups and cushion stand on a small shelf at the bottom right of the monitor the cat is
  on (it follows the cat to another monitor), on the same floor line. Vector art, redrawn
  only when the list changes; placing it is one window move a frame at most.
- Click-through, never takes focus, no taskbar button. Hidden while a fullscreen app runs
  or a battle is on; turning the setting off hides it at once.
- Now and then (first after ~5 minutes, then every 10–15 minutes) the cat walks to just
  left of the shelf and looks at it for 4 seconds (local C#, like the resting-spot visit).
  Never in Quiet mode or with a fullscreen app, never while asleep, held, on a window
  ledge, in a battle or busy with a game or treat; Quiet turned on mid-walk ends it.
  Python is not told anything new.
- With a custom battle.json no keepsakes are given.

## Walking on windows (2026-10-15)

**Settings… → Pet → Climb on windows** (on by default). The pet treats the visible top
edge of ordinary app windows on its monitor as a floor:

- **What counts:** visible, not minimized or maximized, not cloaked (other virtual
  desktops), titled, unowned windows (as in Alt+Tab), at least 200×80 DIPs, with room for
  the pet above the edge. The frame comes from DWM (no invisible resize borders). Parts
  of the edge covered by a window above it are skipped. Only rectangles, styles and
  z-order are read, never window content; the list is refreshed about 3 times a second,
  and the window the pet stands on every frame.
- **Landing:** a falling (thrown, dropped, jumping) pet lands on an edge it comes down
  onto; rising through an edge never stops it. Landings faster than 400 DIPs/s bounce.
- **Walking:** wandering turns at a ledge's ends. Games (ball, laser) and the walk to the
  resting spot carry the pet past the end, where it drops. The ball is batted only from
  the floor; "Remember this spot" is disabled on a window.
- **Riding:** moving (dragging) the window moves the pet with it in the same frame.
  Closing, minimizing or maximizing it, or covering the spot, drops the pet.
- **Sleep:** a sleeping pet whose window vanishes falls in its sleeping pose, lands
  without bouncing and keeps sleeping; waking keeps its 1.2 s even while falling.
- **Going up on its own** (local C#, roughly once a minute, not in Quiet mode or with a
  fullscreen app): a ledge 60–380 DIPs above the feet and within 260 DIPs sideways is a
  leap (usual 0.16 s crouch, velocity planned to peak 40 DIPs above the edge). A taller
  window whose bottom is within 100 DIPs of the floor and whose side is uncovered is
  climbed: the pet walks to the side, climbs at 110 DIPs/s facing the window (Climbing
  pose: body stretched and leaning in, front paws above the head hooked over the edge and
  reaching up in turn, hind feet braced against the side, tail hanging on the outside,
  eyes on the top), then hops onto the top.
- **Going down:** after about 1–2 minutes on a window the pet walks off the nearer end.
- **Letting go:** a click, right-click, Quiet mode or the window going away makes a
  climbing pet drop. Dragging works as usual. AI commands cannot interrupt a climb
  (`Climbing` is in `BUSY_STATES`).
- Turning the setting off, or a fullscreen app, removes all ledges: a pet on a window
  falls and windows are no longer read. Reset and a DPI change put the pet on the floor.
- The debug view shows a `Windows:` line (off / floor / on a window / climbing, ledge
  count, plans).

## Performance measurement (2026-10-16)

`scripts/perf.ps1 -Minutes 5 -Label idle` samples the pet and its Python service once a
second: CPU (share of the whole machine), working set and private memory, handles and
threads. It attaches read-only to the running pet; if none runs it starts the published
pet with separate data (`artifacts/perf-data`) and closes it at the end. Samples go to
`artifacts/perf/<time>-<label>.csv`, and a summary (CPU avg/p95/max, memory, handle and
thread growth) is printed. Do the activity to measure while it runs (idle, sleep, fast
drags, laser, a long run); a steady rise in private memory or handles over a long run
points to a leak.

## Battle Mode (2026-10-16)

Optional mini-game, off by default and never resumed at startup. Right-click the cat →
**Battle Mode / Chế độ chiến đấu** opens the battle hub (2026-10-22, below); an Adventure stage or a
new Expedition run opens the setup panel (below); **Ready** there starts the fight. (The free
battle described here is no longer in the menu; its code stays for tests and `perf.ps1`.)
**Infinity** (2026-10-22, `PetEndless.cs`, `PetWindow.Endless.cs`, battle.json `endless`): a
battle on a generated stage that is never won — waves grow and get stronger, a card every
`upgradeEvery` waves, the boss every `bossEvery`; the HUD says "Wave N" with ten marks around it;
End (HUD or menu) finishes the run as a loss at the wave reached, so the result panel and the
record (furthest wave per weapon and difficulty, battle-records.json) follow. Closing the app
mid-run keeps nothing. Milestone rewards (`endless.milestones`, 2026-10-22): clearing wave
10/20/30/40/50 once (any weapon or difficulty, never with custom data) earns the Infinity collar,
the endless hourglass (a keepsake shown on the screen), the star halo, the nebula orb (keepsake)
and the galaxy cape. They come from the records' most waves cleared (`MostCleared`; older records
count MostWaves − 1), so they are never lost; a banner "★ Unlocked: …" shows when the wave is
cleared, the result panel names them, the hub's card says "Rewards k/5 · next: clear wave N",
and Hub → Rewards has an "Infinity rewards" section. The two keepsakes join the Expedition's in
Settings → Pet (one shown at a time, same physics as a cup). The cat draws a wooden toy sword and
fights slimes on the floor of its current monitor (work area) in 3 waves (2 slimes; 2 slimes +
2 quick slimes; a rock slime + 2 quick + 2 slimes; at most 3 at once, a short break between
waves), then the **Slime King** alone. Quick slimes are small, fast and frail (wind streaks);
rock slimes are big, slow, tough and hard to push (cracks). During a battle the cat runs
(walking speed and acceleration × 1.8, `catRunScale`). The King: twice as big,
400 HP, a crown; its ground slam winds up 1.2 s with a red zone on the floor and hits 25 within
110 DIPs; below half HP it calls two helper slimes (once), below 30% it gets angry (faster,
0.8 s windup); pushes move it and stun it less. Its HP bar replaces the wave dots on the HUD. The battle is won after the last wave, or lost
when the cat's HP reaches 0 (it sits down seeing stars; nothing lasting: no anger, sadness,
bond or hunger change). Both endings close the battle by themselves after ~3 s and show the
result panel (2026-10-19): this cat cheering with its weapon or sitting dazed, the loadout
(weapon · difficulty), slimes defeated and time, HP left (smaller), a gold "New record" line and
the loadout's records, plus the legacy records of that difficulty if there are any.
**Play again** starts the same weapon and difficulty at once if the cat is free (otherwise the
setup panel opens with that choice and the reason; nothing is forced), **Change gear** opens the
setup panel with it, **Back to desktop** closes. It no longer closes by itself; a fullscreen app,
bringing the pet back or Exit still close it. Each button acts once (the panel is gone before the
next step), so a double click never starts two battles. Ending a battle by hand shows no panel. After each wave except the last the cat gets 15 HP back (`WaveHeal`).

- **Setup panel** (2026-10-19, `PetWindow.BattlePrep.cs`; redesigned the same day): the right-click
  menu holds only Start / Pause / End. Start opens a panel at the top centre of the cat's screen.
  Left: a preview of this cat (coat, accessory) holding the chosen weapon, on its own `PetModel`
  (never the real cat); **Try it** swings it once at the weapon's pace and hit moment with its hit
  mark and the usual swing cue (mute/volume/cooldown apply), on its own timer that stops when the
  swing ends, the weapon changes, a fullscreen app appears or the panel closes. Right: one card
  per weapon in the battle data (vector art, name, hint, four bars on fixed scales with the real
  numbers: damage per hit 0–60, swings per second 0–2, reach 0–120, knock-back 0–300 DIPs/s); the
  chosen card has a white rim and a tick, not only a colour. Then the difficulty (three buttons,
  the chosen one ticked), a small **How to play** link, **Close** and the main button **Ready**. Nothing fights yet: no battle object, slime, timer, damage or
  record. It starts from the last choice that went into a battle (`BattleWeapon` as a weapon id,
  `BattleDifficulty`); a choice is saved only by Ready. Opening it again keeps the one panel.
  While it is open it owns the cat's free time like Quiet mode: no random actions, autonomous
  sleep, away nap, AI requests (replies from before are dropped), Do now, emotions menu, treats,
  ball, laser, rest visits, speech or break reminders. What is already going on is not cut: a
  sleeping cat sleeps on (and wakes by its own rules), a drag, throw, game or treat finishes.
  **Ready** checks the cat again when pressed (same rules as before: asleep, waking, dragged,
  flying/climbing, on a window, playing/eating, reacting, paused, fullscreen) and is greyed out
  with the reason on the panel until the cat is free (refreshed 5 times a second, also while the
  pet is paused). A second press finds no panel (no second battle). The panel cannot open while
  the pet is paused, a fullscreen app is open or a battle runs; a fullscreen app appearing hides
  it until it closes. **Close** closes it: no win or loss, nothing saved, the pet's life comes
  back. Reset ("bring the pet back") and Exit close it too. The panel never takes focus (mouse
  only, no keys from other apps), follows the UI language and is placed in the monitor's DIPs.
  The result panel's **Change gear** (or Play again when the cat is not free) opens it with the last battle's choice.
  `PET_BATTLE_AUTOSTART` (measuring) still starts a battle directly.
- **Difficulty** (on the setup panel: Easy / Normal / Hard, remembered in
  `preferences.json` as `BattleDifficulty`): Easy and Hard multiply
  every enemy's HP, damage and speed (battle.json `easy`/`hard`: 0.65/0.5/0.85 (0.75/0.6/0.85 before 2026-10-20) and 1.3/1.4/1.15,
  each 0.25–3; scaled HP and damage are rounded to whole numbers). Normal is the file's numbers
  untouched. The cat, skills and waves are the same on every level.
- **Records** (in `%LOCALAPPDATA%\DesktopPet\battle-records.json`, `PetBattleRecords.cs`, `Version` 2
  since 2026-10-19): per loadout, keyed by the weapon's stable id and the difficulty
  (`"hammer|Normal"`), with `|custom` for battles fought with the user's own battle.json (same ids
  may mean other numbers, so they are kept apart and not compared). Wins, fastest win and most
  slimes defeated in one battle (a loss can set that one). Version 1 files (one record per
  difficulty, both weapons together) are kept as they were under `Easy`/`Normal`/`Hard` and shown
  as "Before weapons were told apart"; they are never given to a weapon. A weapon removed from the
  data keeps its records. A file from a newer version is read but never overwritten. The result
  panel shows the loadout, a gold "New record" line when one was beaten and the loadout's records.
  Battles ended by hand do not count. A broken file is logged and starts over; it never stops a
  battle. A pet built with given preferences (UI checks) keeps records in memory only.

- **Start** (opening the setup panel) is refused, with the reason in the menu or a small
  click-through note above the cat that fades after 2.5 s, only while the pet is paused, a
  fullscreen app is open or a battle runs. **Ready** waits (greyed out, reason on the panel) while
  the cat sleeps, wakes, is dragged, flies/climbs, stands on a window, plays/eats or reacts
  (angry/surprised). Nothing is woken or forced.
- **The cat fights by itself**: picks the nearest slime (or the marked one), walks to it with the
  normal walking physics and swings; each swing can hit once, at a fixed moment of the swing.
- **Combo**: three hits in a row, each within 1.6 s of the last (`comboHits`, `comboWindow`), make
  the third a combo: ×2 damage (`comboDamage`, on top of Cheer), the slime is knocked away from
  the cat (300 DIPs/s × its `pushFactor`, not stunned), a gold slash on the cat, a gold burst with
  "COMBO!" and the ball-tap cue. A miss, lifting the cat or a longer gap ends the chain; the chain
  may move from one slime to another.
- **Click a slime** (left): mark it as the cat's target (yellow arrow + ring). No damage.
- **Skills** (five slots on the HUD; hover for details; mouse only):
  - **Push** (5 s): choose, then click a slime: knocked back and stunned ~1.4 s. Click its left
    half to send it right, its right half to send it left (an arrow shows the way on hover); the
    very centre sends it away from the cat.
  - **Healing fish** (20 s): +25 HP at once (not used at full HP).
  - **Cheer** (18 s): for 5 s the cat swings 1.6× as often and hits 40% harder (gold sparkles).
  - **Yarn bomb** (12 s): choose, then click a slime: a ball of yarn drops there and bursts after
    0.5 s for 30 damage to every slime within 70 DIPs of the spot.
  - **Bubble shield** (15 s): for 3 s slime hits are blocked (a bubble around the cat).
  Aimed skills never change the cat's target; cancel the choice by clicking the slot again or
  right-clicking the field. A slot shows a clock-wipe and seconds while cooling. Hovering a slot
  (or Pause/End) describes it in the HUD's hint line (no tooltips: they kept the first text in a
  window that never activates).
- **Slimes** swell and show a red "!" for 0.8 s before each hit: time to push them or lift the cat.
- **Rescue** (field kept alive 2026-10-21): pick the cat up as usual (drag, dangle, dizzy and
  throw unchanged). While carried or in the air (lifted: a drag past the threshold, a throw/fall,
  a climb or jump) the cat takes no damage and no stun. A rescue never freezes the slimes: a
  windup runs on its announced time; its hit misses if it falls while the cat is lifted (the slime
  then takes its recover and full attack cooldown) and lands as usual if the cat was put back in
  its reach before (unless the landing protection is on). A dash runs its locked lane to the end
  and hits only a cat on the floor. A cat only pressed (a click, not lifted) is still on the floor:
  an attack under way reaches it (no stun), but no new windup starts. So a well-timed rescue
  dodges every hit, and lifting and dropping on the spot does not cancel them. While the cat is
  carried, a coming windup shows a pulsing ring on its "!" and a dashed red ring on the floor
  (reach + 12 DIPs: put the cat down outside it), and the HUD hint says a slime is about to hit.
  After a protected landing the hint shows the protection's seconds left. Other slimes go by where
  the cat last stood on the floor (not by the cursor) without starting a windup; supports keep
  healing/teleporting and the boss its shield phases, summons and rally; no slime spawns or wave
  progress happens. The battle stays on the monitor it started on: if the cat lands on another monitor the
  battle pauses (a note by the cat says where the battlefield is) and goes on by itself when the
  cat is back; a pause the user chose meanwhile is kept. A work-area or DPI change on the battle's
  own monitor moves the field with it. If the battle's monitor is disconnected (checked once a
  second), the field and HUD move to the cat's monitor (primary as fallback) in that monitor's own
  DIPs/DPI; slimes, bombs and effects keep their relative spot inside the new floor; HP, wave and
  cooldowns stay; the battle stays paused with a note until the user resumes. The cat is never
  moved and a drag keeps its mouse capture.
  After a real lift of at least `rescueGraceMinLiftSeconds` (0.3 s; drag past the threshold, or a
  throw/fall; a plain click does not count) the landing protects the cat for `landingGraceSeconds` (1.5 s), at most once per
  `rescueGraceCooldownSeconds` (5 s) counted from when it is given and only while the battle runs
  with the cat on the ground; landing again sooner neither renews nor extends it. The HUD hint
  while the cat is lifted says whether this landing will protect it.
  Skills wait while the cat is held (pressed, carried, flying): `BattleSim.SetCatState` is the one
  rule for the HUD, the window and the simulation; no skill can be used (checked in BattleSim, not
  only on the HUD), cooldowns do not recover, and a real lift cancels a chosen aimed skill. A bomb
  already dropped still bursts; Cheer/Shield already running wear off as usual. A plain press keeps
  the chosen skill and the HUD unchanged (no flicker).
- **Weapons** (2026-10-18; data-driven 2026-10-19): chosen on the setup panel from battle.json
  `weapons` (id, names, hint, drawing `style` "sword"/"hammer", numbers). Built in: Sword (the existing numbers: 25 damage,
  reach 76, 0.75 s between swings, hit at 0.16 s of a 0.36 s swing; only a combo knocks back;
  hitEffect spark) or Hammer (2026-10-19 numbers: 45 damage, reach 76, 1.35 s, a 0.7 s swing hitting
  at 0.45 s, and every hit knocks the slime back at 170 DIPs/s × its pushFactor, no stun; a combo
  uses the larger of comboKnock and that; hitEffect thud: dust and a ring on the floor). The hit is
  judged when it lands, so a slime that moved out of reach during the long wind-up is missed.
  The drawing times the slash to the hit (`WeaponSpec.HitFraction` → `PetModel.SwordHitAt`): the
  hammer is raised, held with the cat crouching and trembling, then slammed with a wider trail.
  Damage over time is about the same. A hammer reach longer than the sword's was tried in
  simulation (78–84 DIPs): with the knock-back a plain slime then never reached the cat, so the
  reach stays equal. Not confirmed by real play. Remembered in preferences
  `BattleWeapon` as the weapon id (older files' "Sword"/"Hammer" still match, ids ignore case; an
  id the data no longer has reads as the first weapon), never changed during a battle,
  shown next to the cat's name on the HUD and on the result panel. Records are per weapon and
  difficulty (see Records). The hammer is a vector mallet in the same poses as the sword.
- **First-battle guide** (PetWindow.BattleTutorial.cs): four cards (targets, skills, rescue and
  the held attack, protection and cooldowns, with battle.json's numbers) in a small panel above the
  floor strip; mouse only (Skip / Next / Done), never takes focus, follows the UI language, no
  sound. It opens by itself with the first setup panel until gone through or skipped
  (preferences `BattleTutorialSeen`, false in older files), and from the panel's How to play
  button; no battle exists behind it. Ready closes it (and marks it seen). Should it be open during
  a battle, the battle is paused (reason Tutorial). A fullscreen app, Cancel or bringing the pet
  back closes it without marking it seen; measuring runs (`PET_BATTLE_AUTOSTART`) never show it.
- **Pause / Resume / End**: HUD buttons or the menu. Pause reasons stack (User, Menu, Fullscreen,
  Away, App, Tutorial) and each owner removes only its own: closing the menu never resumes a battle the user, a
  fullscreen app or an away cat still holds. A fullscreen app pauses it, hides the field/HUD and
  leaves a User pause, so the user resumes afterwards. Every reason except Away also freezes the
  cat's physics: a thrown cat hangs with its position and velocity and goes on after Resume with
  one normal frame (no catch-up, no burst). The cat can still be dragged; a drop during the pause
  keeps the drop's position and velocity and waits for Resume. Away does not freeze physics (the
  cat must be able to fall and be carried back). Skill effects on the cat follow the battle's
  clock and stand still too. Paused time is not in the battle's length (records). Pausing the
  whole pet (tray/menu Pause) adds the App reason: the field and HUD stay on screen but no skill,
  bomb or target can be used (BattleSim refuses too); un-pausing removes only App, so the
  battle's own pause stays; End still works. "Bring the pet to the main screen" and Exit end it.
- **While it runs** the battle owns the pet: no random actions, sleep, away nap, AI commands,
  thought/speech bubbles, Do now, treats, ball, laser, rest-spot visits, kneading or window
  ledges (the fight is on the floor). Python receives no requests; replies from before are dropped.
- **Windows**: one transparent strip along the floor (`BattleFieldView`, 170 DIPs tall, the
  monitor's work-area width) draws every slime and effect, and one HUD at the top centre
  (`PetBattleHud.cs`: portrait, HP bar with a lost-HP trail, wave dots/boss bar, banners, skill
  slots, Pause/End icons).
  The HUD steps aside for the cursor: over its information part (portrait, HP, status, hint
  line) it fades to 25% and becomes click-through (WS_EX_TRANSPARENT), so the app under it can
  be clicked; over a skill slot or Pause/End it is solid and clickable as before. The cursor is
  read every frame (`GetCursorPos`), since a click-through window gets no mouse messages.
  Both are topmost, never activated (WS_EX_NOACTIVATE + MA_NOACTIVATE) and not in the taskbar.
  Empty strip pixels are fully transparent, so clicks pass through to the apps below; only slimes
  (plus an invisible 1-alpha halo for easier clicking) take clicks. The pet window stays above
  the strip. If the work area of the battle's monitor changes, the field moves with it and slimes
  keep their relative positions; the strip is placed in device pixels with that monitor's DPI.
  (The cat landing on another monitor pauses the battle instead; see Rescue.)
- **Drawing cost**: the strip and HUD redraw at most 30 times a second and only while something
  moves (slimes, effects, a falling bomb); a paused battle or an empty floor between waves is drawn
  once and left alone, and a click/pause/move asks for one redraw at once. The pet itself keeps its
  60 FPS. No drop-shadow effects on the HUD (on a layered window they are re-rasterized whenever
  anything under them changes); floating numbers, the marker arrow and windup colours are cached.
  The boss's slam zone is drawn as outlines only, so it never blocks clicks to the apps below.
- **Measuring**: `scripts\perf.ps1 -Battle run` (or `paused`) starts a separate pet that keeps a
  battle going (`PET_BATTLE_AUTOSTART`, measurement only; the user's pet must be closed first).
  `-Battle expedition` keeps the Expedition's heaviest kind of battle going instead (its last elite
  encounter at the last step's threat, a full set of relics, on a run kept in memory: nothing is
  saved, no keepsakes). `-Trophies` starts the separate pet with every keepsake, to measure Pet
  Mode with the display corner shown.
- **Data** (`src/DesktopPet/Assets/battle.json`, embedded, `schemaVersion` 1): every number (cat,
  skills, spawning, breaks, protection), the weapons (`weapons`), the enemy kinds (`enemies`: id,
  family, renderer, HP, speed, damage, reach, windup, drawn size `scale` and combat size
  `hitScale`, colours, boss/slam flags, push/stun factors, summon/enrage thresholds) and the waves
  (`waves`: groups of `enemy` × `count`, any wave may hold a boss). A new enemy variant, weapon or
  wave plan is data only; a new kind of ability or drawing needs code. The user can override it
  without rebuilding: `%LOCALAPPDATA%\DesktopPet\battle.json` (`PetPreferences.DataDirectory`),
  merged over the built-in file and validated as a whole, read each time the setup panel opens
  (a running battle keeps the data it started with). Schema, limits and examples:
  [BATTLE-DATA.md](BATTLE-DATA.md). Misspelt keys, impossible numbers, unknown ids, unsupported
  styles/renderers, sizes over 2.3× or summon loops reject the whole file: it is logged, the
  built-in battle is used and the setup panel says what is wrong (`BattleSettings` defaults, the
  same numbers, if even the embedded file is broken). Skills are listed once in `BattleSkills` (`PetBattleData.cs`: name, aimed or instant,
  cooldown, description built from the numbers); the HUD makes a slot for each. A new skill also
  needs its effect in `BattleSim.Use` and an icon in `HudGlyph`.
- **Code**: `PetBattle.cs` (simulation, no WPF: tested in `tests/DesktopPet.Tests/BattleTests.cs`),
  `PetBattleView.cs` (drawing/hit-testing), `PetWindow.Battle.cs` (lifecycle, orders → walking,
  HUD, menu), `PetWindow.BattlePrep.cs` (setup panel), `PetBattleData.cs` (data loading, override,
  validation); the sword is drawn by `PetRenderer` from presentation fields on `PetModel`.
  Sounds: three short cartoon effects synthesized in `PetSounds` (no new assets): swoosh for a
  swing or push, squish for a hit, pop for a defeated slime; the cat's own cues stay for being hurt
  (surprised), wave clear/victory (excited) and defeat (annoyed). One cue per frame, the most
  important event wins; the usual mute/volume/category rules apply, effects have a 0.25 s cooldown.

## Adventure (2026-10-20, milestone 2 of docs/ADVENTURE-UPDATE.md)

- **Where:** right-click → Battle Mode → the hub's **Adventure** card (2026-10-22; before, a submenu
  item above **Start battle**). It is refused, with the reason, only while the pet is paused, a
  fullscreen app is open or a battle runs.
- **Map** (window "Pet adventure", like the setup panel: clickable, never activated, no taskbar
  button, mouse only): chapter 1 "The Slime Invasion", five stage nodes joined by a path. Won: gold
  with a tick; open: blue with its number; locked: grey with a padlock (not only a colour). Click a
  node for its name, goal, state ("Locked — win stage N first", wave count, "Completed") and its
  wins/fastest win. **Play stage** (only for an open stage) opens the setup panel for it; **Close**
  goes back to the desktop. While the map is open it owns the pet's free time like the setup panel
  (Quiet, no AI commands, games, chatter or away nap; sleep, a drag or a flight are not cut). A
  fullscreen app hides it until it closes; Reset/Exit close it.
- **Stage setup:** the usual setup panel (weapon, difficulty, Try it, How to play) titled
  "Stage N · name" with the goal; its close button says **Map** and goes back to the map. Ready
  starts the stage with the battle engine as before; only the waves (and the enemy cap) are the
  stage's own.
- **Result:** after a win or a loss the result panel names the stage and the loadout. Main button:
  **Next stage** after a win when there is one (opens the setup panel for it with the same gear),
  otherwise **Play again**; below it Play again / **Map** / **Back to desktop**. It says what was
  unlocked, "Chapter complete!" after the last stage, and the stage's records for that gear.
- **Progress** (`%LOCALAPPDATA%\DesktopPet\adventure-progress.json`, `PetAdventure.cs`, Version 1):
  stages won, rewards given (later), tips seen (later), records per
  `stage|r<rulesVersion>|weapon|difficulty` and the last 50 run ids. Each finished run (won or lost)
  is counted once by its run id; a loss never takes back an open stage; replaying a won stage is
  allowed. Ending a run by hand, Reset or closing the app counts nothing (a battle in progress is
  never saved). Saved at once after each run: written to `.tmp` and moved over the file, the old file
  kept as `.bak`; a broken file is read from `.bak`, else progress starts over (logged); a file from
  a newer version is read but never overwritten. A pet built with given preferences (UI checks)
  keeps it in memory unless `AdventureProgressFile` is set.
- **Records:** stage records live only in the progress file; a stage battle never touches
  `battle-records.json` (the free battle's), so the two are never compared. They still count in the
  session's won/lost/combos sent to Python.
- **Custom data:** with the user's battle.json in use, Adventure can be played but the map and the
  result say so and the run saves no progress or record.
- **Data:** `"campaign"` in battle.json (docs/BATTLE-DATA.md). `BattleSim(..., stage)` takes a
  `StageSpec`: `WavePlan` and the enemy cap come from it; without one nothing changes.

### Adventure upgrades and the survive objective (2026-10-20, milestone 3)

- **Upgrades** (battle.json `"upgrades"`, a stage with `"upgrades": true`; stages 2–5 of chapter 1):
  after a cleared wave, when another wave follows, `BattleSim` offers up to three different
  upgrades the run can still use (not at their `maxStacks`; Grilled fish not at full HP); fewer
  when fewer fit, none when nothing fits; no reroll. Never in a free battle, after the last wave, a
  win/loss or End. The built-in six: Sharp edge +20 % damage (×3), Quick feet +15 % running speed
  (×2), Fast paws −12 % time between swings (×3), Heavy push +80 knock-back per hit (×2), Grilled
  fish +30 HP now, Thick fur −15 % damage taken (×2). Cuts stop at 30 % of the original.
- **Choosing:** the panel "Pet upgrade" (clickable, never activated, no taskbar button, mouse only,
  vi/en) shows each card's picture, name, effect and how often it was taken; click a card, then
  **Confirm**. While it is open the battle is paused for its own reason (`BattlePause.Upgrade`):
  nothing moves, the record clock, wave break and skills wait. Confirm removes only that reason (a
  user, menu, fullscreen or away pause stays) and plays the satisfied cue; a second click does
  nothing. A fullscreen app hides the panel until it closes; End/Reset/Exit close it.
- **Run only:** upgrades live in that `BattleSim`; Play again, Next stage or a new battle start
  without them and the shared definitions never change. The 15 HP heal between waves stays. The
  HUD lists the run's upgrades in a small gold line; the result panel lists them too.
- **Survive** (`"objective": "survive"`, `"surviveSeconds"`; stage 3 "Hold on", 75 s): the waves
  come in order and the last one comes again while time is left (the stage's `maxAlive` still caps
  the floor). The clock runs only while the battle runs and the cat is on the ground (carrying the
  cat stops it, like the waves); time up with HP left wins, no HP loses. The HUD leads with
  "Survive: 0:42 left"; the map says "survive 75 s". A survive stage keeps no fastest win.
- Stages that changed rules (2–5) have `rulesVersion` 2, so their old records are kept apart.

### Herb slime, chapter boss, rewards (2026-10-20, milestone 4)

- **Herb slime** (`mender`, stage 4 every wave, stage 5 wave 2 and as a boss helper): teal with a
  white "+". Every 4.5 s (first after half that) it winds up 1 s (a pulsing green "+" over it, a
  dashed green ring under the ally) and then gives the most hurt ally within 170 DIPs up to 15 HP:
  never itself, never past max HP, never a defeated one, and at most 90 HP in all (`healBudget`),
  so healing can never outlast the user. A Push (or any stun) during the windup cancels it and costs
  its full cooldown. The healed ally shows rising green pluses and "+15". The first heal of a battle
  shows the banner "A healer! Take it down first". Healing goes on while the cat is carried.
- **Chapter boss** (`king_c1`, stage 5 only; the free battle keeps `king` as before): 450 HP and
  `phases`, each fired once when its HP fraction is reached: 75 % two slimes, 50 % a herb slime,
  40 % two quick slimes, 30 % angry. At most 4 of its helpers alive at once (more calls skipped). A
  killing blow fires no phase. Slam warning, pause, carried cat and stun rules are unchanged.
- **Rewards:** stage 3 gives the Slime hat, stage 5 the Slime King's crown (accessories.json entries
  with `"reward"`). Given once, the first time the stage is won with official data, recorded in
  `Rewards` of adventure-progress.json; stages won before a reward existed give it when the map
  opens. The result panel says "New reward: … — wear it in Settings → Cat"; the cat never puts it on
  by itself. Settings → Cat lists a reward accessory only once received (or while worn), and maps
  the list back by id. The map lists a stage's reward ("✓ received").
- **End** (HUD or menu) during a stage goes back to the map at that stage; nothing is counted. The
  free battle's End goes back to the desktop as before.

### Stage tips, saving notices, version (2026-10-20, milestone 5)

- **Stage tips** (battle.json `tipVi`/`tipEn`, stages 2–5): the first time a stage starts, its tip
  shows in the guide panel (one card, "Tip · Stage N · name", Done/Skip) and the battle waits
  (`BattlePause.Tutorial`). Closing it with Done/Skip saves it as seen (`Seen` in
  adventure-progress.json); a fullscreen app or the battle's end just closes it. The stage's setup
  panel has a **Stage tip** link to see it again.
- **HUD:** the name line reads "Name · Weapon · Stage N"; a short banner names the stage at start.
- **Saving notices:** when the progress cannot be saved (no access, disk full) the result panel and
  the map say "Progress could not be saved"; when adventure-progress.json comes from a newer version
  the map says it is read only (runs in this version are not saved, the file is left alone).
- **Balance:** `AdventureBalanceTests` simulates every stage × sword/hammer × Easy/Normal/Hard with an
  idle and a simple skill-using player (10 seeds) and prints the table; stage 3 now allows 4 enemies
  at once with a heavier repeating wave (rulesVersion 3). Simulation only, not real play.
- **Version:** 1.1.0-beta at the time (`DesktopPet.csproj`; 1.2.0-beta since the 2026-10-21 enemy buff, 1.3.0-beta since the Expedition release check); see CHANGELOG.md.

### Simpler enemy tactics, far heals, a tougher Slime King (2026-10-21)

- **Plain slime:** no more falling back to a healer or going round a rock to the cat's back; it walks
  up, winds up and hits. Older user battle.json files that still set `fallBackBelow`, `flank`,
  `flankSpeed` or `maxFlankers` keep working: those keys are ignored (one log line).
- **Herb slime:** the jump to a hurt ally is gone (`teleport*` keys are ignored the same way). With no
  hurt ally in reach, it heals the most hurt fighting ally below 35 % anywhere on the field: a normal
  heal (same amount, budget and guard) with a longer 1.2 s windup and a dashed green trail across
  the field to that ally, at most once every 8 s. Push or stun it during the windup to cancel it.
  The first far heal of a battle shows "A herb slime heals from afar! Push it to cut it". With no
  rock to hide behind it keeps behind the nearest fighting ally, as before.
- **Chapter Slime King** (`king_c1`): 600 HP, shields up three times (75 %, 50 %, 25 %; angry with
  the last). Its glowing crown now makes the helper nearest the cat elite for good (gold rim and
  star, its kind's own elite affix, half again its HP, hits 25 % harder) every 10 s, instead of a
  short burst of speed. Push the King while the crown glows to cut it. Rallied elites do not count
  against `maxElites`. Stage 5's tip says so; rules versions of stages 2–5 went up.
