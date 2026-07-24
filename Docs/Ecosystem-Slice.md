# Ecosystem Slice

`Assets/Scenes/Ecosystem Slice.unity` is the first end-to-end living-world
vertical slice. It connects persistent hunters, gear-granted movesets, guild
recruitment, mission selection, autonomous decisions, raid resolution,
relationships, memories, injuries, rewards, and save/load into one playable
loop.

## Play loop

1. Inspect the selected hunter and mission.
2. Invite unaffiliated hunters to Azure Wake.
3. Propose a raid. Hunters evaluate the invitation using their goals, traits,
   relationship with the player, wounds, and mission preferences.
4. The assembled party resolves the expedition and receives wounds, experience,
   guild resources, and relationship memories.
5. The rest of the world continues to choose gear, join guilds, and attempt
   missions whether or not the player intervenes.
6. Continue on later days with the consequences preserved.

## Controls

- `WASD`: move
- Mouse: orbit/aim the camera
- Left mouse button: use the equipped gear attack
- `1`, `2`, `3`: equip Vanguard Blade, Titan Greatsword, or Rift Daggers
- Left/Right: select a hunter
- Up/Down: select a mission
- `I`: invite the selected hunter to Azure Wake
- `P`: propose the selected raid
- `T`: advance one simulation day
- `F5`: save immediately
- Mouse wheel: scroll the World Events history

The simulation also advances automatically every 18 seconds.

## Character presentation

Player and hunter records are presented through a reusable articulated
humanoid rig with separate torso, head, arms, legs, hands, and equipment.
Movement drives a procedural walking gait. Gear attacks drive distinct body
poses for the Vanguard Blade, Titan Greatsword, and Rift Daggers, while hunters
attempting missions periodically perform their equipped attack animation.

These procedural bodies are a dependency-free prototype layer. They can later
be replaced by imported humanoid models and animation clips without changing
the persistent hunter or simulation data.

### ExplosiveLLC animation pack

The imported RPG Character Mecanim pack under `Assets/ExplosiveLLC` contains a
humanoid character, unarmed animations, and two-handed sword animations. Its
demo materials originally used the Built-in Standard shader; they have been
converted to URP Lit with:

`Turtle > Rendering > Convert ExplosiveLLC Materials to URP`

The converter is scoped to the vendor folder and is safe to run again after a
package update. The pack's demo controllers expect legacy Input Manager presets.
Ecosystem Slice should reuse the model and animation clips while retaining
Turtle's existing Input System and gameplay controllers.

## Persistence boundary

The district, guild halls, dungeon gates, training yard, camera, player setup,
and controller wiring are serialized in the scene asset. They remain visible
and editable outside Play Mode.

Hunters are runtime views of persistent world records because their membership,
gear, wounds, activities, and relationships change as the simulation runs.
Their state is saved as versioned JSON at:

`Application.persistentDataPath/ecosystem-slice-v1.json`

This split is intentional: authored geography belongs to the level; changing
people and world history belong to the simulation.

## Scope

Raid outcomes are currently resolved by an inspectable utility simulation.
The scene proves the campaign loop and feeds its consequences back into
character state. A later milestone should replace selected simulated outcomes
with a transition into an authored, fully playable dungeon encounter.
