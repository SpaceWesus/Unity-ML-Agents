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

## Combat contact contract

Damage is contact-authoritative and uses a Smash-style separation between
damageable body volumes and move-specific offensive volumes:

- `CombatWeaponHitbox` evaluates the move's normalized hitbox timeline. Each
  entry has a start/end percentage, local center, box size, and orientation.
  A box exists only inside its authored animation window.
- Active boxes use oriented overlaps plus swept box casts. Rotational movement
  is sampled between rendered poses because Unity box casts sweep translation,
  not rotation.
- `CombatHurtbox` marks the body volume that may receive a hit.
- Startup and recovery frames cannot deal damage. A dodge makes its owner
  intangible only during its configured invulnerability window.
- One attack activation can damage every contacted hurtbox owner once. A wide
  swing may hit multiple enemies, while repeated overlap with the same enemy
  does not deal duplicate damage. A miss, successful dodge, future block, or
  interruption before contact deals no damage.
- `AttackDefinition.range` and `arc` are AI planning data, not damage checks.
- Each attack also declares its action policy. `Mobile` attacks preserve
  command-driven movement while their attack animation remains active;
  `Anchored` attacks lock locomotion. `DodgeAllowed` attacks can be cancelled
  immediately into a dodge by either a player or AI agent. These enum defaults
  intentionally make newly-authored attacks mobile and dodge-cancellable unless
  a move is explicitly designed as a commitment.

The player hit marker listens to the same confirmed-contact event used by the
combat system; it never appears for a proximity test or a whiff.

The combat lab also includes the shared player/AI ability system documented in
`Docs/Combat-Abilities.md`. Use number keys `1`, `2`, and `3` for cooldown
abilities and `4` for the equipped ultimate.

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
  Author the move's `hitboxWindows` against normalized animation time; do not
  enlarge a persistent weapon collider to compensate for missing phases.
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
blood drips, knockback, confirmed-hit marker, directional dodge, threat-aware AI
dodge, and automatic laboratory respawn. Blocking and parrying are future hit
resolution outcomes; they should be added at the hurtbox/defense resolver rather
than bypassing weapon contact. The architecture is ready for additional
movesets and skills; those content sets are intentionally not fabricated before
their intended combat designs are chosen.
