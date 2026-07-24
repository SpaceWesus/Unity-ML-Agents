# Weapons Testing Combat Lab

`Assets/Scenes/Weapons Testing.unity` is Turtle's persistent, production-oriented
combat laboratory. It succeeds the Homies arena as the primary place to develop
player-versus-AI, AI-versus-AI, weapons, skills, classes, animation, and combat
feedback.

## Design rule: the player is an agent

The player and NPC hunters use the same `Combatant`, movement, weapon moveset,
damage, animation, and feedback code. The only difference is the component that
produces a `CombatCommand`:

- `PlayerCombatCommandSource` translates local Input System input.
- `AiCombatCommandSource` makes throttled combat decisions.
- Both feed `CombatAgentDriver`, which simulates the same `Combatant`.

Do not add player-only combat powers. A new capability belongs on the shared
combatant/ability surface and may be invoked by any command source.

## Authoring workflow

Use the Unity menu:

- `Turtle > Combat > Open Weapons Testing`
- `Turtle > Combat > Create Weapons Testing (if missing)`
- `Turtle > Combat > Rebuild Weapons Testing`
- `Turtle > Combat > Validate Weapons Testing`

The level, agents, spawn points, and target dummies are serialized in the scene
and remain visible and editable outside Play Mode. Rebuild is explicit and
warns before replacing scene edits.

The scene is intentionally not placed in release Build Settings. It is a
development laboratory, not a shipping startup scene.

## Runtime controls

- WASD: move
- Mouse: orbit camera
- Left mouse: light greatsword attack
- Right mouse: heavy greatsword attack
- Space or Left Shift: dodge
- F1: reset the arena
- F2: pause or resume AI command drivers

## Extension points

- Add a weapon by creating a `WeaponMoveSetDefinition` and an animation
  controller (or override controller) that exposes the named action states.
- Add a skill by extending `CombatAction` and implementing the shared execution
  path on `Combatant`; expose it to both human and AI command sources.
- Add tactical AI by replacing or extending `AiCombatCommandSource`. Keep its
  decision cadence throttled and continue emitting the same command contract.
- Add gear progression by assigning different move sets and presentation assets
  to individual combatants. Stats and move sets should remain authored data.
- Replace the current registry scan with a spatial query service when profiling
  demonstrates that representative agent counts require it.

## Current prototype boundaries

The first move set is a two-handed training greatsword with light attack, heavy
attack, dodge, hit reaction, knockdown, pooled blood impact particles, weapon
blood drips, knockback, and automatic laboratory respawn. The architecture is
ready for additional movesets and skills; those content sets are intentionally
not fabricated before their intended combat designs are chosen.
