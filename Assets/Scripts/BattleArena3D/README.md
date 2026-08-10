# 3D Test Arena

`Assets/Scenes/3D Test Arena.unity` is the 3D counterpart to the detailed 2D
Battle Test. It is a development fixture for measuring a production-shaped
mass-combat workload while judging whether an individual hunter remains
interesting to watch.

## Authored scene

The arena, four horde gates, cover, walls, central command dais, lighting,
post-processing, baked NavMesh, three squad rally points, and all thirty
hunters are serialized in the scene and remain visible outside Play Mode.
Only the variable horde, projectiles, world-space combat rings, and short-lived
effects are pooled at runtime.

The arena builder updates only these named roots:

- `3D Arena Environment`
- `3D Battle Hunters`
- `3D Battle Runtime`
- `3D Battle Templates`
- `3D Battle Systems`

It preserves unrelated scene roots and deliberately leaves the test fixture
outside release Build Settings.

## Battle population

Round one contains a quota of 220 monsters with a 220-monster concurrent cap.
The pool holds 360 monsters, letting later rounds and the `STRESS +220` control
increase live pressure without runtime `Instantiate`/`Destroy` churn. The
automated smoke test cannot pass until at least 200 monsters are active beside
all thirty persistent hunters.

The horde mixes Ravagers, Brutes, Spitters, Hexers, Chargers, and Elites. Each
archetype has different movement, health, range, target preferences, scale,
color, special attacks, and a complete pooled silhouette pose. Ravagers are
lean and claw-heavy, Brutes are broad, Spitters crouch around a large throat
core, Hexers are tall and crowned, Chargers lean into forward horns, and Elites
combine the largest profile with an oversized core. The right claw doubles as
the procedural attack limb, avoiding a redundant renderer on every pooled
monster.

Hunters use the imported RPG humanoid, greatsword animations, and role-readable
weapons/crystals. A 64-object `LineRenderer` pool draws expanding ability
footprints, support pulses, and Brute/Elite heavy-attack warnings without
changing combat authority. If that cosmetic pool is ever exhausted, the cue is
dropped rather than instantiated during battle.

Slash streaks, deaths, impacts, blood, shield contacts, projectiles, and warning
rings also use authored shared systems or bounded pools. They are never created
per hit or per combatant during the live 250-agent workload.

## Combat and tactics

The dimension-neutral `RaidAbilitySpec` data and the same thirty unique hunter
profiles used by Battle Test feed the 3D runtime. That preserves every hunter's
name, hybrid build, stats, personality weights, and three-ability combination.

`BattleArena3DDirector` owns combat authority and staggers decisions through a
bounded per-frame budget. A coalition commander samples pressure at all four
gates. It assigns squad sectors, orders damaged squads to regroup, promotes a
living field leader after a sergeant falls, and lets aggression/cohesion/support
weights influence each hunter's local behavior.

Damage is not awarded from visual range alone:

- melee uses an oriented 3D overlap volume at the moment the swing reaches its
  contact window;
- projectiles sweep their traveled capsule through `Physics` and can hit walls;
- AOE abilities query actual 3D hurtbox overlap;
- same-faction contacts are rejected.

The first slice supports direct attacks, projectile splash, heal/revive, area
heal, persistent healing fields, temporary Tanker shields, taunts, dash and
shadow strikes, execute, ally empowerment, burning, freezing, vulnerability,
chain damage, and piercing attacks. Frequent projectiles and particles are
bounded and prewarmed. Restart clears active projectiles, particles, and combat
rings before resetting units, preventing effects from one generation of the
battle from leaking into the next.

## Combat feedback and accessibility

`BattleArena3DCombatFeedback` is the single presentation-only consumer of
confirmed combat contacts. It preserves the physical overlap/projectile damage
rules while adding shared slash and death particles, shield-contact feedback,
and camera impulses for selected, heavy, shield-breaking, high-damage, and fatal
events. Shield-only contacts remain visible even when no health damage passes
through. Basic melee does not emit a second duplicate impact at the same contact.

Damage labels use a fixed 48-slot buffer and aggregate rapid hits against the
same target. The contextual mode favors the inspected combatant, shield breaks,
downs, deaths, and unusually large hits instead of attempting to label every
contact in the horde. One shared ring follows the selected unit; no selection
object is added to every hunter or monster. Camera feedback is impulse-based and
bounded, with no global hit-stop that could distort the simulation.

Press `F1` to open the accessibility drawer. Its settings are remembered between
sessions and include:

- 100%, 125%, or 150% HUD scale;
- contextual, selected-only, or all world health bars;
- full, reduced, or minimal cosmetic effect density;
- full, reduced, or disabled camera motion;
- contextual, all, or disabled damage numbers;
- high-contrast hunter/monster colors;
- reduced procedural motion while retaining combat telegraphs.

Gameplay-readable warnings are retained at minimal effects. The HUD also exposes
numeric vitals, text cooldown readiness/timers, and active status names so color
and tiny world-space pips are not the only sources of combat information.

## Spectator controls

- `WASD`: pan over the arena
- `Q` / `E`: orbit left/right
- `R` / `F`: move camera closer/farther
- Right drag: free orbit
- Middle drag: pan
- Left click: inspect a combatant
- `Tab` / `Shift+Tab`: cycle forward/backward through hunters
- `C`: center on and toggle follow for the selected hunter
- `Space` or `P`: pause/resume the simulation
- `F1`: accessibility and controls drawer
- `Home`: reset spectator camera

The HUD offers 0.25x, 0.5x, 1x, and 2x speed, restart, `STRESS +220`, and a
five-round threat jump. It displays round population, scaling, FPS/frame time,
memory, AI/combat rates, peak combatants, AOE/status counts, squad orders,
hunter vitals/cooldowns, and the selected combatant's personal statistics.
Pointer input over either HUD is consumed by the interface instead of selecting
a unit behind it.

## Editor tools

- `Turtle > Battle Arena 3D > Setup 3D Test Arena`
- `Turtle > Battle Arena 3D > Validate 3D Test Arena`
- `Turtle > Battle Arena 3D > Run 3D Battle Smoke Test`
- `Turtle > Battle Arena 3D > Run 3D Battle Soak Test`

The smoke test restores the default presentation, exercises pause/resume, hunter
cycling, camera focus, and selection, then requires combat rings, confirmed
contacts, camera impulses, contextual damage labels, death bursts, and the shared
selection marker before capturing the open accessibility drawer.

The soak test runs two separate 30-hunter + 220-monster deployments and sustains
each for eight simulated combat seconds. Generation one uses presentation
defaults. Generation two runs at 125% HUD scale with minimal effects, disabled
camera motion, selected-only world bars, high-contrast factions, and reduced
motion; it must still produce combat, contextual labels, and telegraphs while
emitting no camera impulses. The test rejects runtime errors or telegraph-pool
exhaustion and verifies that the exact monster, projectile, and telegraph instance
sets survive restart unchanged.

Editor FPS is useful for comparative iteration, but it is not a production
population budget. Capture a development-player Profiler trace on target
hardware before committing to a final active-combatant cap.
