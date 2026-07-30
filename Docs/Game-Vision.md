# Turtle: Authoritative Game Vision and Design Contract

- **Status:** Authoritative product vision
- **Scope:** The intended full game and the production path toward it
- **Last updated:** 2026-07-26

This document is the source of truth for Turtle's product direction. When a
prototype document, scene, implementation shortcut, or older design note
conflicts with this document, this document takes precedence until it is
intentionally amended.

`Docs/Guild-Ecosystem-Prototype.md`, `Docs/Ecosystem-Slice.md`,
`Docs/Weapons-Testing.md`, and `Docs/Combat-Abilities.md` describe current
prototypes and subsystem details. They are subordinate implementation records,
not competing visions.

The design uses four kinds of statements:

| Label | Meaning |
| --- | --- |
| **Invariant** | A defining rule. Implementations must preserve it. |
| **Target** | A production goal that may be delivered incrementally. |
| **Tunable** | A balance value or presentation choice that may change without altering the vision. |
| **Deferred** | A decision that is intentionally not required yet. |

## One-sentence pitch

Turtle (name subject to change) is a video game where the player plays as a hunter in the world of Solo Leveling (Manga and Anime). 
However, in this experience, the player is simple an ordinary hunter among hundreds of other hunters. 
Everything the player can do, other hunters can do. Form Guilds, go on raids, complete long term goals, buy land, form relationships, level up, get money, etc.

The world is largely inspired by the aforementioned *Solo Leveling* and the underlying systems are inspired by games such as *Kenshi*, *Mount & Blade*, and *Shadow of War*. 


## North star: generate lives and stories, not a linear story or quest chain.

**Invariant:** The game succeeds when its interacting systems produce stories that the player can experience that are completely unique to their own playthrough. Playthroughs may rhyme with one another, but none may ever be the exact same.

Example
A low-rank hunter might barely survive a raid, wake after a coma, mourn dead friends, replace ruined equipment, reunite with survivors in the town who saved the them, recruit missing party members, win a bid for the rights to another gate, lose their healer during the boss fight, and barely close the gate just in time before everyone else dies.
Maybe they go onto start their own guild or just outright retire. It all depends on their own personal motivations. 
Who knows what lies in wait for their story in the coming days/weeks etc.

**Target:** The special part of this is that it is all a sandbox that can generate these sorts of stories without outright explicitly scripting it into existence. All the characters are randomly (or maybe procedurally) generated. Meaning their class, the motivations, their traits, their stats (Strength, Vitality, Agility, Intelligence, and Perceptio), and overall character build will be completely unique to them, and them only. Even how they go about building their character can lead to unique skill/trait/ability  combinations that allow for maximum individuality.


## Player promise

The player should be able to say:

> I began as one hunter with a particular body, build, equipment set,
> motivation, and social position. I made choices available to everyone else.
> The world did not wait for me, protect me, or reveal everything to me. My
> relationships and mistakes mattered, and the people around me continued
> living stories of their own.

The player's long-term possibilities include combat mastery, unusual hybrid
builds, trade, party leadership, guild creation, recruitment, political
influence, territory and resource conflict, or a short life ended by a dangerous
profession. None is the mandatory protagonist path.

## Non-negotiable design pillars

### The player is one hunter, not a privileged ruleset

While alive, the player directly controls one canonical hunter. That hunter can
be created manually or generated at campaign start and follows the same
progression, ability, equipment, economy, injury, death, guild, crime, and
world-time rules as NPC hunters.

The player may view allies when information and communication rules permit, but
does not freely possess other living hunters. Taking control of another hunter
is a succession option after the current player hunter dies.

### Reciprocal agency

Almost every meaningful choice offered to the player should also be available
to autonomous hunters when applicable. Both can:

- train, gain experience, allocate Ability Points, and learn abilities;
- choose gear, movesets, active abilities, passives, and tactical roles;
- trade, repair, replace, sell, or claim equipment and materials;
- bid for gates, accept work, form parties, raid, retreat, rescue, or abandon;
- join, leave, found, lead, fracture, or betray guilds;
- recruit, invite, refuse, negotiate, help, threaten, ambush, or assassinate;
- build friendships, rivalries, debts, loyalties, reputations, and grudges;
- make mistakes based on incomplete or incorrect information.

Player and NPC interfaces may differ, but their requests must enter compatible
validation, cost, and outcome rules.

### Every hunter is a persistent person

Hunters should feel closer to memorable Nemesis characters than disposable
units. Every hunter has a stable identity and enough persistent state to make
their decisions and history recognizable:

- appearance, name, origin, age, history, and public reputation;
- health, mana, base attributes, invested and unspent Ability Points;
- learned abilities, equipped loadout, affinities, and inferred combat roles;
- equipment, movesets, inventory, wealth, and gear durability;
- temporary, long-term, and permanent injuries;
- goals, motivations, fears, traits, values, and inclinations;
- guild, rank, strike-team roles, obligations, and current activity;
- directed relationships, debts, loyalties, rivalries, and grudges;
- memories of consequential events and who participated;
- knowledge, suspicions, rumors, evidence, and confidence in each belief.

Generated biography that never changes a choice is presentation, not
simulation.

### Class is an emergent description

Fighter, Healer, Assassin, Ranger, Tank, Mage, and hybrid labels describe how a
hunter currently plays. They are not permanent class locks.

A hunter's rough class estimate emerges from:

- base attributes and magical affinities;
- learned abilities and passives;
- equipped weapon and armor movesets;
- tactical behavior and demonstrated preferences.

Valid builds include a close-range Mage, Fighter-Healer, Healer-Tank, Ranger-
Assassin, bow-using Healer, or any other combination supported by the same
underlying rules. Innate strengths may make some paths cheaper or more
effective, but should create soft pressures rather than arbitrary player-only
restrictions.

### Gear grants verbs and meaningful tier progression

Changing equipment should change what a hunter can do. Weapons and significant
gear grant light and heavy attack patterns, defensive actions, mobility,
counters, utility, or other moveset verbs.

Gear tier also legitimately changes numbers:

- higher-tier weapons can deal greater base light and heavy attack damage;
- higher-tier armor can provide greater protection;
- durability determines whether gear must be repaired or replaced.

Numerical improvement supports the moveset identity; it does not replace it.

### Leadership is influence, not mind control

Guild leaders assign strategic intent such as:

- clear this gate;
- form this strike team;
- recruit for a missing role;
- train toward a Tank or Healer function;
- protect a site or weaken a rival.

Strike-team leaders issue tactical intent such as:

- focus this hostile target;
- focus healing, buffs, or rescue on this ally;
- follow me;
- group up;
- spread out.

Orders influence autonomous choices through authority, loyalty, relationships,
fear, incentives, risk, and personality. They do not bypass agency. Hunters can
refuse, hesitate, flee, reinterpret, betray, or suffer consequences.

### Camera fidelity never changes world truth

An encounter has one canonical state whether it is visible or offscreen. Camera
presence changes how much of that state is rendered and simulated, not the
combatants' hidden bonuses, odds, or eligibility.

### Consequences create the story

Danger, scarcity, injury, death, incomplete information, relationships,
equipment loss, succession, and economic competition are not side systems.
They are the pressures from which stories emerge.

## Player lifecycle

### Character creation

At campaign start, the player creates or generates one hunter. Character
creation includes:

- visible starting attributes such as health, mana, strength, and other final
  production stats;
- initial weapon and equipment selection;
- an initial motivation or life goal;
- appearance and identity choices;
- a rough descriptive class or hybrid estimate derived from the resulting
  build.

The exact stat catalog and starting budget are **tunable**. The result must use
the same hunter data model and legal build space as generated NPCs.

### Life, death, and succession

Hunter death is permanent within world history. When the player hunter dies,
the game presents a life summary containing accurate statistics such as:

- time alive and major affiliations;
- raids and gates attempted, completed, failed, and abandoned;
- mobs and bosses killed by category;
- damage dealt, damage taken, healing, protection, and rescues;
- canonical killer and final cause of death, even when the hunter did not know
  that information while alive;
- final attributes, invested and unspent Ability Points;
- learned and equipped abilities;
- equipment, wealth, reputation, relationships, and major memories;
- guild creation, leadership, territory, and other consequential achievements.

The player may then:

1. create or generate a new awakened hunter in the same ongoing world;
2. take over an eligible existing friend, party member, or guild member;
3. return to the main menu.

Taking over an existing hunter preserves that hunter's identity, history,
traits, relationships, obligations, rank, equipment, and injuries. Their former
AI goal becomes a visible role-playing motivation, but the player may pursue or
ignore it. Their decisions are now player-directed.

The deceased hunter remains part of history. Their body, equipment, property,
guild office, relationships, and memories are resolved by ordinary recovery,
loot, inheritance, succession, and world-event rules rather than being erased.

### Saving

The world supports normal manual/checkpoint saving and reloading. An optional
Ironman mode commits major outcomes and deaths through restricted autosaving.

There is no offline progression. The world advances only while the campaign is
open and running.

## Hunter progression, rank, and builds

### XP and Ability Points

World actions grant authored XP rewards. Examples include participating in
raids, killing mobs, defeating bosses, closing gates, completing contracts,
trading valuable goods, extracting resources, training, rescuing allies, and
other consequential profession activity.

When a hunter fills their XP bar:

1. the bar resets;
2. the hunter receives one or more Ability Points according to progression
   rules;
3. the next bar requires more XP, or the hunter receives XP less efficiently,
   or both.

There is theoretically no hard level cap. Progression becomes increasingly
difficult so exceptionally powerful hunters remain rare. Safe repetitive
actions should have diminishing rewards or risk/value scaling so trivial loops
cannot efficiently produce top-rank hunters.

Ability Points are a pooled choice. A hunter can:

- invest now in base attributes;
- buy an available ability or passive;
- save points for a more expensive future ability.

NPCs make this same tradeoff according to their goals, build preferences,
weaknesses, current needs, mentors, knowledge, and available opportunities.

### Official hunter rank

Rank is the Association's approximate representation of a hunter's underlying
strength and the gate rank they could probably survive or meaningfully
contribute against.

The canonical rank input is **invested Ability Points**:

`invested AP = AP spent on base attributes + abilities + passives`

- Unspent Ability Points do not increase rank.
- Equipment does not increase official hunter rank.
- Gear, composition, tactics, injuries, matchups, and player skill still change
  actual combat performance.
- A higher-rank hunter has definitively more invested personal growth than a
  lower-rank hunter, but is not guaranteed to win every matchup or survive an
  equally ranked gate.

The working target is approximately five to seven progression levels per letter
rank. Exact thresholds, XP curves, Ability Point awards, and the rank letters
themselves are **tunable data**.

Most newly awakened hunters begin in lower ranks, while rare generated hunters
may awaken with enough invested power to begin at S rank or an equivalent top
tier. Hunters can rise over time through the same progression system.

### Ability ownership and loadouts

Hunters can learn more abilities than they can equip. The production combat
loadout target is:

- three normal cooldown abilities;
- one ultimate ability;
- a limited number of passive slots;
- weapon and gear movesets that remain distinct from learned ability slots.

Loadouts can be changed while safe, not freely in active combat. Ability costs,
prerequisites, affinities, passive-slot count, and ultimate-resource rules are
**tunable**.

Rare specializations belong to the shared system. Necromancy, reanimation, and
summoning can appear within specialized Mage builds available to NPCs and the
player. Rank, invested abilities, mana, and build determine summon count,
quality, duration, control, and what can be raised or summoned. These powers are
not protagonist-exclusive gifts.

## Agent agency and decision-making

Each autonomous hunter continuously chooses what to do through inspectable,
data-driven reasoning. Candidate actions may include:

- seek work, train, rest, recover, trade, travel, socialize, or investigate;
- allocate Ability Points and alter a build;
- buy, sell, repair, equip, or reserve gear;
- bid for a gate, recruit a party, join a raid, retreat, or rescue someone;
- join, leave, found, lead, or betray a guild;
- obey, refuse, negotiate, or subvert an assignment;
- help, avoid, confront, ambush, steal from, or assassinate another hunter.

Decisions should use understandable factors such as:

- life goals and current needs;
- traits, fears, values, and risk tolerance;
- expected reward, danger, travel, and opportunity cost;
- current build, injuries, gear, supplies, and party role coverage;
- relationships, loyalty, authority, reputation, and remembered events;
- beliefs about the target or opportunity, including uncertain information.

The developer layer must expose candidates, named score contributions,
rejections, selected action, relevant memories, knowledge sources, and random
provenance. Normal gameplay does not reveal this omniscient decision inspector.

## Knowledge, scouting, and fog of information

The simulation distinguishes world truth from what each actor believes.

The player does not automatically know a hunter's private goals, full traits,
weaknesses, relationships, build, or plans. A Nemesis-style dossier grows
through:

- meeting and spending time with the hunter;
- fighting beside or against them;
- scouting reports;
- guild records and Association data;
- surviving witnesses;
- rumors, confessions, physical evidence, and investigations;
- magical, communication, or surveillance abilities and items.

Knowledge records should retain source, confidence, age, and subject. Reports
can be incomplete, outdated, biased, or false. Normal UI reveals only
information the controlled hunter or their organization actually knows through
an in-world source.

The developer/debug layer remains omniscient and can inspect hidden traits,
true gate modifiers, decisions, evidence, and canonical encounter state.

Remote live observation of a strike team requires an appropriate communication
or surveillance ability/item. Without one, a guild leader receives only the
reports and limited status information their systems support. Developer
spectating is unrestricted.

## Shared world, time, and population

### World clock

Town, wilderness, gates, visible encounters, and abstract encounters share one
in-game clock. The outside world continues at the same rate while a player is
inside a dungeon.

A loading or materialization transition may briefly pause simulation as a
technical safeguard, but it does not create a separate timeline. After the
transition, the shared clock resumes consistently.

Time controls may pause or accelerate the world where gameplay permits. Exact
time scale and acceleration restrictions are **tunable**. No world activity is
simulated while the application is closed.

### Population lifecycle

New hunters awaken every day. This replenishment offsets a profession in which
death and retirement are normal exits.

Hunters receive full persistent simulation. Civilians are intentionally
lightweight background inhabitants who fill towns, cities, roads, and parts of
the wilderness. A civilian generally has minimal state, approximately one hit
point, simple schedules or travel intent, and a strong flee-danger response.
They do not receive the full hunter personality, progression, gear, and combat
model unless promoted into a more important role.

Civilian death, displacement, and infrastructure damage may still affect
regional population, reputation, emergency work, prices, and public safety.

### Scale targets

The first serious ecosystem target is:

- 60 to 100 persistent hunters;
- 4 to 6 active guilds;
- 2 to 3 towns in one region;
- several markets, hospitals, wilderness sites, and travel routes;
- roughly 5 to 15 concurrent gates and contracts.

The final ambition is approximately 500 to 1,000 active hunters across multiple
overworld maps, with many gates, parties, guilds, and concurrent offscreen
events. Exact caps depend on profiling, but architecture must remain capable of
moving beyond the serious regional slice toward the final multi-map population.

## Gate and Association lifecycle

### Canonical gate manifest

A gate is generated as persistent world content before a party enters it. Its
canonical manifest contains enough information for both abstract and real-time
resolution:

- stable identity, location, creation time, and final instability deadline;
- true rank and Association-appraised rank;
- visible and hidden modifiers;
- boss identity and combat definition;
- enemy types, counts, and mob-pod groupings;
- topology or encounter progression state;
- loot tables, placed chests, hidden discoveries, and consumable opportunities;
- extractable resources such as mana crystals;
- current ownership, auction, raid-rights, and completion state.

Rare hidden modifiers can cause the Association to misclassify a gate below its
true danger. Misclassification is exceptional, not routine, and remains unknown
until valid evidence exposes it.

### Appraisal, auction, and raid rights

The Hunters Association appraises gates and sells exclusive raid and loot
rights through a mission/auction-board application.

Independent strike-team leaders and guilds can bid. A winning bidder receives
the right to enter, close the gate, and retain its legal loot under the contract
terms. Party-size quotas and other entry requirements may vary by gate and
Association rule.

The winning team has two in-world days to close the gate. If the team dies,
retreats, refuses to continue, misses the window, or otherwise fails, the rights
return for reauction. Repeated failure or prolonged instability can produce a
dungeon break.

A dungeon break releases monsters into the world and can:

- kill or displace civilians;
- damage towns, roads, facilities, and resource sites;
- create emergency contracts and rescue work;
- disrupt markets and travel;
- damage the responsible guild's or leader's reputation;
- create memories, grief, blame, political pressure, and retaliation.

Traits, goals, criminal incentives, and relationships may drive hunters to
enter another team's gate illegally, poach resources, steal loot, sabotage a
raid, ambush a strike team, or assassinate a target.

## Strike teams and raid lifecycle

A typical gate loop is:

1. A gate manifests independently; the Association discovers, registers, and
   appraises it.
2. Hunters and guilds evaluate the opportunity and bid for rights.
3. The winner forms a legal strike team that satisfies applicable quotas.
4. Leaders recruit missing tactical roles based on available people, builds,
   relationships, cost, and risk.
5. The team travels, equips, enters, and commits resources.
6. It encounters mob pods, hidden discoveries, chests, and extractable
   resources while deciding whether to press on or retreat.
7. It fights or avoids the boss and attempts to close the gate.
8. Survivors recover bodies, stabilize allies, divide or contest loot, report
   events, and return to the world.
9. Rewards, injuries, deaths, durability, XP, Ability Points, memories,
   relationships, reputation, prices, and guild state feed future decisions.

A hunter of the same official rank as a gate is not safe. Composition,
matchups, hidden modifiers, supplies, tactics, behavior, and chance matter.

## Encounter simulation and materialization

### One encounter, two fidelity levels

Encounters beyond the player or developer camera are not rendered. They advance
through a lightweight heartbeat resolver using canonical hunter, monster, gate,
and encounter state.

Encounters involving the player or deliberately spectated by the player or
developer run through real-time combat.

The abstract heartbeat may resolve:

- movement between encounter areas and mob pods;
- target selection and tactical orders;
- attacks, abilities, cooldowns, mana, health, shields, and status effects;
- consumables, durability, healing, incapacitation, rescue, and death;
- retreat, resource extraction, chest or hidden-area discovery;
- boss progress and gate closure.

### Promoting an abstract encounter

When an observer joins or views an already abstracted encounter:

1. pause that encounter safely for transition;
2. load its canonical gate manifest and current encounter snapshot;
3. spawn the exact surviving hunters, mobs, objects, and remaining resources;
4. apply current attributes, health, mana, cooldowns, injuries, items,
   durability, cleared pods, discoveries, deaths, and objectives;
5. place actors in a defensible representation of their current encounter area;
6. resume under real-time combat and the shared world clock.

When no authorized observer remains, the real-time encounter can serialize its
truth back into the canonical snapshot and return to heartbeat resolution.
Promotion and demotion must never duplicate loot, resurrect actors, reset
cooldowns, restore consumables, or restart cleared content.

### Observation invariance and autoresolve accuracy

Observation applies no modifier to combat odds, stats, AI, rewards, targeting,
or random weights. Merely watching must not help or harm either side.

Abstract and real-time combat do not need to produce an identical hypothetical
future because one is a lower-fidelity approximation and real-time play permits
continuous movement and intervention. The target is statistical parity across
many matched encounters, not impossible frame-for-frame counterfactual
identity.

Autoresolve must be measurable and calibratable. Replayable seeds, canonical
encounter snapshots, outcome distributions, casualty rates, resource use, and
real-time-versus-abstract comparison tools are production requirements.

## Combat consequences: injury, rescue, and death

Raiding is a dangerous profession and permanent death should be common enough
to shape world history.

Defeat can cause immediate death or incapacitation. The outcome depends on
accumulated trauma, defenses, attack type, and especially the severity of the
finishing hit. A catastrophic hit may kill outright. A heavier nonlethal hit
creates a shorter rescue window.

An incapacitated hunter can be:

- rescued or healed by an appropriately capable party member;
- stabilized temporarily with a suitable item;
- extracted and treated at a hospital;
- abandoned, captured, robbed, or allowed to die.

Healers, hospitals, and items have treatment grades. A treatment can address
injuries up to its capability. More severe injuries may require rare expertise,
long recovery, or remain permanent.

Injuries may be:

- temporary and treatable during the encounter;
- recoverable over days or longer periods;
- persistent until a sufficiently strong treatment is found;
- permanent.

Death changes rosters, succession, party capability, relationships, goals,
reputation, contracts, and future decisions. It never silently deletes the
hunter's history.

## Guilds, strike teams, and conflict

### Guild creation

A hunter may found a guild after meeting shared world requirements. Locked
requirements include:

- paying an Association registration fee;
- completing a minimum amount of raid experience.

Additional licensing, roster, reputation, or headquarters requirements are
**tunable**. Low-rank hunters are not categorically barred from leadership.

### Guild behavior

Guilds are political and economic actors. They recruit, equip, train, schedule,
bid, raid, protect, investigate, negotiate, claim resources, remember conflict,
and pursue leader- or culture-derived goals.

Guild and strike-team membership does not make hunters puppets. Assignments and
invitations pass through the same agency, relationship, risk, and incentive
rules used elsewhere.

### Succession and fracture

When a guild leader dies or retires, leadership passes first to the most
appropriate established strike-team leader. If none exists, it falls to the
hunter with the highest invested Ability Points.

Succession is a trigger, not a guarantee of stability. Personalities,
relationships, rival claims, ideology, grief, loyalty, and ambition can cause
members to accept the successor, defect, split into factions, form new guilds,
or dissolve the organization.

### Reasons for conflict

Guild wars and covert violence should emerge from concrete incentives:

- competition for profitable high-rank gate rights and lower rival bids;
- control of mana crystals, trade routes, facilities, and wilderness resources;
- weakening a rival before an auction or major raid;
- recruitment poaching, defections, succession disputes, and stolen specialists;
- bounties, debt, loot theft, sabotage, or contract interference;
- revenge for death, betrayal, abandonment, humiliation, or prior violence;
- leader rivalries, guild culture, alliances, and Association politics.

War is remembered by people. Ending a formal conflict does not erase personal
grudges or debts.

## Crime, witnesses, and safe zones

Hunter-versus-hunter combat is mechanically disabled in towns. Hostile hunter
targeting, damage, and status application must be blocked through every weapon
and ability path. Towns are hard PvP safe zones, not merely places with severe
penalties.

Hunter-versus-hunter violence is permitted by the combat rules in gates and the
wilderness. Whether anyone knows a crime occurred is a separate knowledge
problem.

A killing can appear to be an ordinary monster casualty when no witness,
evidence, confession, or investigation connects it to the perpetrator. Crimes
may become known through:

- surviving witnesses and their reliability;
- bodies, wounds, missing loot, tracks, or magical residue;
- communication records or remote observation;
- confessions, rumors, informants, and betrayals;
- Association, guild, or magical investigation.

Consequences can include suspicion, private revenge, bounties, Association
sanctions, lost reputation, contract exclusion, guild retaliation, or broader
war. There is no omniscient global crime flag in normal gameplay.

## Economy and material pressure

The economy exists to create decisions and stories, not to update every market
every frame.

Its causal loops include:

- gate bids and legal raid rights;
- loot, mana crystals, crafting resources, and consumables;
- equipment supply, tier, movesets, durability, repairs, and replacement;
- hospitals, healers, stabilization items, and recovery costs;
- guild treasuries, wages, recruitment, facilities, and strike-team budgets;
- town markets, regional supply and demand, resource sites, and trade routes;
- dungeon breaks, deaths, destroyed infrastructure, scarcity, and price shocks.

Markets and distant economic actors update on coarse heartbeats and events
rather than multiple times per second. Exact pricing formulas, production
chains, update cadence, crafting depth, and resource ownership models are
**tunable**. Economic outcomes must still be persistent, explainable, and tied
to actual transfers rather than cosmetic numbers.

## Presentation and information layers

### Normal gameplay

Normal UI should include, as systems mature:

- character creation and generated-hunter options;
- hunter dossier, build, equipment, injuries, known history, and relationships;
- Association gate appraisal and auction board;
- party recruitment, role needs, invitations, bids, and deadlines;
- map, travel, markets, hospitals, guild management, and communication tools;
- known world events, reports, rumors, evidence, and consequences;
- raid HUD, tactical orders, rescue state, loot, and extraction progress;
- death life-summary and succession choices.

Normal UI respects knowledge and communication limits.

### Developer/debug layer

The developer layer may:

- inspect every hunter's goals, traits, relationships, memories, and knowledge;
- view utility candidates, score factors, rejections, and selected actions;
- inspect true and appraised gate manifests and hidden modifiers;
- teleport or spectate encounters without an in-world communication item;
- pause, accelerate, seed, replay, materialize, and dematerialize simulation;
- compare abstract and real-time combat outcomes;
- validate invariants, event provenance, performance, and save continuity.

Developer omniscience must not accidentally leak into normal player UI.

## Technical and production principles

### Canonical state and stable identity

- Hunters, guilds, gates, encounters, items, memories, evidence, and world
  events use stable IDs independent of scene objects.
- One authoritative persistent world state owns truth.
- Presentation reads state and emits intent; it does not own or silently repair
  domain state.
- Rendered GameObjects are projections of canonical actors, not their identity.

### Shared actions and outcomes

- Player input and AI decisions produce the same typed action requests.
- Shared validation enforces costs, eligibility, location, authority, timing,
  targeting, and safe-zone rules.
- Shared execution applies outcomes atomically and records causal events.
- A player-only bypass is a defect unless explicitly documented as a developer
  command.

### Simulation scalability

- Decisions, economy, travel, and combat use purpose-appropriate heartbeats,
  spatial relevance, and time slicing.
- Offscreen hunters do not require live GameObjects, Animators, NavMeshAgents,
  or per-frame `Update` methods.
- Nearby and observed actors may receive full presentation and real-time
  behavior while distant actors remain data.
- Collections, memory retention, event history, and AI candidate generation
  require explicit budgets and pruning policies.
- Performance must be profiled at the 100-hunter target and instrumented toward
  the 500-to-1,000-hunter ambition.

### Persistence and reproducibility

- Saves are versioned, validated, and migrated without silently discarding
  valid history.
- Normal and Ironman modes share the same world schema.
- Canonical encounter snapshots survive save/load at either fidelity level.
- Seeded random streams and event provenance are used where practical for
  reproduction and balance analysis.
- Observation is never included as a combat-strength or reward factor.

### Data-driven extension

Stats, abilities, passives, gear movesets, monsters, bosses, gate modifiers,
injuries, treatments, XP rewards, rank thresholds, markets, contracts, traits,
goals, and decision factors should be authored as inspectable data where
practical. Adding content should extend shared systems rather than require a
new player-only or NPC-only code path.

## Current prototype versus production target

`Assets/Scenes/2D Ecosystem.unity` currently proves a serious mechanics-first
regional campaign simulation. Its implementation contract is documented in
`Docs/Guild-Ecosystem-Prototype.md`.

The current prototype starts with 80 active hunters, five guilds, three towns,
market and hospital facilities, 5–15 live contracts, and daily awakening and
retirement churn. Discrete day ticks, simple contracts, unrestricted developer
inspection, and fully abstract dungeon resolution remain implementation-stage
constraints only. They do not override the production targets in this document.

Important bridges still required include:

1. continuous shared world time and scalable heartbeat scheduling;
2. generated gate manifests, appraisal, auctions, two-day rights, reauction,
   and dungeon breaks;
3. incapacitation, severity-based rescue, treatment tiers, lasting injury, and
   common permanent death;
4. player creation, life summary, succession, and Ironman behavior;
5. knowledge-limited normal UI, evidence, rumors,
   crime, and communication;
6. lossless abstract-to-real-time encounter materialization;
7. deeper batched economy, durability, treatments, civilian context, and
   measurement of long-run emergence at the implemented 60–100 active scale.

Combat-specific contact, dodge, movement, feedback, ability, and authoring
details remain in `Docs/Weapons-Testing.md` and `Docs/Combat-Abilities.md` and
must obey the shared-agency and canonical-state rules here.

## Production acceptance tests

The vision is being realized only when the following behaviors are demonstrable.

### Emergent lives

- Across many seeds, ordinary low-rank hunters can independently suffer injury,
  replace gear, reunite or separate, recruit, raid, progress, lead, retire, or
  die through generic systems.
- Different seeds produce materially different causal histories and guild
  landscapes.
- Every displayed story beat links to canonical events and affects later state
  or decisions.
- No test depends on a scripted protagonist or John-specific content.

### Player and NPC parity

- Player and NPC requests for progression, equipment, bidding, recruitment,
  trade, raiding, rescue, retreat, guild creation, and crime use shared rules.
- Identical canonical actors receive no hidden player bonus or immunity.
- NPCs can create viable hybrid builds, save Ability Points, buy abilities, and
  make imperfect choices for understandable reasons.

### Progression and rank

- Filling XP awards Ability Points and advances the progression curve.
- Unspent points do not change official rank; investing points can cross a
  configured rank threshold.
- Gear changes movesets and real combat effectiveness without changing official
  rank.
- Safe repetitive actions cannot efficiently create top-rank hunters.

### Gate lifecycle

- Generated gates retain true/appraised rank, visible/hidden modifiers, mobs,
  pods, boss, resources, loot, rights, and deadlines.
- Bid, award, two-day attempt, completion, failure, reauction, and dungeon-break
  branches preserve ownership and pay rewards exactly once.
- Rare misclassification remains hidden until legitimately discovered.

### Injury, death, and succession

- Attack severity produces appropriate outright death or an incapacitation
  window.
- Healers, items, extraction, and hospitals respect treatment capability.
- Temporary, long-term, and permanent injuries persist correctly.
- Player death produces an accurate life summary and continuation in the same
  world without resurrecting the deceased.
- Leader death can cause orderly succession, fracture, defection, or collapse.

### Encounter fidelity

- Materializing an abstract raid preserves participants, health, mana,
  cooldowns, consumed items, durability, cleared pods, deaths, discoveries,
  loot, resources, boss progress, and objectives.
- Dematerializing returns real-time truth without duplication or reset.
- Repeated observation without intervention produces no camera-related combat
  modifier.
- Large matched samples keep abstract and unattended real-time outcome,
  casualty, and resource-use distributions within configured tolerances.

### Knowledge and crime

- Normal players cannot inspect hidden motivations, modifiers, or events
  without a valid source.
- Witness-free crime does not create omniscient certainty.
- Witnesses, evidence, rumors, and investigations propagate source, confidence,
  and age and can produce suspicion or false belief.
- Hunter PvP cannot damage or hostile-target another hunter in a town through
  any weapon or ability path.
- Remote spectating respects communication requirements outside developer mode.

### Economy, population, persistence, and scale

- Raid materials, casualties, repairs, treatment, bids, and infrastructure
  damage cause persistent economic effects on a coarse cadence.
- Daily awakenings, deaths, and retirement can reach a tunable population
  equilibrium rather than uncontrolled collapse or growth.
- Save/load preserves identities, deaths, encounter snapshots, auction timers,
  knowledge, inventories, memories, and causal event history.
- Long simulations contain no duplicate identities or items, orphaned
  membership, impossible life states, negative conserved resources, or
  unbounded history growth.
- The 60-to-100-hunter target runs within measured budgets without requiring
  rendered offscreen actors, with telemetry that informs scaling toward 1,000.

## Locked rules, tunable values, and deferred detail

### Locked

- The game is an emergent story generator, not a scripted protagonist arc.
- The player directly controls one ordinary hunter and uses shared world rules.
- Death is permanent in world history; succession continues the same world.
- Player death produces a canonical life summary and offers a new hunter,
  takeover of an eligible existing hunter, or return to the main menu.
- Normal saving/reloading and optional Ironman mode are both supported.
- No offline progression; one shared in-game clock.
- XP grants pooled Ability Points; only invested points affect official rank.
- Class labels are descriptive and hybrids are valid.
- Loadout target is three normal abilities, one ultimate, and limited passives.
- Gear grants movesets plus tier-appropriate damage/protection and durability.
- Leadership influences rather than mind-controls hunters.
- Gates have persistent manifests, Association auctions, two-day raid rights,
  failure/reauction, hidden modifiers, and possible dungeon breaks.
- Offscreen combat is abstract; observed combat is real-time; observation never
  modifies odds.
- Injury, incapacitation, rescue, lasting wounds, and common permadeath matter.
- Guild registration requires an Association fee and raid experience.
- Guild succession prefers an established strike-team leader, then the hunter
  with the highest invested Ability Points, while relationships may cause a
  split or collapse.
- Player knowledge is limited; developer inspection is omniscient.
- Remote normal spectating requires communication capability.
- Hunter PvP is hard-disabled in towns and allowed in gates/wilderness.
- New hunters awaken daily; hunters leave through death or retirement.
- The serious ecosystem targets 60-to-100 hunters before scaling toward
  500-to-1,000.

### Tunable

- Final stat list and formulas;
- XP rewards, curve, diminishing-return model, and Ability Point awards;
- exact rank thresholds within the five-to-seven-level working target;
- ability costs, affinities, prerequisites, passive-slot count, and cooldowns;
- gate distributions, quotas, modifier rates, break timing, and auction values;
- damage, protection, durability, injury, rescue, treatment, and mortality
  formulas;
- guild licensing details, wages, facilities, sanctions, and territory rules;
- economy formulas, update cadence, production, crafting, and price elasticity;
- population generation, awakening, retirement, and migration rates;
- time scale, heartbeat frequency, retention limits, and performance budgets;
- UI presentation, final terminology, names, art, and audio.

### Deferred

- Final world map count and geography;
- complete stat and skill-tree catalogs;
- exact authored monster, boss, gear, and gate content volume;
- final communication/surveillance mechanics;
- detailed civilian schedules beyond lightweight world support;
- endgame structure and any finite campaign victory condition.

## Guardrails

Do not:

- script the abridged example hunter or guild as the required campaign story;
- grant the player unique progression, survival, knowledge, or combat rules;
- turn classes into rigid code paths that prohibit intended hybrids;
- reduce equipment identity to stat bonuses alone;
- let camera presence alter simulation outcomes;
- expose omniscient developer information in normal gameplay;
- allow hunter PvP to bypass town safe-zone validation;
- run full AI, economy, navigation, animation, or physics for every offscreen
  hunter every frame;
- silently discard world history to recover from invalid state or save data;
- treat the regional mechanics prototype's shortcuts as final design constraints.

## Glossary

**Ability Point (AP):** A pooled progression resource spent on base attributes,
abilities, or passives. Only spent AP contributes to official rank.

**Association:** The institution that appraises gates, operates the auction
board, licenses guilds, records legal raid rights, and may investigate or
sanction hunters.

**Canonical state:** The single authoritative data representation of world
truth, independent of whether an entity is rendered.

**Class or role:** A descriptive label inferred from a hunter's attributes,
abilities, gear, and behavior. It is not a permanent class lock.

**Gate:** A persistent generated dungeon opportunity with a true/appraised
rank, manifest, modifiers, inhabitants, resources, loot, rights, and deadline.

**Heartbeat:** A coarse simulation update for offscreen decisions, economy,
travel, or combat. Different systems may use different tuned cadences while
sharing one world clock.

**Hunter:** A full persistent agent who can progress, equip, decide, relate,
raid, lead, suffer injury, retire, and die under shared rules.

**Materialization:** Promoting canonical abstract encounter state into rendered
real-time actors without changing its truth.

**Strike team:** A temporary or persistent party organized to complete a gate
or other dangerous operation, with a leader and tactical composition.

This contract should change only through an explicit design decision. Prototype
convenience is not sufficient reason to weaken an invariant.
