# Guild Ecosystem Prototype

> **Implementation snapshot:** This document specifies the current v4 serious
> regional mechanics prototype. `Docs/Game-Vision.md` is the authoritative
> product design. Prototype constraints such as whole-day ticks, simple
> contracts, and fully abstract raids do not override that vision.

## Purpose

This document is the production-facing contract for a mechanics-first, 2D
prototype of Turtle's living hunter ecosystem. The prototype exists to prove
that a small world of persistent hunters can create understandable stories
through shared rules before those rules are connected to expensive 3D content,
authored combat encounters, or final UI.

The required slice begins with 80 active persistent hunters, five competing
guilds, and a regional 2D world map with three towns, markets, hospitals,
resource sites, and dungeons. Daily awakenings and retirement at the active cap
grow a permanent historical roster while keeping 60–100 active hunters. The
player controls one of those hunters. That hunter is not a privileged hero:
player and AI actors use the same commands, validations, costs, risks, and
consequences.

This is a systems prototype, not a promise of final presentation. Temporary
icons, text, panels, and deterministic dungeon resolution are acceptable when
they make the mechanics easy to operate and inspect.

## Prototype invariants

- A new world starts with exactly 80 active hunters and exactly five guilds.
- Active population remains between 60 and 100 while retired and dead hunters
  remain persistent historical people rather than disappearing.
- At least one hunter awakens per simulated day. When the active cap is reached,
  eligible idle veterans may retire through the shared action pipeline.
- The Association board maintains between 5 and 15 concurrent offered,
  accepted, or active gates/contracts.
- Every persistent record has a stable, unique string ID. Scene objects and
  list positions are never identities.
- One authoritative world state owns hunters, guilds, map state, contracts,
  parties, dungeon runs, rewards, relationships, and memories.
- The controlled hunter follows the same eligibility and outcome rules as any
  AI hunter, including injury and death.
- All consequential actions pass through the shared world-command pipeline.
- XP, Ability Point spending, ability learning, and loadout selection use the
  same career rules and commands for controlled and autonomous hunters.
- Official rank is derived only from invested Ability Points. Unspent points
  and equipment never raise the Association rank.
- Class and hybrid labels are derived descriptions, never progression locks.
- Simulation results are deterministic for the same save, seed, commands, and
  tick order.
- Saving and loading preserves causal history, not only current numerical
  values.
- Equipment primarily grants actions and tactical behavior. A higher number
  alone is not a valid gear identity.
- Presentation reads authoritative state and emits commands; it does not own or
  silently repair simulation state.

## World and campaign loop

### Hunters

Each hunter persists across days and play sessions. The v4 prototype records at
least identity, career level and XP progress, saved and invested Ability Points,
attribute investments, official rank, derived build, learned abilities, active
loadout, life state, campaign vitals, injuries and recovery time, traits, goals,
current location, current activity, guild membership, inventory, equipped gear,
party membership, relationships, memories, and pending rewards.

Traits and history must affect behavior. Two equally powerful hunters may make
different choices because one is loyal and cautious while another is ambitious,
greedy, or carrying a grudge. Generated biography that does not influence a
decision is presentation, not simulation.

### Hunter Career Loop

Authored world actions award XP through the shared action service. XP is added
to the hunter's current bar before thresholds are resolved. When the bar fills:

1. the current threshold is consumed;
2. one Ability Point is granted;
3. the hunter's career level and next threshold advance; and
4. any overflow remains and may fill later thresholds during the same award.

XP is never silently discarded at a boundary. Dead hunters cannot receive
career rewards. Threshold growth and individual XP rewards are prototype tuning
values, but the overflow and AP-award semantics are contractual.

Ability Points form one pooled resource. Unspent points remain saved on the
hunter until a shared career command invests them in an attribute, learns an
ability or passive, or deliberately leaves them unspent for a future purchase.
Spending is atomic: rejected costs, prerequisites, IDs, or slots produce a
stable reason and no partial mutation.

The Association rank uses six-point invested-AP bands:

- E: 0-5 invested AP;
- D: 6-11 invested AP;
- C: 12-17 invested AP;
- B: 18-23 invested AP;
- A: 24-29 invested AP;
- S: 30 or more invested AP.

Only AP committed to attributes, abilities, or passives counts. Saved AP and
equipment may substantially change actual performance, but cannot change this
official rank. Rank is derived from canonical investment rather than maintained
as an independently mutable source of truth.

Fighter, Healer, Assassin, Ranger, Tank, and Mage are scored build families.
The displayed class is derived from attribute investment, learned abilities and
passives, equipped gear role, and established career preferences. A sufficiently
strong secondary score produces a hybrid label; no label blocks legal learning
or equipment choices.

Hunters own a learned ability library that may exceed the active loadout. The
The v4 loadout contains exactly these bounded categories:

- up to three cooldown abilities;
- up to one ultimate ability;
- up to two passives.

Ability kind, stable ID, AP cost, and prerequisites are authoritative catalog
data. Slot changes validate ownership, kind, duplicates, and capacity. Weapon
and gear moves remain separate from learned-ability slots. Learned abilities
provide a small mastery contribution to abstract combat, while abilities in the
active loadout express most of their cost-, kind-, and affinity-weighted value.
This keeps loadout selection mechanically meaningful without changing official
rank.

This slice simulates ability ownership, career utility, loadout legality, and
their campaign/autoresolve influence. It does not yet execute learned abilities
as rendered combat effects. A later combat adapter must map the same stable
ability IDs and loadout state into real-time behavior, then commit resulting
canonical state without inventing a parallel progression system.

### Guilds

Five guilds compete for members, resources, prestige, and control of useful
sites. Guild records own their treasury, reputation or prestige, territory
claims, roster, and relationship state toward other guilds. Guild competition
may remain simplified in this slice, but it must change world state and give
hunters reasons to prefer or oppose a guild.

Membership is reciprocal and unique: a living hunter belongs to no more than
one guild, and a guild roster cannot contain duplicate or nonexistent hunters.
Joining, leaving, and recruitment use the same commands whether initiated by
the player or an AI decision.

### 2D world map

The world is a persisted graph displayed in two dimensions. Nodes have stable
IDs, positions, types, and optional ownership. The prototype includes:

- towns that support recruitment, recovery, contracts, and trade;
- resource sites that generate or transfer useful economic value;
- dungeons that accept parties and produce risk, rewards, and stories;
- explicit travel links with a deterministic time or cost.

Travel is stateful rather than cosmetic. A hunter or party has an origin,
destination, and remaining travel time, and cannot perform location-bound
actions elsewhere. Essential nodes must be reachable from the starting towns.

### Contracts

Contracts are world records, not transient UI buttons. Each contract identifies
its issuer, destination or target, requirements, reward, creation day, expiry
day, and lifecycle state. The minimum lifecycle is:

`Available -> Accepted -> Completed -> Claimed`

An available or accepted contract may instead become `Expired` when its exact
deadline passes. Completion and claiming are separate so rewards can be
inspected and cannot be paid twice. Expired contracts are replaced on a
deterministic cadence to keep the world active.

### Recruitment and parties

Hunters can recruit for a guild, invite others to a party, and accept or reject
invitations. Acceptance is a utility decision informed by availability,
location, risk, goals, traits, guild alignment, relationships, memories,
expected reward, and current injuries.

A party has one leader, a bounded member list, a location or travel state, and
an explicit activity. Invitations have sender, recipient, purpose, creation
day, and expiry day. Party membership is reciprocal and unique. Disbanding,
death, retreat, or contract cancellation cleans every related reference.

### Dungeon resolution

The 2D prototype resolves dungeons as deterministic campaign events. Resolution
uses the party's gear-granted actions and tactical roles, invested career state,
active loadouts, injuries, relationships, traits, dungeon properties, and a
persisted deterministic random source. Raw power may contribute, but it cannot
be the only input.

The resolver exposes clear phases:

1. validate party, location, contract, and dungeon availability;
2. commit the party to the run;
3. resolve one or more risk stages, allowing retreat where valid;
4. apply wounds, recovery, death, XP, Ability Point awards, loot, and economic
   changes;
5. complete or fail the related contract;
6. create structured memories, relationship changes, and a readable story;
7. release surviving hunters to an appropriate post-run activity.

Retreat is a normal command with costs and consequences, not a special escape
available only to the player. A death remains part of world history and removes
the hunter from active rosters and parties without deleting their identity,
relationships, or memories.

### Social and economic consequences

Social actions include at least a positive and a negative interaction so the
prototype can demonstrate friendship, rivalry, loyalty, and grudges outside a
dungeon. Consequential interactions create structured memories containing a
stable event ID, day, actor, subject, type, emotional weight, and summary.

Relationships are directed. Trust, affinity, rivalry, loyalty, and grudge-like
values may differ in each direction. A story can update both directions, but it
must do so explicitly.

The economy tracks guild resources, site income, trades, dungeon loot, contract
rewards, and unclaimed rewards. Transfers are validated, conserved where
appropriate, and recorded once. Territory or site ownership must provide a
visible economic or decision-making effect during the slice.

## Gear as verbs

Gear definitions are authored data referenced by stable IDs from save records.
Each item grants a move set and a tactical role or role weights, such as
frontline control, protection, healing, ranged pressure, mobility, or burst
damage. Gear may also carry secondary numeric tuning, but changing gear must
change what the hunter can do and how AI evaluates a situation.

The initial catalog should include enough contrasting items to prove all of the
following:

- the controlled hunter receives a different command or move set after
  equipping different gear;
- AI hunters can evaluate and equip the same items;
- dungeon party evaluation recognizes the granted roles or actions;
- saved gear IDs restore the same moves after loading;
- missing catalog IDs produce a reported safe fallback, never an invisible
  replacement.

Adding future weapons, spell books, armor sets, relics, class passives, or
hybrid builds should extend the same contract rather than add player-only
abilities.

## Shared player and AI command architecture

The simulation distinguishes who chooses an action from how the action is
executed.

1. A player input adapter or AI planner creates a command proposal.
2. The shared validator checks the actor and target against authoritative world
   state.
3. The shared executor applies the accepted command atomically.
4. The result emits structured world events and any resulting memories,
   rewards, or decision records.
5. Presenters refresh from the resulting state.

Every command carries an actor ID and typed arguments. A command result reports
success or rejection plus a stable reason code and display text. AI may score
several proposals, but the selected proposal still passes through the same
validator as player input. The player adapter cannot bypass costs, travel,
cooldowns, eligibility, capacity, relationships, injuries, or death.

The v4 command surface includes:

- join a guild;
- leave a guild;
- recruit a hunter;
- accept a contract;
- send a party or activity invitation;
- accept or reject an invitation;
- equip gear;
- travel to a connected map node;
- enter a dungeon;
- retreat from a dungeon;
- trade;
- claim completed rewards;
- perform a social action;
- train for authored XP;
- invest an Ability Point in an attribute;
- learn an ability or passive;
- equip a learned cooldown or ultimate ability;
- equip a learned passive; and
- explicitly save Ability Points for later.

Commands should be plain, serializable descriptions of intent. Unity input,
buttons, and scene objects stay outside domain logic. Validation and utility
inspection must not mutate the world merely by asking what would happen.

## Decision inspector

Every autonomous choice produces an inspectable decision record. At minimum it
contains:

- decision ID, day, actor ID, and decision category;
- each valid candidate action and target;
- named score contributions for traits, goals, risk, reward, gear or role fit,
  relationships, memories, guild interests, injury, distance, and availability
  when relevant;
- total utility, chosen candidate, stable tie-break rule, and rejection reasons;
- a short human-readable explanation derived from the same factors.

The chosen action must be the highest valid utility after deterministic
tie-breaking. The inspector can show the latest decision for a hunter and allow
recent records to be browsed. Short UI event history may be trimmed, but the
structured memories and decisions required to explain later behavior remain in
the save according to an explicit retention policy.

The end-to-end proof is causal: completing a dungeon with another hunter creates
a memory or relationship change, and that fact later changes at least one
utility contribution, selected action, or accept/reject result. The later
decision explanation must identify the relevant history.

Career planning is separately inspectable. An autonomous hunter's career record
must identify whether they trained, invested, learned, equipped, or saved; name
the considered attribute or ability; show the relevant AP cost and named utility
factors; and explain the selected deterministic result. Inspecting candidates
must not mutate AP, learned libraries, loadouts, rank, or build labels.

## Deterministic cadence

The campaign advances in discrete day ticks. Pausing presentation pauses
automatic advancement; manual stepping advances exactly one tick. A tick uses a
fixed phase order:

1. expire invitations and contracts due at the boundary;
2. apply controlled-site income and publish scheduled contract opportunities;
3. visit autonomous hunters in stable hunter-ID order;
4. for each eligible hunter, run the bounded zero-time career-planning phase,
   allowing them to invest, learn, equip, replace a loadout choice, or save through
   shared commands;
5. build, validate, and execute that hunter's one ordinary daily world action;
6. advance travel and resolve previously committed dungeon stages, including XP,
   rewards, injuries, relationships, and memories;
7. replenish the contract board, record the day summary, and trim bounded history;
8. autosave when configured by the host.

The world save stores a seed and deterministic random position, or uses random
values derived from stable keys such as world seed, day, phase, actor ID, and
action ID. Frame rate, collection enumeration order, UI selection, and time
spent paused must not change campaign results. A 28-day uninterrupted run must
match a 14-day run that is saved, loaded, and advanced for 14 more days under
the same command schedule.

The UI may offer pause, normal speed, faster observation, and single-day step.
Speed changes how quickly ticks are requested, never the rules inside a tick.

## Controls and UI workflow

Exact bindings and visual styling are intentionally not contractual yet. Every
required action must remain discoverable and operable through visible UI; any
keyboard or mouse shortcuts are additive and should be shown in the interface.

The provisional workflow is:

1. select the controlled hunter, another hunter, a guild, a contract, or a map
   node;
2. inspect current state, relationships, memories, gear, XP, Ability Points,
   rank, derived build, learned library, loadout, and available actions;
3. choose a contextual action and see its eligibility, cost, travel time, and
   likely risks before confirming;
4. execute the shared command and receive an immediate result;
5. advance or observe the simulation;
6. inspect the world event, decision breakdown, and lasting consequence;
7. save, load, or continue the campaign.

A practical prototype layout may use a 2D map as the central surface with
roster and guild panels, contracts and party details, a contextual action bar,
an event/story history, and a decision inspector. Hovered scrollable panels
should consume scroll input consistently. Selection and time controls must
remain usable while the simulation is paused. Destructive or irreversible
actions, including abandoning rewards or accepting lethal risk, need clear
confirmation.

## Persisted v4 schema

The v4 save is a data-only snapshot stored by default at:

`Application.persistentDataPath/2d-ecosystem-v4.json`

The top-level record contains at least:

- `saveVersion`, world seed, deterministic random position, and day;
- controlled hunter ID;
- hunters, including life, campaign vitals, XP, career level, saved and invested
  Ability Points, attribute investments, rank inputs, derived build inputs,
  learned abilities, active loadout, injuries, location, inventory, gear, guild,
  and party references;
- five guilds, their rosters, resources, prestige, territories, and rivalries;
- map nodes, links, site ownership, and site production state;
- contracts, invitations, parties, active dungeon runs, trades, and pending
  rewards;
- directed relationships, structured memories, consequential world events, and
  inspectable decision records;
- population sequence, awakening day, retirement day and reason, plus the full
  inactive historical roster.

Serialized records use IDs rather than Unity object references. Runtime catalogs
resolve gear, dungeon, contract, and social-action definition IDs. Collections
must serialize in a stable order. If Unity `JsonUtility` is used, dictionaries
and polymorphic interfaces are represented by explicit DTO lists and stable type
IDs.

Saving writes to a temporary file, validates the written snapshot, and then
atomically replaces the active v4 save where the platform allows. The previous
valid save is retained as a backup. Corrupt, incomplete, unsupported, or
reference-invalid data is reported; it is never silently replaced by a fresh
world.

### v3, v2, and v1 import

On first v4 load, the repository may import the newest supported legacy save,
preferring `ecosystem-slice-v3.json`, then v2, then v1. Import is one-way and
non-destructive:

1. read and validate the legacy file without modifying it;
2. preserve day, player hunter, existing hunters, guild membership, levels,
   experience, wounds, traits, goals, equipped gear, relationships, memories,
   and event history where present;
3. preserve v2 life/death and vitals exactly; importing v2 must never resurrect
   a dead hunter or regenerate an already initialized career;
4. deterministically translate legacy level and XP into one valid v4 career
   state, including AP investment, saved AP, rank, build, learned library, and
   loadout defaults, without awarding the same migration growth twice;
5. deterministically add hunters to reach 80 active people and add guilds to
   reach five where an older source lacks the serious ecosystem content;
6. create missing map state, place imported actors at valid nodes, and translate
   legacy missions into initial v4 contracts or archived history;
7. map v1 wounds to injuries/recovery, rebuild reciprocal references, and assign
   safe defaults only for fields the source did not contain;
8. derive and store a stable world seed, then validate every ID and invariant;
9. write and re-read a separate v4 save; and
10. leave all legacy source files untouched after successful import.

Migration warnings and fallbacks are visible in logs. An unsupported future
version is not treated as v1 and is never downgraded.

## Developer extension points

The prototype should keep deterministic domain rules in plain C# and use
MonoBehaviours only as scene, input, timing, and presentation adapters. The
smallest useful extension boundaries are:

- world factory and invariant validator;
- simulation clock and deterministic random source;
- command definitions, validator, executor, and result events;
- AI candidate generation, utility scoring, and decision records;
- travel, contract, party, dungeon, social, progression, and economy rules;
- v4 save DTO mapper, validator, store, and v3/v2/v1 import migrators;
- stable-ID catalogs for gear, abilities, dungeons, contract templates, and
  social actions;
- map, roster, action, event-history, and decision-inspector presenters.

New gameplay should extend these boundaries rather than mutate save DTOs from a
view or create a player-only path. A new command needs shared validation,
execution, AI eligibility where applicable, a structured result, persistence
coverage, and at least one deterministic test. A new gear item needs stable
identity, granted verbs, tactical role data, AI scoring inputs, presentation,
and save/load coverage. A schema change increments the version and supplies a
migration before relying on the new field.

Simulation code must avoid frame-driven decisions, scene-wide searches, and
per-frame allocation. UI refreshes should follow state changes rather than
rebuilding every frame. The 60–100 active-hunter slice remains modest, but
these ownership rules allow later worlds to simulate distant agents at a
coarser cadence.

## Acceptance and validation matrix

| Requirement | Acceptance evidence | Validation |
| --- | --- | --- |
| Persistent population | A new seeded world contains 80 unique active hunters; daily awakenings and cap-balancing retirement retain unique identities and history while active population stays in the 60–100 band. | Factory/invariant test, 35-day lifecycle test, and PlayMode roster inspection. |
| Competing guilds | Exactly five guilds have valid unique rosters and visibly change resources, prestige, relationships, or territory through competition. | Multi-week deterministic simulation test and UI inspection. |
| 2D world | Three towns plus markets, hospitals, resource sites, and dungeons appear at persisted coordinates; required nodes are connected and travel consumes deterministic time. | Graph validation, serialized-scene wiring, and map/travel smoke test. |
| Association board | Between 5 and 15 offered, accepted, or active contracts remain available as contracts resolve and expire. | Initial-world assertion and multi-week board invariant test. |
| Expiring contracts | Contracts publish, accept, complete or expire on exact day boundaries, and rewards can be claimed only once. | Contract lifecycle and idempotence tests. |
| Recruitment and parties | Hunters can recruit, invite, accept, reject, form, leave, and disband without duplicate or dangling references. | Fixed-utility command tests and PlayMode party workflow. |
| Dungeon resolution | A party can enter, resolve, or retreat; outcomes apply gear roles, risk, story, and cleanup exactly once. | Seeded resolver branch tests and one complete runtime expedition. |
| Gear grants moves and roles | Swapping gear changes available verbs and tactical evaluation for both player and AI, then restores correctly after load. | Gear command/role test and visible action comparison. |
| Consequences and progression | Rewards, wounds, recovery, XP overflow, career levels, Ability Points, and deaths persist; the controlled hunter has no immunity. | XP boundary/multi-fill tests plus forced deterministic injury, recovery, and death smoke scenarios. |
| AP, rank, and hybrid build | Saved AP does not affect rank; each valid investment changes the canonical AP ledger; exact six-point boundaries derive E-S ranks; build labels derive from career state without restricting hybrids. | Boundary tests at 5/6, 11/12, 17/18, 23/24, and 29/30 AP; ledger invariant and seeded hybrid-build cases. |
| Ability library and loadout | Learned IDs persist independently of the bounded three cooldown, one ultimate, and two passive slots; invalid costs, kinds, duplicates, and capacity reject without mutation. | Shared command parity, rejection atomicity, slot-capacity, and v4 round-trip tests. |
| NPC career planning | Autonomous hunters deterministically choose to invest, learn, equip, or save in a distinct phase, with understandable factors and varied resulting careers. | Fixed-profile choice cases, factor-sum/argmax checks, repeated-seed equality, and multi-week divergence tests. |
| Social history | Relationships, loyalties, rivalries, grudges, and structured memories change from social and dungeon events. | Directed-relationship and memory tests plus inspector comparison. |
| Multi-week save/load | A multi-week world round-trips without losing state; interrupted and uninterrupted deterministic continuations match. | Deep v4 round-trip, v3/v2/v1 import, corruption, idempotent migration, and continuation tests. |
| Ordinary player parity | Career investment, learning, loadouts, join/leave, recruit, contracts, invitations, equipment, travel, dungeon, retreat, trade, rewards, and social actions use the same command validation and effects for player and AI actors. | Parameterized actor-parity tests and command-result inspection. |
| Inspectable AI | Candidate utilities, named factors, totals, rejection reasons, choice, tie-break, and explanation are visible and internally consistent. | Score-sum/argmax/pure-inspection tests and runtime decision inspector. |
| Causal dungeon story | A completed shared dungeon changes a later decision through a recorded relationship or memory, and the explanation cites that history. | Deterministic A/B end-to-end test and PlayMode story walkthrough. |
| Economy and territory | Site income, trade, contract rewards, dungeon loot, guild resources, and claims remain valid with no duplicate payout or invalid ownership. | Ledger/invariant tests across multiple seeds and runtime economy inspection. |
| Usable prototype UI | Career state and commands, pause/step/time controls, event history, save/load, and decision inspection are discoverable without relying on undocumented shortcuts. | PlayMode workflow at the supported prototype resolution. |

The prototype is ready for handoff only when Unity compiles without new errors,
the deterministic validation suite passes, the 2D Ecosystem scene completes
the runtime workflow above, legacy-to-v4 imports are exercised
against representative legacy saves, and all unverified limitations are
recorded.
