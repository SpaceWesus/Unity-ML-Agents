# Guild Ecosystem Prototype

> This is a current-scene handoff, not the final game specification. The
> authoritative product design is `Docs/Game-Vision.md`; the detailed current
> prototype contract is `Docs/Guild-Ecosystem-Prototype.md`.

`Assets/Scenes/2D Ecosystem.unity` hosts the serious mechanics-first 2D Guild
Ecosystem Prototype. Its camera, controller, strategy view, and gear catalog are
serialized in the scene and persist outside Play Mode. The map and hunter tokens
are runtime projections of the persistent saved world, not scene-authored actors.

The complete contract and validation matrix live in
`Docs/Guild-Ecosystem-Prototype.md`.

## What the slice proves

- 80 active hunters at world creation, with a supported range of 60–100 active
  hunters and an unbounded historical roster retaining retired and dead people.
- Daily deterministic awakenings. On reaching the active cap, eligible veteran
  hunters may retire through the same inspectable action pipeline used by AI and
  player commands.
- A shared Hunter Career Loop in which XP overflow is retained across bar fills,
  each completed bar grants Ability Points, and hunters may save or invest those
  points through the same commands available to the controlled hunter.
- Official rank derived only from invested Ability Points in six-point bands,
  descriptive hybrid builds derived from career choices, and a learned ability
  library feeding three cooldown, one ultimate, and two passive loadout slots.
- Five competing guilds with resources, prestige, territory, and diplomacy.
- A typed regional map containing three towns, marketplaces, hospitals,
  resource sites, routes, and dungeons.
- A maintained Association board containing 5–15 concurrent gates/contracts.
- Contracts that are posted, accepted, expire, become active expeditions, resolve,
  and pay rewards exactly once.
- Persistent parties and guild/party invitations with accept, decline, and expiry.
- Gear that grants named combat verbs and a tactical role, not only power.
- The same validated command executor for the controlled hunter and autonomous
  hunters. The player is not exempt from injuries, death, travel, ownership, or
  membership rules.
- Inspectable utility decisions whose total is the sum of named factors such as
  courage, risk, reward, trust, rivalry, wounds, goals, and remembered events.
- Deterministic coarse simulation that continues identically across save/reload.

## Play loop

1. Select a hunter, guild, contract, and map location.
2. Join or leave a guild, recruit hunters, accept a contract, and form a party.
3. Travel to the contract location, invite a complementary tactical role, and
   enter the dungeon or retreat before resolution.
4. Claim rewards, change equipment, recover from injuries, contest resource
   sites, trade, help, challenge, betray, or reconcile with other hunters.
5. Advance days or weeks and inspect how autonomous choices change guild control,
   relationships, memories, careers, injuries, and future decisions.
6. Save, reload, and continue the same world rather than reseeding a new story.

## Interface and controls

The full-screen strategy view is clickable and contains:

- hunter roster and selected-hunter dossier;
- career inspection for XP, saved and invested Ability Points, official rank,
  derived hybrid build, attributes, learned abilities, and equipped loadout;
- guild selectors and their political/economic state;
- world map with typed location markers and hunter counts;
- expiring contract board;
- action palette with shared training, AP investment, ability learning, loadout,
  invitation, inventory, and gear commands;
- world-event history;
- latest decision score with its named factor contributions.

Keyboard shortcuts:

- `T`: advance one day;
- `F5`: save;
- `F9`: reload the latest validated v4 save;
- `Space`: pause/resume automatic world advancement.

The top bar also exposes pause/run, simulation speed, one- and three-day steps,
save, and load. The legacy third-person
controller is intentionally disabled while this strategy prototype is active so
the cursor remains available for the 2D interface.

## Shared agency architecture

`EcosystemDecisionSystem` only selects a command for an autonomous hunter.
`EcosystemActionService` validates and executes that command. Player button presses
construct the same `EcosystemActionRequest` and go through the same service.
Training, attribute investment, ability learning, loadout changes, and the
explicit choice to save Ability Points follow this boundary as well. NPC career
planning runs in its own deterministic zero-time phase before each ordinary
daily action, spending rewards that were already present at the start of that
day rather than being hidden inside the general action planner.

This boundary is the central production constraint: a future dungeon scene may
replace coarse resolution, but it must return results to the same contracts,
parties, hunters, guilds, relationships, and memories.

The v4 slice owns ability identity, learning, costs, classification, and loadout
selection. Active ability choices already influence abstract combat power, but
the slice does not yet execute those learned abilities in rendered combat; a
later combat adapter will resolve the stable ability IDs into real-time effects
while preserving the same canonical career state.

## Persistence

The active schema is version 4. It retains the Hunter Career Loop and adds
persistent awakening and retirement state, population-sequence identity, the
serious-scale regional facilities, five guilds, and the maintained Association
contract board. Version-3, version-2, and version-1 saves are imported into a
separate v4 snapshot while retaining existing hunters, history, life state,
relationships, memories, careers, and elapsed day.

The active and legacy paths are:

- `Application.persistentDataPath/2d-ecosystem-v4.json` (active save)
- `Application.persistentDataPath/ecosystem-slice-v3.json` (read-only import source)
- `Application.persistentDataPath/ecosystem-slice-v2.json` (read-only import source)
- `Application.persistentDataPath/ecosystem-slice-v1.json` (read-only import source)

Writes use a temporary file, schema/invariant validation, atomic replacement,
and a rolling backup. Corrupt or unsupported saves are backed up before a
replacement world is created.

## Developer tools

- `Turtle > Ecosystem > Validate Guild Ecosystem Prototype` runs deterministic
  domain, serious-scale population, awakening/retirement, scene-wiring,
  migration, shared-command, XP/AP boundary, rank, build, loadout,
  career-planning, contract, utility, persistence, and multi-week invariant
  checks without touching the player's real save.
- `Turtle > Ecosystem > Build 2D Ecosystem Scene` idempotently adds or updates
  the serialized host, view, camera, gear references, and Build Settings entry.
- `Turtle > Ecosystem > Rebuild Requested Scenes` remains an explicit destructive
  scene-authoring command. It is not invoked on Play and should not be used for
  routine incremental layout changes.

The old 3D humanoid/animation presentation is still available in the codebase as
an optional view. It is not authoritative and can be replaced without changing
the simulation.
