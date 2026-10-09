# Pet emotions

The pet now has a visual emotion layer separate from its movement action. A pet
can walk while sad or idle while happy; emotions do not replace dragging, physics,
or the existing action/animation system. No LLM or additional dependencies are used.

## Try the reactions

| Emotion | Trigger | Visual reaction / duration |
|---|---|---|
| Happy | A click, gentle release, or petting | Happy eyes, smile, small visual bounce, outlined pink heart; about 3 seconds |
| Angry | Four clicks within 2 seconds, or a throw at 1,600 DIPs/sec or more | Frown, angled brows, tiny shake, outlined red anger symbol; about 4 seconds |
| Sad | No attention for 5 minutes | Worried brows, frown, outlined blue tear; walking slows to 65% until attention returns |
| Surprised | Cursor moving at least 1,600 DIPs/sec within 140 DIPs of the pet's center | Wide eyes, open mouth, outlined exclamation mark; about 1.1 seconds |
| Sleepy | No attention for 2 minutes, or the existing Sleeping action | Drooping/closed eyes, yawn, high-contrast floating Zs; grounded movement stops |
| Dizzy | The existing high-speed pickup detector | Existing wobble, orbit, stars, dizzy eyes; same raised threshold, intensity buffer, hold and fade |

To **pet** it, move the cursor gently back and forth over the character without
holding a button for about 0.65 seconds. Strokes must stay between 15 and 450 DIPs
per second. Leaving the character or moving too fast resets the stroke. A parked
cursor does not continually award attention. Petting cannot wake a sleeping pet.

Left-click gives attention; holding and moving 6 DIPs begins pickup. A quick click
does not trigger throw physics. Right-click gives attention and opens the
existing context menu. Dragging a sleepy pet immediately uses the normal drag state.

## Priority and timing

- While picked up, ordinary emotional faces, symbols, and body motions are hidden.
  Only the existing high-threshold Dizzy effect can accompany dangling.
- Throwing, jumping, and waking suppress ordinary reactions until those actions
  complete. Emotions never alter airborne velocity, gravity, or collisions.
- Sleepy enters Sleeping with a reaction lock. Cursor movement, petting, neglect
  moods, pending reactions, AI hints, and AI commands cannot interrupt sleep.
  Sleep ends after `PetSettings.SleepDurationSeconds` (10 seconds by default),
  or immediately on a direct left/right click. Waking clears Zs and resets
  inactivity so the pet does not immediately fall asleep again.
  Left-click can pick up and drag immediately; right-click starts Waking.
  Pause freezes the sleep clock; AI toggles do not reset it.
- Sleepy, Surprise, and Angry stop grounded autonomous movement and reject AI
  movement commands, including during expression fades. Sleepy holds an idle or
  sleeping pose; inactivity Sleepy smoothly lowers the head and settles the body
  into the same compact resting pose, with gentle breathing. No walking animation
  plays underneath it. When a reaction ends,
  the appropriate underlying mood returns. Only one expression is drawn at a time.
- Happy and Angry suppress incidental cursor surprise until their reaction ends.
  Positive attention clears sadness; anger gets time to finish before another smile.
- Happy has a 2-second retrigger cooldown, anger and surprise have 6-second
  cooldowns, and petting has a 3-second cooldown. Anger cannot be extended forever
  by rapidly clicking during its cooldown.
- Faces fade out and in over a few tenths of a second. Happy bouncing and angry
  shaking are drawing transforms, not changes to the model's desktop position.
- The inactivity timer measures unpaused pet-session time since attention to this
  pet, not the user's total keyboard/mouse activity. Pausing freezes emotion timers.
  Reset clears the current mood and inactivity history.

Heart, anger, surprise, tear, Zs, and dizzy indicators use contrasting light/dark
borders. The permanent transparent margin remains in place to avoid pickup flicker.

## Code and Python integration

`PetEmotions.cs` owns immediate interaction reactions, cooldowns, smooth expression
strength, and the offline inactivity fallback. `PetWindow` forwards mouse events
and cursor samples. `PetRenderer` draws the expressions. `PetBehavior` applies a
bounded speed modifier to sad walking and enforces emotion movement locks.

The C# observation adds an optional `emotion` string (`Neutral`, `Happy`, `Angry`,
`Sad`, `Surprised`, `Sleepy`, `Dizzy`). Python uses Angry/Surprised context to prefer
a calm sitting action, and returns an optional long-term `emotion` hint:
`neutral`, `sleepy`, or `sad`, derived from the attention timer. Random action
selection does not trigger random happy/angry emotions.

C# validates hints and rejects stale sadness/sleepiness after fresh attention.
Immediate reactions remain local and responsive during IPC timeouts. If Python
is unavailable, the same deterministic reactions and inactivity fallback continue.
Protocol version 1 remains compatible: the new observation field has a default,
and replies without an emotion hint are still accepted.

Add future long-term AI/LLM emotion policies behind this optional hint field; keep
drag priority and reaction cooldowns in the C# engine. Short-lived reactions are
session state, not new persistent memory records.

## Run and verify

Launch `artifacts\win-x64\DesktopPet.exe`. Close the old pet before opening an
updated published copy. Exit via the cat or tray context menu as before.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Ui
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

Tests cover triggers, cooldowns, stale hints, gentle petting, input priority,
movement scaling, emotion fades, and real WPF click handlers. A visual comparison
is written to `artifacts\visual-review\emotions.png`, showing all six emotions
on light, dark, and colorful backgrounds.
