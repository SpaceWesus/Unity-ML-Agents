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

Last updated for the v5 spatial 2D Ecosystem working tree: 2026-07-26.
Baseline commit: `16f1653570783bcc1c3a0ead3bbaf8dd87533a6d`.

<!-- unity-onboarding:generated:end -->
