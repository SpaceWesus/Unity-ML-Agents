# Battle Scale Lab

`Battle Scale Test` is a rendered combat-capacity laboratory. It answers two
separate questions:

1. How many visible, fully simulated combatants can the current combat stack
   sustain?
2. Does hierarchical command make large fights readable without turning every
   agent into a perfectly synchronized hive mind?

The arena, camera, deployment guides, unit template, and system references are
authored into `Assets/Scenes/Battle Scale Test.unity`. Armies are intentionally
pooled at runtime because the selected population is a test variable. Pressing
Play does not rebuild the level.

## Running the test

Open `Battle Scale Test` and press Play. The upper-left HUD provides manual
presets from 25v25 through 800v800, a reset button, a physics-fidelity toggle,
and an automatic limit test.

The automatic test spends 3 unmeasured seconds warming each preset and then
samples it for 9 seconds. It advances while sampled FPS remains at or above the
configured target. Use the result as a comparative Editor diagnostic. A
development-player build and Unity Profiler capture are still required before
setting a production population budget.

`PHYSICS: SCALE LOD` uses kinematic bodies and trigger hurtboxes. It preserves
the shared cast-to-hurtbox damage contract while avoiding expensive crowd
collision resolution. `PHYSICS: FULL` rebuilds the selected battle with dynamic
bodies and solid hurtboxes to expose the higher-fidelity cost.

## Command model

- Each team has one commander. It periodically selects Advance, Hold, Flank, or
  Rally from force strength and battle phase.
- Every ten agents form a squad. A gold marker identifies its sergeant.
- The sergeant projects the command into a squad anchor, selects a local focus
  target, and provides a small nearby cohesion bonus.
- When a sergeant dies, the squad promotes a living member.
- Individual discipline controls how strongly an agent follows the focus target
  and formation. Nearby threats can override the order, and healers independently
  rescue injured squadmates.

This produces RTS-level intent with autobattler execution. Command remains
influence rather than mind control, matching the hunter-agent vision.

## What is measured

- Rendered and living units
- Editor-local FPS and frame time
- Unity allocated memory
- AI decisions, attack attempts, and confirmed hits per second
- Active squads, commander decisions, and sergeant promotions

Units reuse `RaidAgent2D`, `RaidCombatPhysics2D`, and
`DungeonRaidDirector2D.ResolveBasicAttack`. A displayed hit therefore requires
the same 2D cast/hurtbox contact as the dungeon prototype. The first version uses
basic role stat profiles plus healer support and a sergeant aura; production
ability scheduling and VFX are deliberately deferred so the baseline simulation
cost remains legible.

## Editor tools

- `Turtle > Battle Scale > Setup Battle Scale Test`
- `Turtle > Battle Scale > Validate Battle Scale Test`
- `Turtle > Battle Scale > Run 100v100 Smoke Test`

The setup tool is safe to rerun and only updates named Battle Scale Lab objects.
The scene remains outside Build Settings because it is a development benchmark,
not a shipping level.
