# Labyrinth

A top-down 2D puzzle maze made in Unity. You carry a lantern through six dark mazes, each a place in
one world that gets more dangerous the deeper you go: from the edge of a forest at night, through a
village with a cemetery, a misty lake, crystal caves and a volcano, to a temple in the volcano's heart.

Each maze is a puzzle first: push and pull crates, find keys for locked doors, flip levers, step on
rune stones in the right order, slide across ice. Enemies patrol the paths; touching one starts a
short dodge-the-bullets fight.

## Levels

| Scene | Level | Place | New idea |
|---|---|---|---|
| Level1 | Tutorial | Forest edge at night | Moving, pushing crates, fights |
| Level2 | 1 – Schlüssel | Village and cemetery | Keys and doors, an ice hall |
| Level3 | 2 – Handschuh | Misty lake | The glove (pull crates), pressure plates, a guard to shove aside |
| Level4 | 3 – Hebel | Crystal caves | A lever chamber, rune stones |
| Level5 | 4 – Schild | Volcano | The shield in fights; two plates, one crate |
| Level6 | 5 – Finale | Temple in the volcano | Everything together, plus a double fight |

## Controls

| Key | Action |
|---|---|
| WASD / arrow keys | Move (in fights: move the light) |
| Walk into a crate | Push it |
| E (hold) | Pull a crate (needs the glove) |
| E (press) | Flip a lever, unlock a door next to you (needs the key) |
| Space | Shield in a fight (needs the shield, once per wave) |
| R | Restart the level (e.g. when a crate is stuck) |
| Esc | Back to the level menu |
| M | Sound on/off |
| 1–6 / C | In the level menu: pick a level / choose another character |

Every defeated enemy gives back one HP; the ♥ in each maze fills your HP and adds one extra.
At 0 HP the level starts over.

## Scenes and flow

```
CharacterSelect → LevelSelect → Level1 → Level2 → … → Level6 → WinScreen
                                   ↕ (additive)
                                 Fight
```

- **CharacterSelect:** pick one of five characters; the choice is remembered.
- **LevelSelect:** pick any level.
- **Level1–Level6:** each scene builds its maze from `Assets/Resources/levels.json` when it starts.
- **Fight:** loaded on top of the running level for an encounter, then unloaded, so the maze keeps
  its state.
- **WinScreen:** after the last level; play again or go back to the level menu.

`GameManager` (game state, HP, abilities) and `SceneLoader` (all scene changes) are singletons on
`Assets/Resources/Game.prefab`, created before the first scene loads and kept across scenes. Reaching
a goal loads the next scene in the build order, so after Level6 the WinScreen follows on its own.
You can press Play in any scene.

## Opening the project

- Unity **6000.3.23f1** (2D, URP), new Input System.
- Open the folder in Unity Hub, then open `Assets/Scenes/CharacterSelect.unity` and press Play.

## Project layout

| Path | What |
|---|---|
| `Assets/Scripts/GameManager.cs` | Game loop: level start, HP, abilities, fights, restart, next level |
| `Assets/Scripts/SceneLoader.cs` | Scene changes: load, reload, next scene, open/close the fight |
| `Assets/Scripts/MazeBuilder.cs` | Turns one level from `levels.json` into GameObjects |
| `Assets/Scripts/PuzzleGrid.cs`, `PuzzleObjects.cs` | Puzzle rules (crates, ice, doors, levers, plates, runes) |
| `Assets/Scripts/PlayerController.cs` | Player movement, pushing and pulling |
| `Assets/Scripts/Encounter/` | The fight: arena, attack patterns, bullets |
| `Assets/Scripts/Themes/` | Look of each world: art, lighting, fog, fireflies, music |
| `Assets/Scripts/DevTools/Autopilot.cs` | Test tool: plays each level's solution by itself |
| `Tools/gen_levels.py` | Level generator and solver (see below) |
| `levels-plan.excalidraw` | The paper plan of the levels the generator starts from |

## Level generator

The mazes are generated and checked by Python scripts (standard library only):

```
python3 Tools/gen_levels.py levels-plan.excalidraw Assets/Resources/levels.json
```

Every level is verified: it can be solved from start to goal, every puzzle really blocks the way,
and every ♥ can be reached with the abilities you have in that level. The seeds in `Tools/seeds.json`
make it repeatable; `--search` looks for new, harder ones (slow).

## Credits

Art, music and sounds: [Ninja Adventure Asset Pack](https://pixel-boy.itch.io/ninja-adventure-asset-pack)
by Pixel-boy and AAA (CC0). A few extra sprites were drawn in the same style. Full list:
`Assets/Resources/Art/CREDITS.txt`.
