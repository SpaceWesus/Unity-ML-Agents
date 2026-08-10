# Battle Test: Three-Squad Survival

`Assets/Scenes/Battle Test.unity` is the detailed mass-combat stress scenario.
It complements `Battle Scale Test` rather than replacing it:

- **Battle Scale Test** measures symmetric baseline population capacity.
- **Battle Test** measures a production-shaped workload: unique hunters,
  escalating rounds, mixed monsters, abilities, status effects, AOE, VFX,
  squad tactics, and individual telemetry.

The arena and all thirty hunters are authored into the scene and remain
visible in Edit Mode. The variable monster horde is pooled at runtime because
round population is dynamic. The pool is prewarmed before round one to prevent
`Instantiate`/`Destroy` churn from contaminating wave measurements.

## Scenario

Three ten-hunter squads defend a central rally zone from four horde gates:

- **Aegis** emphasizes protection, control, and stable formation.
- **Ember** emphasizes aggressive pressure and flexible hybrid damage.
- **Vanguard** emphasizes mobility, rescue, and ranged coverage.

Each squad begins with one sergeant and one primary Tank, Fighter, Healer, Mage,
Ranger, and Assassin contribution. Those are roles rather than locks: examples
include a Tanker/Healer, Healer/Fighter, Ranger/Combat Medic, Mage/Spellblade,
and Assassin/Hexblade. All thirty hunters have unique stats, personality
weights, trait descriptions, and three-ability combinations.

The setup tool writes those defaults into the scene. After setup, each hunter's
serialized `RaidAgent2D` abilities/stats and `BattleSurvivalUnit2D` personality
weights can be edited directly in the Inspector; runtime restart consumes those
authored values instead of rebuilding the catalog.

## Hierarchical AI

The coalition director samples pressure at the West, East, North, and South
gates. It assigns the healthiest squads to the most threatened sectors, orders
weakened squads to regroup, and redirects a capable squad when another squad has
downed members.

Each sergeant converts that macro order into a squad anchor and priority target.
Sergeants provide a small local cohesion bonus and are replaced by a living
squadmate if killed. Individual aggression, cohesion, and support weights decide
how closely each hunter obeys the formation, chases local threats, changes
targets, or prioritizes support. Command is influence, not mind control.

## Horde escalation

Every round increases:

- total monster quota;
- maximum concurrent monsters;
- monster health by a compounding multiplier;
- monster damage by a smaller compounding multiplier;
- monster speed up to a capped bonus;
- elite frequency.

The population and stat-growth constants are serialized under **Round Scaling**
on `BattleSurvivalDirector2D`, so designers can tune the pressure curve without
editing source code. The defaults produce `24 + 11r + 2r^2` total monsters,
`1.17^(r-1)` health, and `1.075^(r-1)` damage for round `r`.

The horde mixes Ravagers, Brutes, Spitters, Hexers, Chargers, and Elites.
Specialists exercise AOE slams, damage-over-time acid, stuns, chain attacks,
charges, and elite area pressure. Hunters exercise heals, revives, temporary
Tanker shields, taunts, buffs, vulnerability marks, fire fields, freezing,
chain attacks, piercing shots, teleports, executes, and close-range AOE.

## Developer HUD

The HUD shows round state, population, wave progress, strength multipliers, FPS,
frame time, memory, decisions/attacks/hits/abilities per second, AOE count,
active status effects, peak population, gate pressure, and recent events.

Three bottom squad cards expose every hunter's health, mana, and three cooldown
states. Click a hunter to inspect their build, traits, objective, kills, damage,
casts, cooldown names, and active statuses. Controls support 0.25x, 0.5x, 1x,
2x, restart, `STRESS +220`, and a five-round threat jump. `STRESS +220` bursts
the added bodies from the prewarmed pool over a few frames, making it a genuine
population-spike test without introducing runtime instantiation noise.

The automated smoke path requires at least 200 simultaneous monsters alongside
the thirty hunters before it can pass.

## Shared combat authority

The scenario does not own a second damage or ability model. It reuses:

- `RaidAgent2D` for stats, cooldowns, mana, life state, status timers, movement,
  hurtboxes, and pooled reset;
- `RaidCombatPhysics2D` for cast-to-hurtbox contact;
- `DungeonRaidDirector2D` for ability scoring/resolution and persistent fields;
- `RaidFxPool2D` for bounded projectiles, arcs, bursts, and combat text.

`DungeonRaidDirector2D.BeginExternalCombat` and `StepExternalCombat` let a
disabled dungeon objective component resolve combat against the survival
director's roster and clock without running dungeon progression.

## Editor tools

- `Turtle > Battle Survival > Setup Battle Test`
- `Turtle > Battle Survival > Validate Battle Test`
- `Turtle > Battle Survival > Run Survival Smoke Test`

The scene is intentionally excluded from Build Settings. Editor FPS is useful
for comparisons, but a development-player build and Profiler capture on target
hardware are required before selecting a production population budget.
