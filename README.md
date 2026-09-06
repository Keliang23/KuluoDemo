# 推箱子原型 / Sokoban Demo

Unity **2022.3.51f1** · 2D Built-In Render Pipeline.

Open `Assets/Scenes/SokobanDemo.unity` and press Play.

## First playable

- One simple level, checked with a breadth-first solver. Neutral geometric player / box placeholders; no theme is decided.
- WASD / arrows to move; Z / Backspace to undo; R to restart; Esc for pause menu.
- One box per push; solid map boundaries; smooth movement; delivery feedback and synthesized audio.
- Move / push counters, completion and restart.
- In-game workshop: floor, wall, goal, box, and player brushes; drag to paint; right-click to erase.
- Save / load one custom level as JSON under `Application.persistentDataPath/sokoban-custom-level.json`.
- Playtesting uses a copy of the draft; returning to edit restores the design, not the moved boxes.

The workshop checks map shape, spawn count, and box / goal counts. It does not claim arbitrary custom maps are solvable. Saving replaces the single custom save slot.

## Code

- `SokobanModel.cs`: level format, grid rules, snapshots and original demo levels.
- `SokobanDemo.cs`: temporary immediate-mode UI, board drawing, input, audio and workshop.
- `SokobanSetup.cs`: demo scene setup and rule / level validation. Run **Tools > Sokoban > Validate Demo**.

Presentation is intentionally self-contained and uses generated shapes and sound, without downloaded art or packages. The board is drawn in the Game view during Play; the Scene view contains the camera and controller. The original SampleScene is preserved. This is a prototype; sprite / Tilemap presentation and a larger authored campaign can replace the temporary UI later.
