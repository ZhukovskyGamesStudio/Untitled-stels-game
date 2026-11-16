# House generation: session notes, research and roadmap

Saved 2026-11-16. Branch `Gameplay_rooms_test`, nothing committed yet. The design of the generator itself is in
[HouseGenerator.md](HouseGenerator.md); this file is what changed in this round, what we learned, and what comes next.

## 1. What was done in this session

### Robots and the storage room
- **Worker loot errands**: the worker picks loot up and puts it down with a front-loader lift (arms + level tray,
  `Tools/Blender/worker_lift.py`, `CargoMotion`, `RobotController.Loot.cs`, `RobotAnimator.HandleCargo`). Loaded workers
  haul loot to the storage room's shelves and pile (`LevelLayout.StorageDrops`); without a storage room they put it down
  at a patrol stop.
- **Carried loot looks like the real item**: the worker's cargo slot shows the item's own model (`ItemVisuals.Models`),
  not the tetris piece.
- **Storage room** (`StorageRooms`, one per house, ground floor, exactly one door, brings workers):
  locked by default; unlocked for good from its door terminal; open while a worker is at the door or inside (players can
  follow it in) and locked again once no worker is left inside; a player inside can hold E to open it until nobody is in
  the room or at the door (`AutoDoor.StorageLocked/InRoom/AtDoor`, `GameManager.UpdateStorageDoor`, `EscapeKey`).

### Room types
- **Room rules** (`RoomRules`, `BuildingSettings.RoomTypeRules`): per type a min/max area, max aspect, loot limits,
  and now a **zone** and **caps** (see zoning below). Kitchen: 24–64 m², aspect ≤ 2.2, small loot only; window and
  rectangle no longer required; **at most one fridge** (and a lone fridge is put back if the walkability check removed the
  kitchen run: `Furnisher.RestoreFridges`).
- **Test rooms level**: no size limits, every room type at least once, workers only.

### Generators (two modes)
- **Room program (default)**: `RoomProgram.Make` decides the rooms first (types and sizes, corridors not counted), the
  footprint is sized from them (+20 % corridors), free rectangles are cut into exactly those rooms (treemap, rooms ≥ 4 m
  wide); linked houses pack up to 3 rooms per block, the storage room alone in a dead-end block. A typed room is never
  below its type's minimum (`RoomRules.Enforce`).
- **Classic generator** (switch): BSP split into rooms of any size, then each room's type guessed from size and
  proportions (`PlanGenerator.GenerateClassic`, `AssignTypes`/`Fit`). The storage room is a dead end picked afterwards.
  Turn on with the **"Classic generator" toggle in the main menu** or **`-classicrooms`**; like test rooms it is not
  replicated, every peer must match. Plan pictures: `PLANCLASSIC=1 PLANS="7:2" Tools/dev.sh plans`.

### Zoning (step 1 of the roadmap — done)
- `Zoning.Arrange` on the finished plan, both modes: themed rooms over their cap become an everyday type; rooms of a
  floor swap types while that places them better (work rooms on the facade, service rooms inside, toilets by the stairs),
  only where both still hold their rule. Walls and doors do not move; storage and atriums are not swapped.
- Zones: Facade — office, open space, meeting, lounge, library. Interior — server, storage, restroom, copy room, control
  room, nap room, studio, locker room. Anywhere — kitchen, gym, game room, lab, infirmary.
- Caps: one per floor — kitchen, restroom, lounge, copy room; one per house — gym, library, game room, control room, nap
  room, studio, locker room, infirmary. A house kind that at least doubles a type's weight lifts its caps (`HouseKinds`).
- **Stair core**: the first stair hall goes near the lobby, each next one as close to the previous as possible.
- Tests: `ZoningTests`.

## 2. Measurements (72 houses per mode: 12 seeds × houses 0–5, 2 players)

| Metric | Before zoning | Room program now | Classic now | Real offices |
|---|---|---|---|---|
| Rooms per house | 111 | 112 | 104 | 15–40 per floor |
| Work rooms without windows | 33 % | 15 % | 13 % | ~0 |
| Service rooms inside | ~63 % | 90 % | 92 % | most |
| Toilets by the stairs | 27 % | 79 % | 75 % | ~all |
| Kitchens per floor | ~2.5 | 0.86 | 0.77 | ~1 |
| Gyms per house | ~3.3 | 0.9 | 1.0 | 0–1 |
| Corridor share of area | 13 % | — | — | 15–25 % |
| Rooms with 3+ ways in | 51 % | — | — | rare |
| Dead-end rooms | 15 % | — | — | most |
| Independent loops per house | 58 | — | — | a few |
| Depth from lobby by type | flat (9–11 for every type) | — | — | public shallow, private deep |
| Rooms with no furniture | 213 offices (program), 328 (classic) | ~300 | ~500 | — |

Still open after zoning: rooms connect mostly room to room (enfilade) instead of off corridors; depth from the entrance
does not depend on room type; many empty offices and server rooms.

## 3. Research summary

### Floor plans
- **BSP / grid subdivision** — fast, fills space, but rooms are boxes without purpose (types guessed afterwards) and
  layouts come out tree-like. Devlog: https://unusual-intersection.itch.io/subveillance/devlog/492733/procedural-generation-of-building-interiors-part-1
- **Squarified treemaps** (Marson & Musse 2010) — areas split hierarchically (zones → rooms), corridors added after.
  Rectangles only, weak adjacency unless the hierarchy encodes it. https://doi.org/10.1155/2010/624817
- **Constrained growth** (Lopes et al. 2010) — room seeds grow on a grid (rectangle, then L), steered by position, size
  and adjacency constraints. https://graphics.tudelft.nl/~rafa/myPapers/bidarra.GAMEON10.pdf
- **Merrell, Schkufza & Koltun 2010** — a Bayesian network trained on real plans produces the program (rooms, sizes,
  adjacency), stochastic optimisation lays it out. Realistic, slow. https://www.cs.princeton.edu/courses/archive/spring11/cos598A/pdfs/Merrell10a.pdf
- **Graph-first learned models** (Graph2Plan, House-GAN++, HouseDiffusion) — bubble diagram → plan; apartment data,
  not practical at runtime. https://arxiv.org/abs/2004.13204 · https://arxiv.org/abs/2103.02574 · https://arxiv.org/abs/2211.13287
- **Shape grammars / CGA** (Müller 2006) — great for shells and facades, weak for interiors.
  https://course.ccs.neu.edu/cs5150f13/readings/muller_buildings.pdf
- **Wave Function Collapse** — local style, no global structure; better for decor than layout.
  https://www.boristhebrave.com/2020/02/08/wave-function-collapse-tips-and-tricks/
- **Shadows of Doubt** — hand-made floor shells, procedural dividing walls, doors as portals, procedural ducts.
  https://colepowered.com/?p=34642 · https://colepowered.itch.io/shadows/devlog/98636/shadows-of-doubt-devblog-17-ventilation-shafts
- **Cyclic generation** (Dormans, *Unexplored*) — start from one or two cycles (two routes to the goal), add locks, keys
  and shortcuts by graph rewriting. https://www.boristhebrave.com/2021/04/10/dungeon-generation-in-unexplored/
- **Lock-and-key mission graphs** — dependency graph first, then embed in space. https://www.boristhebrave.com/2021/02/27/lock-and-key-dungeons
- **Spelunky templates** — guaranteed path, then hand templates per cell role. https://www.tinysubversions.com/spelunkyGen2/index.html
- **Rooms and mazes** (Nystrom) — reads as a dungeon, not a building. https://journal.stuffwithstuff.com/2014/12/21/rooms-and-mazes
- **Hitman / stealth level design** — readable public / restricted / private zones, loops so the player is never trapped,
  flow diagrams with guard routines first. https://www.gdcvault.com/play/1025879/ ·
  https://www.gamedeveloper.com/design/from-pen-and-paper-to-blockouts-how-level-designers-craft-iconic-stealthy-encounters

### Real office rules
- **Daylight depth**: useful daylight reaches ~6–7.5 m from a window; work rooms on a facade band, corridors / core /
  storage / servers / toilets inside. https://www.fmlink.com/the-importance-of-daylight-to-workplace-performance/
- **Density and grid**: ~10 m² per person; 1.5 m planning grid (cellular office 3.0 × 4.5 m) — fits the 0.5 m cell grid.
  https://www.interaction.uk.com/insight/articles/how-much-office-space-do-i-need-per-person/
- **Core and adjacency** (convention): stairs, lifts, toilets, risers stacked in a core; kitchen next to open plan; server
  room interior; storage near a service entrance; reception and meeting rooms near the lobby; main corridors 1.5–1.8 m.
- **Space syntax**: depth from the entrance and integration on the room graph; public rooms shallow, private deep.
  https://discovery-pp.ucl.ac.uk/10114064/1/Sailer_10.1201_9781003128830-21_chapterpdf.pdf

### Furniture
- **Make it Home** (Yu et al. 2011) — cost of clearance, visibility, pairwise distance/angle, wall alignment, minimised by
  simulated annealing. https://ww3.math.ucla.edu/camreport/cam11-49.pdf
- **Interactive furniture layout** (Merrell et al. 2011) — design guidelines (clearance, circulation, conversation,
  balance, alignment, emphasis) as cost terms. https://graphics.stanford.edu/projects/furniture/
- **Example-based synthesis** (Fisher et al. 2012), **human-centric grammars** (Qi et al. 2018) — functional groups with
  affordance zones. https://graphics.stanford.edu/projects/scenesynth/ · https://yzhu.io/publication/scenesynthesis2018cvpr
- **Learned / LLM** (ATISS, DiffuScene, LayoutGPT, Holodeck) — not for runtime; the useful idea is relational constraints
  plus a solver. https://arxiv.org/abs/2110.03675 · https://arxiv.org/abs/2312.09067
- **Practical**: functional groups, clearance boxes, wall-snapping, weighted rules (we have these), then a short cost-based
  polish pass.

### Stealth
- **Camera / guard placement and solvability** (Xu, Tremblay & Verbrugge, AIIDE 2014). https://ojs.aaai.org/index.php/AIIDE/article/view/12711
- **Stealth path analysis** (Tremblay et al. 2013) — search for undetected paths, heat maps, as a difficulty check.
  https://ojs.aaai.org/index.php/AIIDE/article/view/12591
- Every loot room with two approaches (a vent or squeeze counts); loot value by depth; guards at chokepoints of the deep
  part; keys on the far side of a cycle.

Not worth it here: learned generators at runtime, WFC for room layout.

## 4. Roadmap

| # | Step | Status |
|---|---|---|
| 1 | **Zoning**: facade band for work rooms, interior for service rooms, stair core, caps on themed rooms | **Done** (`Zoning`, `ZoningTests`) |
| 2 | **Corridor-first floors**: most rooms are dead ends off corridors (1–2 doors), with a few planned loops and vents instead of walk-through chains | **Done** (see below) |
| 3 | **Depth from the lobby** (BFS on the room graph): public types shallow, private deep; use depth for loot value, robot density and lock/key placement | Planned |
| 4 | **Adjacency score**: kitchen beside open space, meeting rooms near the lobby, storage near a service entrance; keep the best of several plan attempts | Planned |
| 5 | **Furniture**: fill empty offices and server rooms; then a cost-based polish pass or hand templates for themed rooms | Planned |
| 6 | **Stealth checks** (later): camera coverage on the plan grid, a hidden path to every loot room, mission-graph lock and key | Idea |

### Step 2 result: corridor-first floors (room program; classic is within a few points)
- Wings at least 11 m deep get evenly spaced corridors (one per ~19 m of depth, rooms at most ~8 m from a corridor);
  8–11 m wings get a single-loaded corridor on their inner side; narrower wings have none. `CorridorChance` and house
  kinds still leave some wings open-plan.
- Rectangles next to circulation are cut across it so every room keeps a wall on the corridor (`Treemap` frontage,
  strips at most 2.6× deeper than wide).
- Doors: circulation edges first in the spanning tree (room-to-room only where there is no other way); then per-floor
  budgets placed where they make the longest way round: `LoopsPerRoom` 0.08, `VentsPerRoom` 0.25, `SqueezesPerRoom` 0.1
  (replace the old per-wall `ExtraDoorChance` / `VentChance` / `SqueezeChance`).
- Stair halls of neighbouring floors touch or keep a room's width apart (no 1 m slivers).

| Metric (72 houses) | Before | After |
|---|---|---|
| Rooms touching circulation | 56 % | 66 % |
| Rooms with a door onto circulation | 45 % | 59 % |
| Dead ends (doors/arches) | 40 % | 56 % |
| Dead ends counting vents | 16 % | 31 % |
| Rooms with 3+ walk ways | 21 % | 11 % |
| Room-to-room doors / house | 77 | 51 |
| Doors onto circulation / house | 55 | 71 |
| Walk loops / house | 13 | 4 |
| Vents / squeezes per house | 31 / 11 | 18 / 8 |
| Corridor share of area | 12 % | 16 % |

Stealth trade-off: walk loops dropped a lot; vents and squeezes remain the hidden ways round. Tune `LoopsPerRoom` /
`VentsPerRoom` in `Data/Config/Level` if houses feel too linear. Remaining enfilade comes from open-plan wings (no
corridor by roll or house kind).

### How to measure
A temporary EditMode test generated 72 houses per mode and printed the metrics above (windowless work rooms, service
rooms inside, toilets by the core, caps, corridor share, dead ends, loops, depth by type, empty rooms). Re-create it
when a step needs before/after numbers; plan pictures: `PLANS="7:2,42:3" Tools/dev.sh plans` (add `PLANCLASSIC=1`).
