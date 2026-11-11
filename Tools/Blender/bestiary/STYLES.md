# Bestiary: robots, devices and sensor modules per visual style

Every style designs its own robots from scratch. A style is `bestiary/styles/<Style>.py`:

```python
from lang import Lang
import designs as D

class Style(Lang):          # the shape language: seg, soft, flat, hull_kind, eye_kind, ... and helpers of your own
    name = "S13_Comic"
    ...
    def signature(self, model, P):   # optional: pieces added to every model of this style
        return P

def guard(L): ...           # P = D.Parts(); P.add(role, [bmesh, ...]); P.eye((accent, glow)); P.split = {leg: knee_z}
DESIGNS = {"robot_guard": guard, "robot_hunter": hunter, ...}   # replaces the shared design for this style
```

Build and look: `Tools/Blender/bestiary/review_style.sh <scratch_root> <Style>` → `<root>/<Style>_robots.png`,
`<root>/<Style>_devices.png` (front + 3/4 of every model). The build checks the contract and prints `CONTRACT` errors
and `MESHFIX ... floats` warnings: both must be zero before a style is done. Final export into the game:
`blender -b --factory-startup --python Tools/Blender/build_bestiary.py -- Assets/_Game/Art/Models --styles <Style>`.

## Why per-style designs

Changing only the language (rounder hulls, other lens) leaves every style with the same robots in different paint —
that is exactly what players saw before. The silhouette must change: what the robot *is* in this world (a wind-up
doorman, a welded scrap bandit, a crystal sentinel, a drafting frame). Keep the job (what it senses, how it moves)
and the contract; redesign everything else. Shared `designs.py` are the reference for contract positions, not a
template to repaint.

## Contract (do not break — the game swaps meshes by name into prefabs built from the original model)

Coordinates: metres, Blender x right, **-y = front**, z up. Roles: `Body` (robot body colour), `Accent` (accent colour),
`Eye` (glows in the status colour: only lenses, visors, lamps, seams — small and readable), plus moving parts.

| model | parts | joints / mounts |
| --- | --- | --- |
| robot_guard | Body, Accent, Eye, AccentLeg0/1 (Upper/Lower via `P.split`) | hips (∓0.22, 0, 0.9), knee cut z 0.45, feet on the floor; head ~z 1.84, face y ≈ −0.22 (camera module mounts at y −0.38) |
| robot_hunter | Body, Accent, Eye | static parts only (no walking legs): it glides — hover, wheel, or a pose that reads while sliding; eye (0, −0.3, 1.9); ~2.3 m |
| robot_hound | Body, Eye, AccentLeg0..3 (split) | legs at (+.15,−.35), (−.15,−.35), (+.15,+.35), (−.15,+.35); hip 0.6, knee 0.3; nose module mounts at (0, −1.02, 0.75) |
| robot_spider | Body, Eye, AccentLeg0..7Upper/Lower (no split: build them) | `D.spider_leg(i)` → hip, knee, foot; up = away from the wall |
| robot_listener | Body, Eye, AccentWheel0..3 | wheels r 0.22, width 0.12 at (∓0.43, ∓0.3, 0.22) [0:(−,−) 1:(+,−) 2:(−,+) 3:(+,+)]; microphones mount at (±0.41, 0.1, 1.25) |
| robot_worker | Body, Accent, Eye, AccentWheel0..3 | wheels r 0.12, width 0.1 at 0:(+.31,+.25) 1:(−.31,+.25) 2:(+.31,−.25) 3:(−.31,−.25), z 0.12; carried crate (Accent) around y −0.38..0.08, z 0.95..1.35; eye (0, −0.4, 0.5) |
| robot_sweeper | Body, Accent, Eye, AccentLidar, AccentWheel0..2 | wheels r 0.08, w 0.06 at (−.28,.18), (.28,.18), (0,−.3), z .08; AccentLidar centred on x=y=0, z 0.22 (spins); eye (0, −0.35, 0.5) |
| robot_thumper | Body, Accent, Eye | tracks/feet within x ±0.69, y ±0.7; seismic pads on the floor at (±0.3, −0.85); eye (0, −0.8, 0.3) |
| robot_sorter | Body, Accent, Eye | ~1.4 m; big colour lens around (0, −0.2, 1.25) |
| robot_drone | Body, Accent, Eye, AccentRotor0..3 | rotors (+.42,+.42), (−.42,+.42), (+.42,−.42), (−.42,−.42) at z ≈ 0.065, span ≤ 0.36; body within z ±0.12; camera eye (0, −0.25, −0.1) |
| robot_turret | Body, AccentHead, Eye | AccentHead (turns about x=y=0) within z ~1.15..1.45; **Eye does not turn** → glow on the mount/feet, not on the head |
| robot_vacuum | Body, Accent, Eye | puck r 0.36, z −0.05..0.12; −z faces the room (it rides on the ceiling) |
| sensor_lidar | Body, AccentHead, Eye | AccentHead z 0.39..0.52 turns; Eye still |
| sensor_pir | Body, Eye | wall at +y (y 0..0.08), looks to −y, ±0.08 |
| sensor_plate | Body, Accent, Eye | 1.1 × 1.1 slab, ≤ 2 cm high, flush with the floor |
| security_camera | Body, Accent, Eye | wall plate at y ≈ +0.27..0.3, lens at y ≈ −0.24 |
| laser_emitter | Body, Eye | 1.6 m post, 0.12 wide; emitter strip on the −y face, z 0.15..1.55 |
| attach_eye / attach_mic / attach_seismic / attach_nose | as the shared designs | origin = mount point; keep their size (±0.12, mic 0.39 tall, probe 0.32 tall, nose 0.28 long) |

Parts the original does not have (Accent on the spider/hound/listener/drone) are folded into Body automatically;
moving parts (wheels, rotors, lidar, heads, legs) are never moved by the mesh fixer — connect them yourself (axles,
hubs, hip sockets) with 1–3 cm overlap.

## How it is seen

A top-down-ish game camera, robots ~10–15 m away, lit by a few lamps in the dark. Detail under 3 cm does not exist.
The style lives in **silhouette, proportions and 2–3 big signature forms**; one strong idea per robot beats ten small
ones. Eye glow is how players read a robot's state — keep it visible from above and from the front. Budgets: walkers
≤ 8k triangles, other robots ≤ 5k, devices ≤ 2k, modules ≤ 800.

Material families still apply on top of your meshes (the style's family): Lowpoly — flat colours, use `flat = True` and
low segment counts; Toy — glossy plastic, soft round forms; Lofi — pixel textures on PS1-like low-poly (seg 6–10, hard
edges, optional `grid = 0.01`); Clean — flat chamfered surfaces; Realistic — PBR, more bevels and panel breaks;
Derelict — scuffed PBR.

Our styles are **inspired by** a mood, never copies: no character, logo, iconic prop or signature design of another
game (no identity discs, no turret-with-a-voice, no specific mascots). Invent our own.

## The 16 styles

**S01 NightShift** «Ночная смена» (Lofi). Night-shift industrial contractor horror: utilitarian work machines nobody
maintained — hunched, heavy forearms, caged headlamps, hazard chevrons, exposed pistons and hoses, bolted sheet metal.
Low-poly hard edges.

**S02 Scrapyard** «Барахольщики» (Toy). Little round toys made of scrap: trash cans, paint tins and kettles for bodies,
huge round eyes, stubby limbs, bolted patches, springs and antennae with balls. Cute, bulbous, oversized heads.

**S03 Heist** «Ограбление» (Realistic). Modern private security tech: matte tactical armour, angular chamfered plates,
visor slits, strobes, rails, clean industrial design. Believable proportions.

**S04 Hunted** «Туман и твид» (Realistic). Victorian country-estate robots: tweed-and-brass gentlemen, lanky stilt
legs, bowler domes, monocle lenses, pocket-watch faces, umbrella/cane antennae, hunting-gear vibes. Elegant and eerie.

**S05 Toon** «Мультяшный рейд» (Lowpoly). Chunky cartoon stone golems and rock critters: faceted boulders for bodies,
huge shoulders and fists, tiny heads, glowing rune slits, exaggerated proportions. Flat-shaded facets.

**S06 Storybook** «Книжка-чертёж» (Clean). Art-deco clockwork automata from a picture book: fluted columns, stepped
discs, round face plates with monocles, wind-up keys, finials, brass rims. Tall, symmetric, ornamental.
(`styles/S06_Storybook.py` has a first sketch of the language and a guard — rework freely.)

**S07 Logbook** «Бортовой журнал» (Clean, rendered 1-bit dithered!). 19th-century ship and diving-bell automata:
riveted brass hulls, portholes, diving-helmet heads, lead boots, anchors, ropes, ship ventilators. Only silhouette and
large shading survive the 1-bit filter — bold forms.

**S08 Footage** «Найденная плёнка» (Toy). 2000s consumer electronics come alive: camcorders, flip-out screens, tripods,
tape cassettes, red REC lamps, rounded grey/black plastic with seams and buttons.

**S09 WhiteRoom** «Белая комната» (Lowpoly). Crystal sentinels: angular gem shards, faceted bodies, no visible
machinery, minimal and abstract; a slight glow inside the cracks.

**S10 Grid** «Сеть» (Clean). Sleek dark digital-world machines with glowing seam lines: smooth aerodynamic shells,
light strips running along edges, wrap-around visors, hovering parts. Our own motifs (no identity discs).

**S11 Noir** «Нуар-детектив» (Lofi). 1940s streamline-moderne: chrome teardrops, speed-line fins, fedora-brim discs,
trench-coat skirts, bakelite, venetian-blind slats. Low-poly hard edges.

**S12 TestChamber** «Испытательный комплекс» (Clean). White glossy lab robots: smooth egg/pill shells split by dark
seams over black joints, one big round optic, clean panel gaps, clinical. Our own designs (no copy of famous turrets).

**S13 Comic** «Комикс-пустошь» (Clean with ink outlines). Welded wasteland bandit bots: lopsided boilers, rivets,
spikes, tyres as armour, gas-mask faces, mismatched scrap plates, exaggerated chunky proportions.

**S14 Thermal** «Тепловизор» (Lowpoly). Heavy heat-sink machines seen through a thermal camera: radiator fin stacks,
coolant tanks and hoses, hot cores and vents (Eye), massive blocky silhouettes that read as heat blobs.

**S15 Blueprint** «Синька» (Clean). Drafting automata from technical drawings: open frames of square beams, compass
and ruler parts, circles and triangles, crosshair lenses, dimension-line struts. Drawn with a ruler.

**S16 Derelict** «Заброшенный комплекс» (Derelict, Sasha's style). Decommissioned space-station security synths:
worn armour shells over exposed machinery, cables, cracked panels, one limb broken or replaced, dim lenses.

## Furniture and level pieces (second pass)

`bestiary/props.py` has the shared designs of 39 props and level pieces written against the furniture vocabulary in
`bestiary/furniture.py` (mixed into `Lang`): `slab`, `carcass`, `door`, `handle`, `leg`/`four_legs`, `cushion`,
`screen`, `shade`, `pot`, `foliage`, with kinds `leg_kind`, `edge_kind`, `cushion_puff`, `handle_kind`,
`foliage_kind`. Build them with `--set props` (or `--set all`); review: `review_style.sh <root> <Style> "" props`
renders `<root>/<Style>_props.png`.

For furniture the language does most of the work — a sofa stays a sofa; the style shows in profiles, legs, edges,
upholstery, handles and ornament. So: set the furniture kinds and override the vocabulary methods in your `Style`
(e.g. Storybook's `leg` is a turned baluster, Blueprint's `slab` is an open frame with a thin panel, Toon's `cushion`
is a boulder), then replace 6–10 props where the world has its own take (the arcade machine, the vending machine, the
safe, the plant, lamps, the server rack, the reception desk are good candidates) via `DESIGNS["<prop>"] = fn`.

Contract for props: the original's roles (`Body`, `Accent`, `Eye`, the terminal's `AccentConsole`); the same footprint
and height ±3 cm (the build fails above 6 cm: the colliders and room layouts are the original's); standing on the
floor; the lamps keep the bulb (Eye) where the original's is (desk lamp ~(0, −0.16, 0.27), floor lamp ~(0, 0, 1.36–1.5),
ceiling lamp hangs from z 0 down to −0.08); the exit sign sticks out of both faces of a 0.2 m wall (y ±0.15).
The window keeps the shared model. S03_Heist and S04_Hunted: the realistic family swaps a dozen props for CC0 scans
(`AssetBuilder.Families`); style the rest. S16_Derelict keeps Sasha's own props (`derelict_props.py`) — skip props there.

## Состояние на 11.11.2026 и что осталось доделать

В игре (`Art/Models/Styles/PerStyle/<стиль>/`, тесты 762/762, Windows-билд собран, сценарий `styles` снят):
- все 16 стилей — свои 12 роботов, датчики и модули;
- мебель и детали уровня — у 14 стилей (S01, S02, S03, S05, S08, S09, S10, S12, S13, S14, S15 — полностью свои конструкции;
  S04, S06, S07 — только то, что агент успел до остановки: язык мебели и часть своих предметов; S11 — без plant, bookshelf, coat_rack);
  S16 — мебель Саши (`derelict_props.py`), не трогали.

Осталось:
1. **Мебель S04 Hunted, S06 Storybook, S07 Logbook, S11 Noir** — второй проход «своя конструкция в крупных формах»
   (сравнить `review_style.sh <scratch> <стиль> "" props` с нейтральным рядом). У S11 три предмета вне габаритов:
   plant (низкий и узкий), bookshelf (шире на 13 см), coat_rack (крюк торчит на 10 см); у S11 lockers и S04 hound meshfix притягивает куски.
2. **Accent у пса, паука, слушателя и дрона.** Базовым моделям добавлена часть `Accent` (чтобы акцентный цвет не пропадал), но
   стили строили их без неё — сейчас вместо неё скрытая точка (`build_bestiary.fillers`, строки `FILLER` в выводе). Отдать
   акцентным деталям этих роботов роль `Accent` в файлах стилей (21 случай).
3. Мебель, которая осталась близкой к нейтральной: шкафы/стеллажи в S10 Grid и S12 TestChamber, офисный стул, вешалка и умывальник в
   S01/S14, шкафы в S13 Comic и S08 Footage, доска в S05 Toon.
4. Проверить в игре: тонкие рамы S15 Blueprint в темноте с 15 м, гусеницы штамповщика S16, плоские модели S11/S14 (бюджет позволяет больше граней).
5. Независимый критик по всем рядам (`review.py --lineup`), потом точечные правки.
6. Архитектура стилей (`StyleKit`: плинтусы, карнизы, панели) пока общая на семейство — свой набор на стиль требует правки
   `StyleConfig.KitFor` (по стилю, а не по семейству).
7. Мелочи общего кода: общий `revolve` (кольцо из замкнутого профиля) в `shapes.py` вместо копий в стилях.
