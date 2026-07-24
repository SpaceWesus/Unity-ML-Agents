# Game Vision: A Living World of Hunters

## North star

Build a bustling world where the player is one ambitious hunter among many autonomous, persistent people. The player can pursue any combat identity, found a guild, recruit hunters, clear dungeons and raids, accumulate resources and territory, form rivalries, and become involved in wars whose consequences are remembered.

The world should continue generating stories without waiting for the player. Hunters and guilds take jobs, form parties, compete for loot, fail expeditions, change allegiances, improve their equipment, and react to past treatment.

## Design pillars

### Gear grants verbs

Equipment primarily changes what a character can do. Weapons, armor sets, relics, and artifacts grant movesets, active abilities, counters, mobility options, summons, or tactical rules. Plain numerical bonuses are secondary.

This lets a hunter's class emerge from their equipment and chosen abilities rather than from a permanent class selection.

### Reciprocal agency

Almost every meaningful choice offered to the player should also be available to autonomous hunters when applicable:

- choose equipment and abilities
- accept or refuse missions and raids
- join, leave, betray, or create guilds
- recruit and evaluate other hunters
- pursue goals and relationships
- contest territory and resources
- remember favors, defeats, insults, abandonment, and rescue

Agents do not need identical interfaces to the player, but they should operate under compatible world rules.

### Unique, persistent hunters

Hunters should feel closer to Shadow of War's memorable Nemesis characters than disposable spawned units. Identity can be composed from:

- stable identity, appearance, voice, and history
- level, attributes, injuries, and potential
- equipped movesets and learned abilities
- goals, fears, values, traits, and behavioral inclinations
- friendships, rivalries, loyalties, grudges, and debts
- memories of specific world events and participants
- reputation and standing within guilds and settlements

Uniqueness should change decisions and create recognizable behavior, not merely generate biography text.

### Guilds are political actors

Guilds recruit, equip, schedule, negotiate, raid, claim territory, and remember conflict. They may cooperate, compete economically, feud, or go to war for material and personal reasons.

The intended flavor combines autonomous campaign movement and faction politics from Mount & Blade/Warband/Bannerlord with personalized relationship-driven rivals.

### Simulation supports authored combat

The world simulation creates context, stakes, and opponents. Dungeons, raids, bosses, gear movesets, and moment-to-moment combat still need deliberate encounter and feel design. Simulation should produce stories without making combat unreadable or arbitrary.

## Architectural implications

- Give persistent hunters stable IDs and serializable state; do not identify them by scene objects.
- Separate hunter decisions and relationships from presentation MonoBehaviours.
- Represent gear-granted moves as data plus executable ability behavior.
- Record consequential memories as structured facts that decision scoring can consume.
- Use the same mission, party, inventory, guild, and relationship rules for player-facing and AI-facing actions where practical.
- Keep simulation frequency scalable: distant hunters can make coarse decisions while nearby actors receive full simulation.
- Prefer deterministic, inspectable utility scores for early autonomous decisions before considering ML-driven behavior.
- Preserve authored exceptions and designer control over bosses, story events, and signature characters.

## Near-term prototype path

1. Make Homies a satisfying combat laboratory.
2. Extract attacks into gear-granted move definitions.
3. Create a persistent `HunterProfile` model with identity, traits, goals, relationships, equipment, and memories.
4. Let several hunters choose equipment and parties using inspectable utility scores.
5. Add guild membership, mission offers, and autonomous dungeon resolution.
6. Connect simulation outcomes to playable raids and rival encounters.

Steps 2 through 5 now have a first integrated proof in
`Assets/Scenes/Ecosystem Slice.unity`. The slice deliberately resolves raids in
the campaign simulation for now. The next major bridge is to turn a selected
raid into a playable encounter and return its casualties, loot, memories, and
relationship changes to the same persistent world state.

This document is a direction-setting constraint, not a promise to implement the entire simulation inside the current combat prototype.
