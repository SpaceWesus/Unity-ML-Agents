# Turtle Unity Project Context

<!-- unity-onboarding:generated:start -->

## Summary

Turtle is a Unity experimentation project containing ML-Agents turtle/racecar work and several independent scenes. `Assets/Scenes/Homies.unity` is the original combat laboratory for Monarch Protocol. `Assets/Scenes/2D Ecosystem.unity` is the serious persistent living-world slice for hunters, guilds, facilities, gate contracts, population churn, gear movesets, autonomous missions, relationships, memories, save/load, and the shared Hunter Career Loop. `Assets/Scenes/Weapons Testing.unity` is the production-oriented successor to Homies for combat development.

## Confirmed environment

- Unity `6000.3.5f2` (`ProjectSettings/ProjectVersion.txt`)
- Universal Render Pipeline 17.3.0
- Input System 1.17.0 with both old and new input backends enabled
- Important packages: ML-Agents 4.0.2, AI Navigation 2.0.9, Entities 1.4.6, Splines 2.8.4, Cesium 1.23.0
- Unity Test Framework 1.6.0 is installed; no first-party test assembly was found

## Structure and architecture

- First-party runtime scripts are under `Assets/Scripts`.
- Existing Turtle and racecar scenes use scene-bound MonoBehaviours and ML-Agents.
- Homies gameplay uses small, composed MonoBehaviours under `Assets/Scripts/Homies`.
- Weapons Testing uses shared `Turtle.Combat` capabilities for human- and
  AI-commanded hunters. Player and AI differ only by command source.
- Ecosystem domain models and utility simulation are plain serializable C# under
  `Assets/Scripts/Ecosystem`; scene presentation and input are separate
  MonoBehaviours.
- Ecosystem career state persists overflow-safe XP, career level, saved and
  invested Ability Points, attribute investments, learned ability IDs, and a
  bounded loadout of three cooldown abilities, one ultimate, and two passives.
  Official rank uses six-point invested-AP bands; equipment and saved AP do not
  affect it. Hybrid class labels are derived descriptions rather than locks.
- Player and NPC career changes use the same action requests and validation.
  Autonomous career planning runs as a separate deterministic simulation phase
  so saving, spending, learning, and equipping remain inspectable.
- The ecosystem currently owns ability learning and loadout state, not rendered
  ability execution. A later combat adapter must resolve those stable IDs into
  real-time behavior without creating separate progression state.
- Ecosystem gear is authored as `EcosystemGearDefinition` ScriptableObjects
  under `Assets/Data/Ecosystem`.
- `Assets/ExplosiveLLC` contains the RPG Character Mecanim animation pack.
  Its materials are URP-converted, but its legacy Input Manager demo controller
  is not part of Turtle's gameplay architecture.
- The authoritative product design is `Docs/Game-Vision.md`: an emergent
  story-generating hunter world with reciprocal player/AI agency, persistent
  identity, hybrid builds, gate auctions, shared-time simulation, combat LOD,
  guild politics, knowledge limits, injury, death, and succession.
- No first-party `.asmdef` boundary is present; scripts compile into `Assembly-CSharp`.
- Vendor/sample content lives under `Assets/PROMETEO - Car Controller`, `Assets/Samples`, and embedded packages.

## Scenes and startup

- `Assets/Scenes/3D Test Arena.unity` is the perspective 3D counterpart to the
  detailed Battle Test. Its arena architecture, four glowing horde gates,
  central command dais, walls, cover, lighting, post-processing, baked 3D
  NavMesh, three squad rally points, and thirty imported humanoid hunters are
  serialized and visible outside Play Mode. The 220-monster opening wave uses a
  360-body prewarmed pool, lightweight procedural monster views, pooled
  projectiles/VFX, collider-authoritative melee/projectile/AOE contact, the same
  thirty unique hunter profiles and ninety ability definitions, and a
  commander -> sergeant -> individual decision hierarchy. The fixture includes
  a responsive spectator HUD/camera and remains outside Build Settings. Its six
  monster archetypes now re-pose one shared procedural rig into distinct pooled
  silhouettes, while a 64-object world-space ring pool makes AOE, support, and
  heavy-attack footprints readable without affecting combat authority. The
  two-generation soak test verifies 30 + 220 sustained combat and exact
  monster/projectile/telegraph pool reuse across restart.
- `Assets/Scenes/Battle Test.unity` is the detailed three-squad horde-survival
  stress fixture. Its 120x72 arena and thirty unique hunters persist in Edit
  Mode; up to 600 escalating monsters are prewarmed and pooled at runtime. A
  coalition commander assigns threatened gates, squad sergeants translate those
  orders into anchors and focus targets, and individual aggression/cohesion/
  support weights preserve local autonomy. The fixture exercises the shared
  contact combat, cooldown, mana, status, AOE, persistent-field, and VFX stack
  while exposing round, squad, individual, and performance telemetry. It is
  intentionally excluded from Build Settings.
- `Assets/Scenes/Battle Scale Test.unity` is the rendered mass-combat laboratory.
  Its arena and references persist in Edit Mode; variable armies are pooled at
  runtime. It supports 25v25 through 800v800 presets, a comparative auto-limit
  benchmark, scale/full physics modes, and commander -> sergeant -> individual
  AI. The scene is intentionally excluded from Build Settings.
- Build Settings enable `Assets/Scenes/Turtle Scene.unity` and the ecosystem
  scenes; the v4 editor builder ensures `Assets/Scenes/2D Ecosystem.unity` is included.
- Other first-party scenes include Homies, Drone Swarm, racecar variants, and the racetrack.
- Homies starts its feature only when the active scene name is `Homies`; it does not alter other scene startup.
- Static Homies dungeon dressing and the 2D Ecosystem's camera, host, view, and
  catalog references are serialized in their scene assets. Runtime generation
  is reserved for dynamic actors and projections of simulation-driven state.
- The Weapons Testing arena, spawn points, humanoid agents, and target dummies
  are serialized in the scene. Its editor builder runs only from explicit
  `Turtle > Combat` menu commands.
- `Assets/Scenes/Demo Dungeon.unity` is the standalone six-hunter autonomous
  raid combat fixture. Its actors, walls, chest, room graph, and system
  references are serialized before Play Mode. Actors use dynamic `Rigidbody2D`
  bodies and solid `CircleCollider2D` hurtboxes; walls use `BoxCollider2D`, and
  damage is authorized by swept/overlap physics contacts rather than range
  checks. The fixture exercises twelve role abilities and hunter/enemy tactical
  directors. It is not a second persistence model: campaign integration must
  materialize and commit the existing `GateInstanceState` and
  `DungeonEncounterState`.
- The Demo Dungeon combat fixture now contains three ordered goblin encounter
  pods (6, 6, and 7 members) followed by a distinct Goblin Warlord boss. The
  Warlord has a larger hurtbox, 850 health, cleave, chain-damage, and roar
  abilities. The full fixture is 26 visible combatants and completes without
  player input.
- Demo Dungeon hunters begin with zero shield HP. Temporary shield HP is an
  explicit hunter Tanker ability: Bulwark may be cast during an engagement,
  affects the Tanker and nearby party members, and expires after 12 simulated
  seconds. Non-tanker and monster abilities cannot grant shield HP.
- `RaidPlaybackController2D` makes the fixture start at 0.25x global time while
  presenting 0.25x, 0.5x, 1x, and 2x developer controls. It scales
  the physics step with slow motion and restores the prior time settings when
  disabled. The autonomous smoke harness temporarily overrides it to 4x.
- `RaidHud2D` draws camera-projected status timers for visible living combatants
  while a bottom-left strike-team panel keeps all hunter names, health, mana, and
  ability readiness readable during clumped combat. These are read-only
  projections of `RaidAgent2D`'s status and cooldown state. Pod and party defeat
  checks use damageable survivors
  rather than `CanAct`, preventing a stun from completing or failing the raid;
  boss completion additionally requires the Warlord to be dead at zero health.
- Demo Dungeon now includes a serialized room-first preview generated from seed
  `731245`. `DungeonRoomFirstPlanner2D` deterministically creates 8-12 rooms,
  graph-diameter entrance/boss placement, branches, optional loops, semantic
  side rooms, varied spatial templates, and bent corridors.
  `DungeonRoomFirstGenerator2D` materializes floors, collidable walls, props,
  sockets, and raid placement. The standalone demo rolls a new seed on Play for
  rapid variety testing; campaign gates will provide their persisted map seed.
- Generated Demo Dungeon geometry bakes a shared `DungeonNavigationGrid2D` from
  rooms, corridor routes, and inflated `BoxCollider2D` blockers. Every hunter
  and monster receives that same navigation reference through `RaidAgent2D`,
  while dynamic `Rigidbody2D` movement and local casts remain the final collision
  authority. Unity AI Navigation remains installed for 3D scenes; its stock
  surface collector does not consume this fixture's `Collider2D` geometry.
- The 2D navigation bake uses explicit XY bounds for freshly generated
  `BoxCollider2D` walls and obstacles, audits connectivity to every room, and
  rebakes one physics step after runtime regeneration. Same-faction physical
  collision is ignored to prevent doorway jams while colliders remain queryable
  hurtboxes and retain solid world collision. Corridor progression uses a
  living-party quorum, so one delayed hunter cannot freeze the entire raid.
- Ecosystem world state saves to
  `Application.persistentDataPath/2d-ecosystem-v5.json`; sibling v4, v3, v2, and
  v1 files are retained as read-only import sources and are never overwritten
  by migration.
- Gate contracts own deterministic, persisted dungeon manifests before they are
  observed. Active runs use fixed-step, renderer-independent encounter snapshots,
  so offscreen resolution and a materialized top-down dungeon share positions,
  vitals, targets, loot, hazards, and outcomes.
- The 2D Ecosystem scene serializes its terrain, roads, locations, gate landmarks,
  one hundred reusable hunter pawns, and an inactive dungeon materialization stage.
  Play Mode projects save state into those authored objects rather than rebuilding
  the map.

## Tooling and constraints

- The official Unity AI Assistant/MCP package is installed and its Editor bridge is
  live, but this Codex task is not connected because the user-level Codex MCP entry
  still points at an inactive legacy HTTP endpoint.
- Repository inspection and local Unity compilation are available.
- `Turtle > Ecosystem > Validate Guild Ecosystem Prototype` includes
  deterministic serious-scale population and churn, spatial scene wiring, XP/AP,
  six-point rank boundaries, hybrid builds, ability/loadout, shared-command,
  NPC career-planning, deterministic gate manifests and encounters, legacy import,
  round-trip, and continuation checks.
- Preserve independent ML-Agents and car-controller experiments.

## Sources and confidence

Confirmed from `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset`, `README.md`, representative first-party scripts, `Assets/Scenes/Homies.unity`, and `Assets/Scenes/2D Ecosystem.unity`.

Last updated for the persistent 3D Test Arena mass-combat fixture: 2026-08-10.
Baseline commit: `16f1653570783bcc1c3a0ead3bbaf8dd87533a6d`.

<!-- unity-onboarding:generated:end -->
