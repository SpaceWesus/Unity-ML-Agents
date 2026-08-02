# Dungeon Raid Prototype

This folder contains the first autonomous, top-down raid slice for `Demo Dungeon`.
It deliberately operates on generic parties, pods, agents, rooms, connections, and
ability effects. No state machine assumes six hunters, goblins, or a particular
class composition.

## Scene setup

Use `Turtle > Dungeon Raid > Setup Demo Dungeon Prototype` after changing the
authored room or actor hierarchy. The command is idempotent: it preserves the
scene geometry and primitive art, updates the required components and references,
and saves the scene. Then use `Validate Demo Dungeon Prototype` for structural
validation.

The setup recognizes the current hunter labels. `Tanker` is intentionally mapped
to the internal `Tank` combat role. The current authored Squad 1 contains six
goblins: one sergeant, two bowmen, and three swordsmen.

The saved scene contains a fully materialized room-first preview, so the dungeon
is visible and editable before Play Mode. The demo deliberately rolls a new seed
when Play begins. A campaign gate should instead call
`GenerateFromStoredGateSeed(gate.mapSeed)` so saving and reloading reproduces the
same layout.

The generator creates 8-12 rooms on a connected cardinal graph, selects distant
entrance and boss endpoints, builds a critical path, adds optional branches and
loops, and routes bent corridors between room doorways. Branches become reward,
resource, or event rooms. Eleven spatial templates provide open arenas, pillars,
split halls, crossroads, ring cover, ambush cover, caches, resource pockets,
antechambers, and boss variants.

## Runtime ownership

- `DungeonRaidDirector2D` owns the encounter clock, agent registry, ability
  resolution, combat events, and outcome.
- `RaidPartyBrain2D` owns the hunter party's objective state: rally, advance,
  engage, recover, loot, and complete/fail.
- `RaidEnemyPodBrain2D` owns a generic enemy group's dormant, alert, combat, and
  defeated states.
- `RaidAgent2D` owns one combatant's stats, resources, cooldowns, movement, life
  state, and presentation.
- `RaidRoom2D` describes a room. `RaidRoomConnection2D` owns adjacency, so neither
  room owns the shared doorway. Connections also expose ordered corridor
  waypoints so parties follow bends rather than cutting through walls.
- `DungeonRoomFirstPlanner2D` is a pure deterministic layout planner. It can be
  tested without loading a scene and is suitable for both rendered and abstract
  dungeon representations.
- `DungeonRoomFirstGenerator2D` materializes the plan into floor, wall,
  collider, obstacle, and spawn-marker objects, then binds the raid fixture to
  the generated entrance and a deep encounter room.
- `RaidRoom2D.Purpose` separates semantic room use from theme presentation.
  `RaidSpawnMarker2D` exposes party, enemy, boss, chest, and exit sockets for a
  future generated-gate materializer without owning encounter state.
- `RaidChest2D` stores only open/closed state. Loot ownership belongs to the
  recipient's inventory when a real inventory system is connected.
- `RaidFxPool2D`, `RaidCamera2D`, and `RaidHud2D` provide pooled combat feedback,
  framing, and readable runtime state.

## Unity 2D combat contract

- Every combatant is a zero-gravity dynamic `Rigidbody2D` with a solid
  `CircleCollider2D`. Authored walls use solid `BoxCollider2D` components and
  chests expose trigger colliders.
- Shared movement uses allocation-free `Rigidbody2D.Cast` probes and stable
  left/right steering preferences to flow around solid room cover. This keeps
  generated template obstacles physical without teaching a hunter-specific AI
  about each layout.
- AI chooses an intended action and aim. `RaidCombatPhysics2D` decides whether
  the action actually contacts an opposing hurtbox. Range checks are useful for
  decision-making but never grant damage by themselves.
- Basic melee and ranged attacks use swept circle casts. Piercing attacks
  collect multiple contacts until a wall, while circle, cone, and rectangle
  volumes use non-allocating overlap queries.
- Faction is checked from `RaidAgent2D`; Unity tags are not authoritative combat
  data. This keeps the same contact rules available to player and AI command
  sources.
- The current class fixtures exercise Challenge, Bulwark, Cleave, Rallying
  Strike, Mend, Sanctuary, fire Elemental Affliction, Fireball, Piercing Shot,
  Hunter's Mark, Shadow Step, and Execution with the cooldowns and magnitudes
  recorded in the prototype brief.

## Adding content

Add agents to a party or pod and assign them through its serialized member list.
The brains work with any member count. Add new behavior by extending
`RaidAbilityEffect` and the director's ability scoring/resolution. AI chooses
abilities from effect, range, resource cost, cooldown, and battlefield need; it
does not branch on character names.

Add reusable spatial recipes by extending `DungeonRoomTemplate2D` and
`PopulateTemplate`. Add semantic rules to the planner separately; this keeps
geometry choices independent from encounter or reward behavior. A pod can lock
a chest; once that pod is defeated, the party can open the chest and continue.

See `Docs/Dungeon-Room-First-Guide.md` for the current layouts, recommended next
room archetypes, BSP constraints, and theme/art-direction hooks.

## Persistence boundary

`Turtle.Ecosystem.GateInstanceState` and `DungeonEncounterState` are the
canonical persistence models for generated gates and live raids. This standalone
scene is a combat fixture, not a second save format. A campaign materializer must
bind these views to those existing records and commit physical outcomes back to
the same encounter state.

Static dungeon facts should remain reproducible from the gate's map seed,
generator version, and generation rules. Persist only sparse mutations that
cannot be recovered from the seed, such as an opened chest slot or depleted
resource slot, plus exact participant and encounter state.

The prototype encounter clock is runtime-only. An offscreen resolver may process
a raid in one batch without saving a tick. If a future save can pause a raid or
promote it from abstract to rendered combat mid-encounter, persist the minimal
resolver state necessary to resume the same outcome.

A generator version is not required by this prototype. It becomes useful when
released saves must regenerate an old seed after the generation algorithm or its
content ordering changes. At that point, the version selects the compatible
generator; the alternative is persisting the full generated layout.
