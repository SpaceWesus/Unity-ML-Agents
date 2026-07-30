# Combat ability system

The Weapons Testing ability system is shared by player-controlled and
AI-controlled hunters. A character's command source chooses actions, while the
same `Combatant` and `CombatAbilityController` enforce activation, cancellation,
cooldown, damage, barrier, and ultimate rules.

## Loadout contract

`CombatAbilityLoadoutDefinition` contains:

- three regular cooldown ability slots;
- one ultimate slot powered by ultimate charge;
- a variable passive list in the current prototype;
- ultimate gain rules for damage dealt and received.

The production design limits the number of equipped passive slots while still
allowing a hunter to learn more passives than can be equipped. The exact passive
slot count is tunable. See `Docs/Game-Vision.md` for the authoritative loadout
and progression contract.

Loadouts do not enforce classes. Discipline names such as `Mage`, `Barrier
Magic`, or `Spatial Magic` are descriptive authoring metadata. Any hunter can
equip any compatible ability or passive, allowing hybrid builds without
creating special hybrid classes in code.

## Runtime rules

- Press `1`, `2`, or `3` for regular abilities and `4` for the ultimate.
- Cooldowns begin only when an ability commits after its cast time. Dodging or
  being interrupted before commitment does not consume the cooldown.
- Every ability declares whether it permits movement and whether it may be
  dodge-cancelled. New abilities default to mobile and dodge-cancellable.
- Ability damage uses the same teams, health, hit reaction, knockback,
  hit-marker, death, and ultimate-gain paths as weapon damage.
- Barriers absorb damage before health and expire when their capacity or
  lifetime is exhausted.
- Resetting or respawning a combatant clears cooldowns and barriers and restores
  the loadout's configured starting ultimate charge.
- AI evaluates the same equipped slots and readiness state as the player. Its
  current prototype heuristics consider ability intent, range, health, target
  spacing, and ultimate readiness.

## Mage prototype loadout

The initial loadout demonstrates four different extension points:

1. `Arcane Bolt` — a pooled, swept-collision projectile.
2. `Spatial Step` — collision-respecting forward repositioning.
3. `Aegis Barrier` — temporary damage absorption.
4. `Arcane Nova` — an ultimate area attack.

`Arcane Tempo` demonstrates a passive asset that modifies cooldown duration,
ultimate gain, and ability damage. Weapons remain independent from the ability
loadout.

## Authoring a new ability

1. Derive a stateless ScriptableObject from `CombatAbilityDefinition`.
2. Store tuning and presentation data on the definition.
3. Implement `Activate(CombatAbilityContext)` without storing per-cast mutable
   state on the asset.
4. Put any continuing runtime state on a spawned component or the caster's
   `CombatAbilityController`.
5. Create the asset through its `Create` menu and assign it to any loadout.
6. Set AI intent and useful range so agents can reason about it.

Definitions decide what an ability does. `CombatAbilityController` remains the
authority for whether it can be used and for spending its resource. This keeps
future effects such as curses, summons, healing, walls, stealth, and class-tree
passives composable without duplicating cooldown logic.

## Scene tooling

Use `Turtle > Combat > Upgrade Ability System In Weapons Testing` to create the
Mage prototype assets and persistently attach the loadout to existing player
and AI combatants without rebuilding the arena.

Future full scene builds also include the ability system automatically.
