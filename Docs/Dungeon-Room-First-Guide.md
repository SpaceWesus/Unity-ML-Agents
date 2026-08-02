# Dungeon Room-First Greybox Guide

This document defines the first reusable room kit for Turtle's top-down dungeon
prototype. Room purpose is independent from visual theme: a Damp Cave entrance
and a Ruined Temple entrance use the same gameplay contract while selecting
different tiles, props, lighting, enemies, and hazards.

## First rooms to author

1. **Entrance / staging room** — safe party spawn, readable exit direction,
   enough space for six hunters to form up, and no immediate enemy line of sight.
2. **Open encounter room** — baseline combat arena used to validate melee,
   ranged attacks, party spacing, and large area abilities.
3. **Pillar encounter room** — four staggered blockers create flanks, protected
   ranged positions, and interrupted lines of sight without forming a maze.
4. **Boss antechamber** — a short tension-release room for regrouping, healing,
   readiness checks, dialogue, or a one-way commitment gate.
5. **Boss arena** — the largest room, with a clear boss socket, edge pylons,
   reward socket, and exit-portal socket.
6. **Straight corridor** — the first connector to standardize. Corridors provide
   pacing and spatial separation; they should not be treated as full encounter
   rooms unless explicitly promoted to an ambush or trap room.

The next highest-value optional rooms are a reward/resource side room, an elite
encounter room, a hazard/event room, and a secret room. These should branch from
the critical path rather than lengthening every mandatory dungeon.

## Current generated demo

```text
┌──────────┐  ┌────────────┐ ═══════ ┌────────────┐ ══════ ┌───────────┐ ═══════ ┌──────────────┐
│ ENTRANCE ├──┤ OPEN COMBAT├─────────┤ PILLAR HALL├────────┤ANTECHAMBER├─────────┤  BOSS ARENA  │
│ party    │  │ goblin pod │ corridor│ pod socket │corridor│regroup/lock│corridor│boss/chest/exit│
└──────────┘  └────────────┘         └────────────┘        └───────────┘         └──────────────┘
```

The saved Demo Dungeon contains a preview generated from seed `731245`, so its
entire greybox is visible in the Scene view before Play. In the standalone demo,
Play Mode selects a fresh seed and replaces only the generated hierarchy. A
campaign gate will supply its persisted `mapSeed` instead.

The current planner grows 8-12 connected grid cells, makes a randomized spanning
tree, optionally adds loop connections, and uses the graph diameter for the
entrance-to-boss route. This creates forks, dead-end side rooms, alternate loops,
and routes that turn in all four directions instead of a single horizontal hall.

## Encounter layout examples

### Open arena

```text
┌──────────────────────┐
│                      │
◀       ENEMIES        ▶
│                      │
└──────────────────────┘
```

Use this to establish the baseline difficulty of an enemy composition. It makes
poor target selection and party-spacing problems obvious.

### Pillar hall

```text
┌──────────────────────┐
│    ■          ■      │
◀       POD CENTER     ▶
│    ■          ■      │
└──────────────────────┘
```

Stagger or rotate the blockers in themed variants, but preserve multiple viable
lanes. Avoid narrow pockets where several `Rigidbody2D` agents can deadlock.

### Boss arena

```text
┌────────────────────────────┐
│  ■                      ■  │
│                            │
◀          BOSS              │
│                            │
│  ■                CHEST ○  │
└─────────────────────────◎──┘
                          EXIT
```

Keep the center readable for major telegraphs. Edge objects can support phase
mechanics, adds, destructible cover, or ritual objectives later.

## Implemented room-first generation constraints

- Generate 8-12 non-overlapping rooms on a cardinal spatial graph.
- Assign entrance and boss rooms to distant graph endpoints on the critical path.
- Fill intermediate critical-path leaves with encounter or transition rooms.
- Connect the critical path first, then attach optional branches.
- Use a roughly four-unit doorway and corridor width in the current prototype so the
  six-agent party can pass without excessive physical congestion.
- Keep at least one room-width of separation when possible; short threshold
  connections are allowed only when two rooms intentionally share a wall.
- Place encounters, chests, bosses, and exits through `RaidSpawnMarker2D`
  sockets rather than name searches or hard-coded world coordinates.
- Persist the seed and semantic layout. Theme presentation can be regenerated;
  opened chests, depleted resources, defeated pods, and other mutations remain
  sparse encounter state.

## Implemented spatial templates

- Open arena: clean baseline for spacing and ability evaluation.
- Pillar hall: four corner-offset blockers and multiple combat lanes.
- Split hall: paired divider walls that split pressure without sealing the room.
- Crossroads: diagonal cover that keeps all exits readable.
- Ring: four separated blockers around an open center.
- Ambush cover: staggered cover for flanking starts.
- Reward cache and resource pocket: distinct side-room silhouettes.
- Boss antechamber: compact regrouping space before the final room.
- Boss pillars and boss open: two arena silhouettes selected per seed.

Room purpose and room template remain separate. For example, a future elite
encounter can use a ring, pillar hall, or open arena without adding a new purpose
or teaching the AI about a particular visual layout.

## Theme and art-direction hooks

Start with strong value and silhouette differences before detailed tiles:

- **Damp cave:** irregular dark edges, mossy green floor shifts, shallow pools,
  roots, crystals, and bioluminescent encounter landmarks.
- **Ancient crypt:** axial symmetry, cold stone, sarcophagus cover, columns,
  sealed doors, candles, and warm treasure highlights.
- **Ruined temple:** broken colonnades, sand, collapsed walls, open courtyards,
  banners, and large ritual geometry in boss rooms.
- **Frozen cavern:** pale reflective floors, deep blue crevasses, ice pillars,
  drifting snow, and slippery or brittle hazard zones.

Gameplay landmarks should remain readable when all decoration is removed. The
theme dresses the room grammar; it should not be required to understand the
route, exits, enemy staging, or boss telegraphs.
