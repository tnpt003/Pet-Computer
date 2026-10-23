# Desktop Pet for Windows

**[⬇ Download Desktop Pet for Windows (Setup.exe)](https://github.com/tnpt003/Pet-Computer/releases/latest/download/PetComputer-win-Setup.exe)**
— Windows 10/11 x64, no admin needed. If SmartScreen appears: **More info → Run anyway**. See **Install** below.

A small animated cat that lives above your desktop windows. It walks, jumps, sits,
sleeps, and looks around on its own. Grab it with the left mouse button, move it,
and release to throw it. Gravity, bouncing, and friction bring it back to rest.

**Python AI extension:** the existing C# engine now accepts high-level decisions
from an optional Python child process. See [the AI architecture and setup guide](docs/AI.md)
for the protocol, personality, memory, fallback, testing, and packaging details.

**Windows application:** double-click `artifacts\win-x64\DesktopPet.exe` in File
Explorer after publishing. The complete release includes .NET and Python. See
[Windows build, publish, and sleeping behavior](docs/WINDOWS.md).

**Emotions:** clicks, gentle petting, repeated clicks, fast cursor passes, and lack
of attention now produce distinct expressions. See [emotion triggers and controls](docs/EMOTIONS.md).

**Install (Windows 10/11 x64):** download `PetComputer-win-Setup.exe` from the
[latest release](https://github.com/tnpt003/Pet-Computer/releases/latest) and run it. No admin
rights are needed; the cat starts right away and gets a Desktop and Start menu shortcut. The
first start shows a welcome with a few tips, the language (English, Tiếng Việt or 日本語) and **Start with Windows** (ticked);
a copied pet that is still running is closed so the installed one takes over. The
installer is not code-signed yet, so Windows SmartScreen may say "Windows protected your PC":
click **More info → Run anyway**. Installed pets update themselves: they check GitHub on start
and every 6 hours, download a new version in the background and say so in a tray notification;
choose **Update to …** in the tray menu, or it is applied the next time the pet starts. After an
update the tray says so; click it to see what's new. Settings → General → Updates shows the
version, turns automatic updates off (then it only checks when you ask) and checks now. Your
settings, diary and battle progress (`%LOCALAPPDATA%\DesktopPet`) are kept through updates and
uninstalling (Windows Settings → Apps → Desktop Pet). A portable zip is in each release too
(it does not update itself).

**Run from source** (Windows 10/11 x64, from a clone of this repository; `scripts\run.ps1`
uses a local .NET SDK under `.tools\dotnet` if there is one, see section 2):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run.ps1
```

**Stop:** right-click the cat and select **Exit pet**, or right-click its tray icon
and select **Exit**. This also stops its Python process. Closing the launch terminal
does not stop the background pet. To run without AI, add `-NoAi` to the command.

## 1. Technology and architecture

This implementation uses **C# / WPF on .NET 10**. WPF is a good fit for a Windows-only
pet because it supports transparent, borderless, always-on-top windows and mouse
capture. No browser, game engine, external packages, or downloaded artwork are needed.
The character is drawn with WPF shapes; its animation changes the pose over time.
Optional high-level decisions run in Python 3.10+ using only its standard library;
the C# app retains local random behavior when Python is disabled or unavailable.

The app has a small native window containing only the character. It has no title
bar, taskbar button, or console window. A notification-area icon gives you controls
while the app runs in the background. It is a normal interactive desktop process,
not a Windows service.

```text
src/DesktopPet/
  DesktopPet.csproj   Windows GUI executable, WPF + tray support
  app.manifest       Per-monitor DPI awareness, normal user privileges
  App.xaml           Application entry point
  App.xaml.cs        Application lifetime and notification-area menu
  PetWindow*.cs      Transparent host, mouse capture, timing, monitor selection, UI
  PetRenderer.cs     Cat artwork and animated poses
  Assets/            Icon, sounds and data (battle.json, coats.json, accessories.json);
                     i18n/<language>.json: UI and data text keyed by its English (PetText.cs)
src/DesktopPet.Core/ Everything that does not need WPF (library, net10.0):
  PetModel.cs        State, tunable settings, gravity and collisions
  PetBehavior.cs     Timed random action selection
  ThrowTracker.cs    Cursor history and release velocity
  PetBattle*.cs      Battle simulation; PetAdventure/PetExpedition/PetEndless modes
  PetPreferences.cs  Preferences; PetJsonStore.cs reads/writes every data file
  AI/                Replaceable decision source, protocol, Python process bridge
ai/
  service.py         Personality, mood, memory, and high-level decisions
  personality.json   Behavior preferences
tests/DesktopPet.Tests/
  Program.cs         Dependency-free executable checks (references DesktopPet.Core)
tests/DesktopPet.UiChecks/
  Program.cs         WPF checks against a real PetWindow
scripts/
  run.ps1            Build and launch in the background
  test.ps1           Run physics checks
  i18n.py            List UI text in the code and data files that a translation table is missing
```

## 2. Setup on Windows

Use Windows 10 or 11 and install the **.NET 10 SDK**, not just the runtime:
<https://dotnet.microsoft.com/download/dotnet/10.0>
Choose x64 for a typical Intel/AMD PC or Arm64 for a Windows ARM PC. An editor is
optional; Visual Studio Code or Visual Studio with .NET desktop tools works.

Open a new PowerShell terminal after installing, then:

```powershell
cd <your clone of this repository>
dotnet --list-sdks
dotnet build .\src\DesktopPet\DesktopPet.csproj -c Release
dotnet run --project .\src\DesktopPet\DesktopPet.csproj -c Release
```

The last command keeps the terminal attached for development. To launch in the
background instead, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run.ps1
```

This script also detects a project-local SDK in `.tools\dotnet`, if present. The
execution-policy override applies only to this PowerShell process. You can also
double-click `src\DesktopPet\bin\Release\net10.0-windows\DesktopPet.exe` after building.

## 3. Transparent desktop host

`PetWindow` uses `WindowStyle.None`, `AllowsTransparency = true`,
`Background = Brushes.Transparent`, `Topmost = true`, and `ShowInTaskbar = false`.
The visible drawing receives mouse input; the empty transparent regions let you
interact with windows underneath. The host does not cover the entire screen.
`ShowActivated = false` and a `WM_MOUSEACTIVATE` hook prevent ordinary pet clicks
from taking keyboard focus away from your current app.

WPF documentation:
<https://learn.microsoft.com/dotnet/api/system.windows.window.allowstransparency>

## 4. Movement and screen boundaries

The model stores top-left position, horizontal and vertical velocity, facing
direction, grounded state, current action, and animation time. Distances use WPF
device-independent pixels (DIPs); velocity uses DIPs per second.

A dispatcher timer requests updates roughly every 16 ms. The physics accumulator
runs fixed 1/120-second steps, which keeps collision behavior stable when rendering
cadence varies. Elapsed time is capped at 0.1 seconds so resuming after a long pause
does not launch the pet across the desktop or produce a huge backlog.

For each step, physics applies `verticalVelocity += gravity * dt`, then
`position += velocity * dt`. Side and ceiling collisions reverse the relevant
velocity with energy loss. Floor impacts bounce until their speed falls below a
threshold; exponential horizontal friction then stops sliding. Walking reverses
direction at side walls.

The current monitor's **working area** defines the boundaries, keeping the pet
above the taskbar. Negative monitor coordinates are supported. Dragging can cross
monitors; releasing selects the monitor containing the pet's center. Autonomous
movement stays on that monitor. Work-area changes are checked once per second, so
the pet can recover after a monitor disconnect or a taskbar move.

The manifest uses PerMonitorV2 DPI awareness. Monitor DPI changes rescale position,
velocity and drag offsets together, and discard old throw samples. The last position
and favorite spot use monitor-relative coordinates; an unavailable monitor falls
back to the primary screen. Mixed-scale hardware should also be checked manually.

## 5. Dragging and throwing

Left-button down captures the mouse and saves the original grab offset, then eases
that offset toward the top of the pet's head so it hangs below the cursor. Every
tick uses the global cursor position, avoiding feedback from the moving window.
Physics is suspended while dragging.

`PickupAnimation` drives a small damped pendulum using cursor movement. The cat
tilts behind horizontal movement, hangs its paws down, and continues swinging
briefly when you stop before settling upright. Quick horizontal or vertical motion
smoothly increases dizziness: wobbling head, dizzy eyes, and circling gold stars.
Swing reacts more strongly to direction changes and is capped at 26 degrees.
A permanent transparent margin keeps the backing surface the same size across
idle, pickup, and release, avoiding resize-related flicker. The first pickup pose
matches idle and eases into dangling. Rotated artwork is constrained at monitor
edges so picking up near the bottom does not cut off the feet.

Dizziness starts only after smoothed cursor speed exceeds 1,800 DIPs/second with
enough accumulated intensity (roughly 0.2 seconds of sustained fast movement,
plus speed smoothing). Small/normal motions and isolated spikes remain normal
dangling. A lower 1,200 DIPs/second exit threshold prevents rapid toggling; direction
reversals count toward intensity. Swing response is independent of this gate.
Dizziness holds briefly after fast movement (about 1.2 seconds), then fades over
several seconds while held. Its larger stars and orbit have a dark outline and
white outer border. Sleeping Zs use bold white lettering with the same contrasting
outlines, with staggered upward movement and short fades.

These effects only render while dragging. They do not change throw velocity,
gravity, collisions, or the ordinary idle/walking/sleeping animations. Pickup state
is reset on each grab and release, so effects do not leak into other actions.

`ThrowTracker` retains about 120 ms of cursor history. Releasing estimates velocity
from displacement divided by elapsed time and caps total speed at 2,200 DIPs/sec.
If you hold still before releasing, old samples expire and the pet drops instead
of inheriting an old fast movement. Lost mouse capture cancels the throw safely.

## 6. Behaviors and animation

`PetBehavior` picks an action every 2–6 seconds when the pet is grounded. Walking
is weighted more heavily; sleeping lasts 10 seconds (configurable), then starts a 1.2-second
Waking stretch and two seconds of idle. Jumping gives an upward
impulse. Airborne physics and dragging take priority over autonomous decisions.

`PetRenderer` draws a cat with ears, a face, tail, body, and paws. Animation includes
breathing, blinking, tail sway, walking paw motion, sitting/sleeping poses, looking
eyes, a waking stretch/yawn, and an airborne pose. Sleeping loops floating/fading Zs
and breathing while staying planted. Sleep ends after its configured duration or a direct click; cursor motion and petting do not interrupt it. Animations are
procedural, so there are no missing asset paths or sprite-sheet setup steps.

## 7. Controls and background lifetime

- **Left drag:** pick up and carry the pet, including between monitors.
- **Release while moving:** throw it. Release after holding still to drop it.
- **Right-click the pet:** feed, play, Battle Mode, Do now, emotions, Quiet, sounds, status card, diary, Settings…, More, Exit.
- **Battle Mode** (optional, off by default): right-click → **Battle Mode** opens the battle hub, with a card for each mode (Adventure, Expedition, Infinity) showing your progress. A stage or a new Expedition run opens the same setup panel (weapon cards with stats, Easy/Normal/Hard, how to play); press **Ready** and the cat fights slimes; click a slime to set its target, use skills on the small HUD, lift the cat to rescue it. In Adventure, Expedition and Infinity, five made-up desktop app icons (Tab Tornado, Ping Storm, End Task, Ctrl+Z, Bin) stand above the battlefield and charge up: click a glowing one and its help flies down to the fight. On harder fights a slime may shoot a virus into an icon, which then works against the cat (ads, bells on its head, "Not responding"…) until you click it 3 times or defeat that slime. The result panel keeps your records per difficulty. Enemies, weapons and waves can be customized with a `battle.json` in `%LOCALAPPDATA%\DesktopPet` (see `docs/BATTLE-DATA.md`). See `docs/WINDOWS.md`.
- **Adventure** (Battle Mode hub → Adventure, beta in 1.1.0): chapter 1 "The Slime Invasion", five stages on a map. Each stage opens the setup panel, then a battle with its own goal: clear the waves, or survive 75 s on stage 3. From stage 2, pick one of three upgrade cards after each wave (they last for that run only). Stage 4 brings herb slimes that heal their friends (push one while it winds up); stage 5 is the Slime King, who calls help phase by phase. Winning a stage opens the next one; stages 3 and 5 give the Slime hat and the Slime King's crown, which you can wear from Settings → Cat. Progress is kept in `%LOCALAPPDATA%\DesktopPet\adventure-progress.json` (a battle in progress is never saved). Playing with a custom `battle.json` saves no progress. See `docs/ADVENTURE-UPDATE.md` and `CHANGELOG.md`.
- **Expedition** (Battle Mode hub → Expedition, opens after winning the Slime King; in development): one run over a small map of 5 steps and a boss. **Prepare the run** opens the setup panel to pick the weapon and difficulty for the run. Only the boss is shown at first: at each step press **Spin** and a capsule reveals that step's 2–3 nodes (odds shown; a rest always waits before the boss). Then choose a fight, a harder elite node (winning one offers a relic), a rest (30 % HP back or a card) or an event (trade a card, a slime chest, an old shrine); after each won fight the capsule machine offers a free capsule (a common card or HP) or a gold one for 12 % HP (a relic, a rare card or a card with HP); the cat's HP, upgrade cards and up to 3 relics (each a gain with a price, for that run only) carry from step to step. Leaving a step's battle (End, closing the app) lets you fight it again for 25 % of max HP. A won run gives keepsakes once each (a cup per difficulty, a slime cushion, the Expedition scarf); the cups and cushion stand in a small display corner at the bottom right of the screen (Settings → Pet → Keepsake display corner), and the cat goes to look at them now and then. The run, its records and keepsakes are kept in `%LOCALAPPDATA%\DesktopPet\expedition.json`. See `docs/EXPEDITION-DIRECTION.md` and `docs/BATTLE-DATA.md`.
- **Right-click the tray icon:** pause/resume, Smart pet (AI) on/off, view AI status, Settings…, reset, or exit.
- **Double-click the tray icon:** reset to the primary screen.

**Start with Windows** (off by default) is in **Settings… → General**; **About** is under **More**.
Diagnostics are logged to `%LOCALAPPDATA%\DesktopPet\logs`.
The tray icon may be under the Windows notification area's hidden-icons arrow.
Pause freezes animation, physics, and random actions. You can still reposition the
pet while paused; physics resumes when you unpause. Reset returns it to the floor
at the center of the primary screen. Exit disposes the tray icon and closes the app.

`Topmost` keeps the pet above ordinary windows. Windows secure desktops (such as
UAC), exclusive-fullscreen applications, and competing topmost windows can cover
it. This app does not override those operating-system behaviors. It does not add
itself to Windows startup automatically; that only happens if you enable it.

## 8. Verify

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

These checks cover throw settling, screen containment on a negative-coordinate
monitor, wall reversal, floor and ceiling rebounds, drag isolation, velocity
estimation, stale-sample expiration, and the throw-speed cap. No test packages or
NuGet dependencies are required.

Manual checks for the actual desktop integration:

1. Launch; confirm only the cat and tray icon appear, with no taskbar button.
2. Put another ordinary window behind the cat; verify transparency and topmost.
3. Click beside the silhouette; verify the underlying app receives the click.
4. Drag slowly, then throw quickly upward and sideways; verify bouncing and settling.
5. Hold still during a drag for half a second, then release; verify a simple drop.
6. Observe walking, jumping, sitting, sleeping, and looking over a few minutes.
7. Drag to a second monitor, if available; check its edges and taskbar.
8. Pause/resume, reset, and exit from the tray; verify the process exits cleanly.

## 9. Publish the Windows application

From the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

Double-click `C:\Project VS\Pet Computer\artifacts\win-x64\DesktopPet.exe` from File
Explorer. The Release profile bundles .NET, WPF, Python, and AI files. No Visual
Studio or separately installed runtimes are required to run this output.
Distribute the **whole `win-x64` folder**, not just the executable.
See [the Windows guide](docs/WINDOWS.md) for build commands and dependency details.

## 10. Extend it

- **Tune motion:** change `PetSettings` speed, gravity, bounce, or throw-speed values.
- **Add actions:** extend `PetAction`, choose it in `PetBehavior`, and draw its pose
  in `PetRenderer`. Keep input and physics priorities intact.
- **Add sprite animation:** replace `PetRenderer` with an element that selects
  frames by action, facing, and animation time. Keep frames within the host size.
- **Add pet designs:** introduce a definition containing dimensions, colors or
  sprite paths, and behavior settings; pass it to each pet's components.
- **Add multiple pets:** create one `PetWindow` per pet and have the app's tray
  commands operate on the collection. Each window already owns its own state.
- **Add sounds:** trigger short, rate-limited audio on action transitions or
  collisions. Keep playback separate from the fixed-step physics loop.
- **Add interactions:** add context-menu actions, double-click reactions, or
  persistent settings without changing the physics implementation.

Start with this small separation of concerns; add interfaces or a larger entity
system only when the features actually require them.

## License

The code and artwork are under the [MIT License](LICENSE). The bundled sounds come from
CC0 recordings (see [src/DesktopPet/Assets/Sounds/CREDITS.md](src/DesktopPet/Assets/Sounds/CREDITS.md));
the published app also includes the .NET runtime and the embeddable Python runtime under
their own licenses (`python\LICENSE.txt` in the release folder).
