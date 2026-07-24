# Turtle Unity Project Context

<!-- unity-onboarding:generated:start -->

## Summary

Turtle is a Unity experimentation project containing ML-Agents turtle/racecar work and several independent scenes. `Assets/Scenes/Homies.unity` is the combat laboratory for Monarch Protocol. `Assets/Scenes/Ecosystem Slice.unity` is a persistent living-world vertical slice for hunters, guilds, gear movesets, autonomous missions, relationships, memories, and save/load.

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
- Ecosystem domain models and utility simulation are plain serializable C# under
  `Assets/Scripts/Ecosystem`; scene presentation and input are separate
  MonoBehaviours.
- Ecosystem gear is authored as `EcosystemGearDefinition` ScriptableObjects
  under `Assets/Data/Ecosystem`.
- Long-term design direction is recorded in `Docs/Game-Vision.md`: persistent unique hunters, reciprocal player/AI agency, gear-granted movesets, guild politics, memories, and autonomous world activity.
- No first-party `.asmdef` boundary is present; scripts compile into `Assembly-CSharp`.
- Vendor/sample content lives under `Assets/PROMETEO - Car Controller`, `Assets/Samples`, and embedded packages.

## Scenes and startup

- Build Settings enable `Assets/Scenes/Turtle Scene.unity` and
  `Assets/Scenes/Ecosystem Slice.unity`.
- Other first-party scenes include Homies, Drone Swarm, racecar variants, and the racetrack.
- Homies starts its feature only when the active scene name is `Homies`; it does not alter other scene startup.
- Static Homies dungeon dressing and the complete Ecosystem Slice environment
  are serialized in their scene assets. Runtime generation is reserved for
  dynamic actors and simulation-driven state.
- Ecosystem world state saves to
  `Application.persistentDataPath/ecosystem-slice-v1.json`.

## Tooling and constraints

- The official Unity AI Assistant package is installed, but no Unity MCP capabilities were exposed to this Codex task.
- Repository inspection and local Unity compilation are available.
- Preserve independent ML-Agents and car-controller experiments.

## Sources and confidence

Confirmed from `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset`, `README.md`, representative first-party scripts, `Assets/Scenes/Homies.unity`, and `Assets/Scenes/Ecosystem Slice.unity`.

Last analyzed: commit `16f1653570783bcc1c3a0ead3bbaf8dd87533a6d`, 2026-07-23.

<!-- unity-onboarding:generated:end -->
