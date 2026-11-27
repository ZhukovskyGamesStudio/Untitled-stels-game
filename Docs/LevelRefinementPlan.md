# Level refinement plan

Agreed 2026-11-25. Steps 0 and 1 are built (section 10), the rest is not. Follows [GenerationRoadmap.md](GenerationRoadmap.md)
(its steps reach only the office houses). Numbers marked † are starting values for tuning, not measurements.

## 1. Where we are (LevelAudit, 360 houses, two players)

| | Apartments | Garage | Hospital | Station | Offices |
|---|---|---|---|---|---|
| Share of houses | 25 % | 25 % | 25 % | 9 % | 16 % |
| Building outlines | 1 | 1 | 1 | the hospital's | 8 |
| Two houses alike (same type, same place) | 64 % | 47 % | 85 % | 85 % | 6–10 % |
| Dead-end rooms, robots | 60 % | 48 % | 85 % | 84 % | 37–57 % |
| Loops per 10 rooms, robots | 0 | 0.1 | 0.1 | 0.05 | 0.1–0.3 |
| Rooms entry → exit | 4 | 3 | 3 | 3 | 7–9 |
| Longest corridor, m | 17 | 28 | 57 | 57 | 37–48 |
| Rooms / floor area, m² | 67 / 1350 | 22 / 1140 | 65 / 2400 | 60 / 2400 | 86–127 / 5100–5600 |

Ceiling ducts are not counted as ways here.

## 2. Goals

1. **Big facilities from the first house.** Size comes from the kind of building and the seed, never from the house number.
2. **Long routes.** The exit is far from the entry on foot, whatever way is taken.
3. **Real buildings.** Every layout is one an architect would draw for that kind; what is strange is what happened to it.
4. **Zones of hazard.** The house is made of parts with their own robots, traps, loot and things worth finding.
5. **No two houses alike**, within a kind as well.

## 3. Size

- Gone: `BaseRooms`, `RoomsPerHouse`, `MaxRooms`, `FloorsPerHouse` by house number, `HospitalSize`.
- New: per kind a range of floor area and of floors, drawn from the house's own stream.

| Kind | Area, all floors † | Floors † | Now (average) |
|---|---|---|---|
| Hospital | 5000–9000 m² | 2–4 | 2400 |
| Station | 4500–8000 m² | 2–3 | 2400 |
| Apartments | 3500–6000 m² | 5–9, or 4–6 in two sections | 1350 |
| Garage (a depot: service halls, body shop, parts store, offices) | 3500–6000 m² | 1–3 | 1140 |
| Offices (all five) | 5000–9000 m² | 2–4 | 5100–5600 |

- The largest offices today are 9300 m², so the upper end is a size the game has already built. The frame rate, the
  navmesh baking time and the generation time at that size must be measured before the ranges are final.
- **The team's size leaves the building alone and changes the difficulty**: how many hazard zones, how dense the loot lies, how many
  medium and hard zones there are against low ones (the last is for playtests and may change). - **Some houses come only late in a run**: the abandoned station (dark) from the fourth house.

What the size used to carry, and where it goes:

| Today by house number | Then |
|---|---|
| Building size, floors | kind and seed only |
| Number of robots, devices, cameras, lasers | per zone, by its area and hazard |
| Number of items | per zone, by its area and hazard |
| Which robots and traps exist (`MinLevel`) | stays, inside each zone's roster |
| Difficulty of the run | the share of medium and hard zones grows with the house number |
| Quota | fixed numbers, growing with every house (section 5) |

**Two rules that break in a big house and must change with it:**
- **Reinforcements** come after 3 min, then 2, then every minute for ever. At 15–25 minutes a house that is 12–22 extra robots.
  New: the clock is scaled by the floor area, newcomers come into medium and hard zones only, and there is a cap.
- **An alarm wakes every docked guard at any distance.** New: it wakes its own zone's guards (section 6).

## 4. Layouts and routes

### The problem
Every kind today is, at heart, **one long corridor with rooms on both sides**: the hospital and the station a 3 m spine down a slab,
the apartment block a landing, the offices a spine per wing. The user wants overall shapes that are more interesting, corridors that
turn and cross, single rooms and clusters of rooms, and buildings that still read as planned by an architect.

### Blocks first, corridors between them
Prototyped in `Tools/Prototypes/layouts.py` (Python, pictures: `python Tools/Prototypes/layouts.py Logs/layouts`). In this order:

1. **Mass.** A building is a set of wings that overlap, step and turn: figure of eight (two courtyards), comb, deep block with a
   lightwell, pinwheel, campus (a core with four wings round it), cross, staggered bars, Z. Wings are 18–25 m deep, deep blocks 40 m.
   Upper floors may step back, but keep the lower floor's cores and its main corridor (structure and stairs stack).
2. **Blocks.** The mass is cut into blocks of 8–15 m (garage halls up to 24) by a staggered partition: each piece is cut on its
   own, so the cut lines do not run through. The building is first "rooms", not "corridor".
3. **Corridors along the block edges.** The network starts at the entrance and grows the cheapest way to the next block nobody
   reaches yet: along an edge between two blocks costs least, through a block costs more (it splits it), near the facade much more
   (corridors run inside, rooms keep the windows), a turn costs a little, a straight past 15 m costs a lot. It grows until every
   block is met along 3 m. The result is a tree with turns, T-junctions and crossings.
4. **Loops.** Then 1–4 shortcuts per floor: a new way between two points that are far apart along the network and close across it,
   kept a block's width away from the corridors it passes (a ring round a courtyard or a lightwell comes from this by itself).
5. **Corridor widths and places.** The main way (entrance to the farthest point) 3 m, the rest 2 m; at some crossings a hall 5–7 m
   (waiting area, nurse station, lift lobby). A corridor never runs along the facade.
6. **What a block becomes**, by its size, its depth and which sides touch a corridor:

| Treatment | When | What it is |
|---|---|---|
| Single room | any size; most often large blocks | a lab, a hall, a ward bay, a store, a workshop |
| Row | a block up to 8 m deep | rooms side by side, each with its door on the corridor |
| Back to back | corridors on two opposite sides | two rows, each opening onto its own corridor |
| Cluster | from 100 m², a corridor on one side | a private hall from the corridor into the block, rooms off it, a room at its end: a department, a suite, a nursing pod, a flat |
| Pinwheel | from 11 × 11 m | four rooms round a small hall, one of them the anteroom from the corridor: a director's suite, a doctors' unit |
| Open plan with pods | from 120 m² | an open office or day room with 1–3 closed rooms in its corners |

   The kind of building weighs them (hospital: clusters, rows, back to back; office: open plan and clusters; station: single rooms
   and pinwheels; garage: halls). The room types are then given to the rooms by today's zoning (windows, depth, neighbours).
7. **Doors.** Clusters open only through their hall or anteroom; everything else onto the corridor; a room with no corridor reaches
   it through a neighbour. A few second doors: a cluster's back room onto another corridor, a room through to the next.

**Prototype, 40 floors of each kind** (one floor; the house has 2–4):

| | Hospital | Station | Office | Garage |
|---|---|---|---|---|
| Floor, m² | 2300 | 2100 | 2300 | 2100 |
| Corridor share of the floor | 20 % | 19 % | 21 % | 14 % |
| Longest straight sight along a corridor, m (avg / max) | 37 / 59 | 32 / 48 | 33 / 55 | 28 / 55 |
| Corridor loops per floor | 1.6 | 1.1 | 1.9 | 0.5 |
| Clusters / single rooms per floor | 2.6 / 18 | 2.6 / 13 | 1.7 / 17 | 1.4 / 8 |
| Walk from the entrance to the farthest corridor end, m | 75 (52–125) | 69 | 74 | 66 |
| Rooms without a way in | 0 | 0 | 0 | 0 |

For comparison today: one corridor of 57 m in the hospital and the station, 0.1 robot loops per 10 rooms, 85 % of the hospital the
same in two seeds. Real buildings have 15–25 % corridors. Still to do in the real generator: straighten the corridors' 1 m steps
(a turn only after 4 m), break straights over 25 m (a hall, a bend, fire doors), more loops for robots on the main floors.

### Per kind
(As first planned. What was built — every kind its own scheme, not one generator with other numbers — is section 11.)

| Kind | Masses | Blocks become | Own rules |
|---|---|---|---|
| Hospital | figure of eight, comb, deep, pinwheel, campus, cross, stagger | wards as back-to-back rows and nursing clusters, imaging and theatres as single rooms in a cluster, outpatient pinwheels | theatres and imaging together on one floor; the nurse station is a hall at a crossing of the ward floor |
| Station | pinwheel, campus, Z, stagger, cross, deep | labs as single rooms, quarters as clusters, control as a pinwheel | its own masses: no more the hospital's slab |
| Office | deep, figure of eight, stagger, cross, comb, Z, campus | open plan with pods, clusters (departments), rows of offices | today's L/T/U/H and linked blocks stay as masses |
| Garage | stagger, Z, campus, deep, comb | halls (workshop, paint, parts) as single rooms, offices and staff rooms as clusters | the workshop on the street side; doors wide enough for a car |
| Apartments | sections in a row, an L or a U, each with its stair | every flat a cluster (its hall is the private hall) off a landing; a corridor type with flats on both sides on some floors | storeys differ (knocked together, burnt out, taken over); sections join through the attic, the basement store and broken walls |

### Entry and exit
- **Distance is walked, not as the crow flies.** Today the exit is the farthest ground-floor room in a straight line, and a
  wet room at 60 % of it is preferred: that is why it is three rooms away.
- The exit stands at 70 % † or more of the longest walk from the entry, and at least 120 m † of walking (about half a minute at a plain walk of 3.6 m/s, several minutes sneaking).
- It stays on the ground floor (its shaft meets the sewer), so in a tall house the way may be made to go **up, across and
  down**: the ground floor is split (a collapsed corridor, a locked department, a flooded stretch) and joined upstairs.
- **Two ways to it at least**, sharing no room but the two ends' halls, one of them not for robots (ducts, ruins, squeezes).
- The exit is never in the entry's wing and never in a hard zone.
- The vent network keeps its promise (arrival to exit), but with screwed grates on the way: a shortcut that has a price.

## 5. Hazard zones

A zone is a connected part of the house, one wing or several; small wings may share one. Every room has a zone, and corridors
belong to the zone they serve.

| | Low | Medium | Hard |
|---|---|---|---|
| Feels like | a building still being looked after | a building that guards itself | a part the robots have made theirs |
| Robots | **utility only**: workers, cleaners, repairers | patrols: listener, drone, sorter, thumper; docked guards | more of them, turrets, spiders, the strongest guards |
| Traps | none | pressure plates, motion sensors, some lasers, cameras | dense: lasers that move, plates, lidars, cameras; **other hazards later** |
| When a player is seen | it raises the alarm and goes on with its work | chase | chase by all of the zone, locked doors |
| Doors | open, few locked | some locked, robot locks | robot locks, key doors |
| Light | mostly on | mixed | mostly dark, the robots' own glow |
| Loot | common, plenty | better of two draws, some rare | better of three draws, at least one rare each zone, crates |
| Terminals | one map terminal always | hack, camera and map terminals | the best ones; consoles of the vault |
| Objective, point of interest | rarely (10 %†) | sometimes (30 %†) | usually (60 %†) |
| Share of the house's area | 45 %† first house → 25 %† late | 40 %† | 15 %† → 35 %† |

### Where they lie
- The entry is in a low zone, always. The exit is in a low or a medium one.
- **A hard zone is never on the only way from the entry to the exit.** It hangs off the route: a choice, with a reward.
- Order by walk from the entry: low, then medium, then hard; a second low zone may lie at the far end (a place to breathe).
- A hard zone has two ways in at least: its gate and a hidden one (duct, ruin, hidden room, squeeze).
- Zones follow what the building is:

| Kind | Low | Medium | Hard |
|---|---|---|---|
| Hospital | reception, outpatient, canteen | wards, emergency, laboratories | theatres, imaging, pharmacy, morgue |
| Station | quarters, canteen, stores | laboratories, workshops | containment, servers, reactor hall |
| Apartments | entrance, lower floors, laundry | middle floors | the floors the robots rebuilt, attic |
| Garage | reception, showroom, wash | service halls, parts store | body shop, scrap press, armoured store |
| Offices | lobby, open plan, canteen | departments, meeting floor | executive floor, server hall, vault |

### The charging room and existing pieces
The charging room goes to a medium or hard zone (one per zone with docked guards). Wards (traps on rich rooms), key doors, the
hidden room, the vault and the cage choose by zone first, then by their own rules. The deep and dark bonuses of the loot stay
but become small: the zone is the main reason.

### How a player knows
Without text: a threshold at every gate (a floor stripe, an airlock frame, a changed door), the robots' growths thickening
(cables, conduit growths, lamps that look), light and sound. On the map terminal the zones are tinted. The exact colours and
the pictures are to be agreed.

### Quota
Fixed numbers, growing with every house (`RunSettings.Quotas`: 240, 320, 420, 540, 680, then +160), and by half for every player
after the first (`QuotaPerExtraPlayer`; a friend joining mid-house raises it), never counted from what lies in the house. The house only guarantees loot worth at least twice its quota. Where the loot lies is the zones' business: when they exist, the
guarantee should hold without the hard zones, so that a hard zone stays a choice. The bag's room must be checked against the numbers.

## 6. Robots by zone

- **A leash.** A robot patrols its own zone. In a chase it may leave by 15 m † and then turns back; a docked guard may cross
  into the next zone, never from hard into low.
- **Utility robots never hurt a player.** Today the cleaner (sweeper) knocks players down, so it changes: it only reports. The
  worker already works that way. A ceiling vacuum trips an alarm on touch, so it counts as a trap and lives in medium zones.
- **Their alarm is a local one.** It brings the nearest medium robots to look (or, with none within reach, one docked guard) and
  the utility robots hide or stand still; it wakes nobody in a hard zone. A medium alarm wakes its own zone's guards; a
  hard one wakes the zone's guards and locks its gates for a while.
- **The repairer** (new, the only new robot here): goes round its zone putting right what players changed. It wakes hacked
  robots and devices, screws grates back, switches lights on, closes doors that were forced. It minds its own business, reports
  what it sees, and its round can be read and used. Built by the new-robot checklist in `stealth-code`.
- Hard-zone hazards that are not robots (gas, electricity, flooding, heat, crushing doors…) get **an empty slot** now: a list
  per zone with a kind, a place and a size. None is made in this plan.

## 7. Architecture of the code

- `HazardZones` (pure, own stream): wings → zones, their hazard, gates, and checks (entry low, exit not hard, a hard zone never
  the only way, two ways in). Result in `LevelLayout.Zones`, and `PlanRoom.Zone`.
- `ZoneRules` (pure, `Data/Config/Level` → Zones): roster, traps, loot, terminals, light and doors of each hazard.
- `PlaceSecurity`, `Wards`, `PlaceTerminals`, `LootPlanner`, `HouseFeatures` ask the zone. A level without zones (the rooms test) keeps
  working: one low zone.
- `WalkDistance` (pure): metres on the plan grid, used by the exit, the zones and the key doors.
- `HouseRules` per kind draws a wing arrangement; `PlanGenerator.*` fills it.
- The classic generator and the old enemies are deleted (step 0).

## 8. Order

| # | Step | Done when (LevelAudit, per kind) |
|---|---|---|
| 0 | **Done.** The classic generator and the old enemies deleted | tests |
| 1 | **Done** but the frame time. Sizes by kind; late houses; fixed quota and its guarantee; reinforcement clock; guards and loot by area and team | sizes in range; generation and navmesh baking measured at the top of the range; frame time: in the game |
| 2 | Exit by walking distance, two ways, corridor sight rule | entry → exit ≥ 120 m and ≥ 70 % of the longest walk in every house; two ways in 100 % |
| 3 | `HazardZones` on today's plans: robots, traps, loot, terminals, objective, alarms, leash, cleaner | zone checks in 100 % of houses; utility robots only in low |
| 4 | The repairer | its own tests |
| 5 | Blocks-first layouts (section 4) in C#, **done**: every kind of house a scheme of its own (section 11); the garage and the apartment block keep the plans they have | two houses alike ≤ 25 %; corridor share 15–25 %; loops ≥ 1 a floor; no sight run > 25 m; LevelAudit and plan pictures |
| 6 | How zones are seen: thresholds, growths, light, map tint | pictures agreed |
| 7 | Landmarks: one memorable place per house; pillars, niches, level changes | ≥ 1 per house; plain rectangles ≤ 70 % |
| 8 | Furniture: bare apartments, odd rooms | rooms under 3 pieces ≤ 5 % |

Steps 2–3 work on today's plans and are playable on their own; step 5 is the largest and goes kind by kind. Step 5 may come
before 2: the exit and the routes are easier to place on the new layouts, and the zones follow their clusters and wings.
Every step: tests, a full build, the report before and after. Playing it is the user's check.

## 9. Decided 2026-11-25

1. The team's size modifies the difficulty; the quota grows by half for every player after the first (2026-11-26) (number of hazard zones, density of loot, share of medium and hard zones), not the building.
2. Later houses are harder by their zones; some houses come only late (the abandoned station).
3. The quota is fixed numbers growing with every stage; a house guarantees its quota twice over.
4. The classic generator and the old enemies are deleted.

## 10. Built

**Step 0.** The classic generator; the old enemy mode; the hound, hunter and warden with their models, materials, icons, prefabs and
texts; scent; the hunter's timer. `RobotKind` keeps its numbers (7, 10 and 16 are empty).

**Step 1.** `BuildingSettings.Sizes`, `SecuritySettings …Per1000`, `LootSettings.ByPlayers`, `LootPlanner.Enrich`, `RunSettings.Quotas /
LootPerQuota / LateHouses / Reinforce…`, `ReinforcementClock`. 360 houses, two players, before → after:

| | Apartments | Garage | Hospital | Station | Offices |
|---|---|---|---|---|---|
| Floor area, m² | 1350 → 1630 | 1140 → 1770 | 2400 → 5520 | 2400 → 5450 | 5100–5600 → 6500–7750 |
| Rooms | 67 → 84 | 22 → 30 | 65 → 158 | 60 → 140 | 86–127 → 109–171 |
| Loot in the first house (quota 240) | 360 → 510 | 375 → 550 | 415 → 960 | 415 → 950 | 805–830 → 1615–1895 |
| Robots, first / fifth house | 5 / 11 → 3.5 / 4 | 4 / 8 → 4 / 5 | 3 / 8.5 → 6 / 10 | 4 / 9 → 6 / 9.5 | 4 / 9 → 7–8 / 12–13 |
| Biggest house: generate / build / navmesh, s | 0.3 / 4.5 / 0.2 | 0.3 / 0.6 / 0.1 | 1.3 / 1.8 / 0.5 | 1.1 / 1.9 / 0.4 | 1.6–3.8 / 1.2–1.8 / 0.3–0.7 |

Every one of the 360 houses holds its quota twice. The apartment block and the garage stay below their ranges until they get new
layouts (step 5); the hospital and the station are longer slabs meanwhile (corridors of 84–100 m: step 2 and 5).

**Step 5, first part (2026-11-26): the hospital and the station on blocks** (`PlanGenerator.Blocks.cs`; the slab code is gone).
Masses: hospital figure of eight, comb, deep, pinwheel, campus, cross, stagger; station pinwheel, campus, Z, stagger, cross, deep.
Corridors 2 m, the main way 3 m, on whole metres; halls at some crossings. Clusters open only through their hall (`PlanRoom.Cluster`,
`ClusterDoor`, `ClusterAllows` in the doors' pass). Types by the kind (`TypeHospital`, `TypeStation`): one-door rooms (patient rooms,
theatres, imaging, the nurse station) only where they share 4 m of wall with a corridor, and none that would shut a room in
(`KeepReachable`); theatres and imaging only in plain rectangles wide enough for their machines; scraps under 14 m² join a neighbour.
The partition and the corridors stack through the floors; what a block becomes is drawn per floor. 40 houses each, two players:

| | Hospital before → after | Station before → after |
|---|---|---|
| Two houses alike (same type, same place) | 85 % → 8 % | 85 % → 17 % |
| Dead-end rooms, robots | 85 % → 52 % | 84 % → 56 % |
| Loops per 10 rooms, robots | 0.13 → 0.26 | 0.05 → 0.31 |
| Rooms | 65 → 127 | 60 → 111 |
| Median room, m² | 29 → 43 | 35 → 46 |
| Biggest house: generate / build / navmesh, s | — → 1.9 / 1.1 / 0.7 | — → 2.0 / 1.4 / 0.6 |

("before" is before step 1, when the houses were half the size.) Known weak points: a quarter of the one-door rooms end up with a
second door or a door off the corridor (where the corridor's wall had no room for a door); about half of the patient rooms get
their beds and headwalls (the slab's 4 m rooms always did); the station's rooms are big (labs).

**After the hospital play (2026-11-26):** "a bit chaotic, but not so repetitive". Calmer: one room width per building
(`BlockPlan.Module`, 4–5 m; a double room now and then, more on the hospital's top floor for its theatres), back-to-back
rows split down the middle, fewer pinwheel and cluster blocks in the hospital, whole-wall open passages only between rooms
that are open by nature. The hospital's furniture: no arcade machines, egg chairs, robot arms or 3D printers (`Furnisher.Belongs`);
its lab gets a fridge, a medicine cabinet and a sink. A theatre may be left out where its one door would shut a room in
(two in at least 70 % of hospitals). Hospital, 40 houses: alike 8 % → 9 %, robot loops 0.26 → 0.28 per 10 rooms, median room 43 m².

**Upper floors of their own (2026-11-26):** the user saw one floor four times. Now only the stair halls stack: every upper
floor cuts its own blocks and grows its own corridors from the stair hall it arrives in (`BlockPlan.Floors`, `PlanFloor`),
and has a character — mostly rows, mostly clusters, or mixed (`Character`, each once before any repeats). The nurse station
waits for a corridor (its open wall) and prefers rooms 5 m deep (the counter and the chairs behind it); a patient room may be
up to 60 m² (four beds). Hospital, 40 houses: same type one floor below 58 % → 27 %, rooms 128 → 116, robot loops 0.28 → 0.22
per 10 rooms, dead ends 50 % → 57 %; station 50 % → 31 %.

**Step 5, the offices (2026-11-27).** *(The first version, replaced the same day: all five kinds shared one layout, see
section 11. The masses and the weights named here are gone; the room typing by the program and the atriums stayed.)* The five office kinds are laid out blocks first (`BuildingSettings.BlocksChance` = 1; a
linked house stays what it was, the test house of every room type keeps the wings). Masses: deep, figure of eight, stagger,
cross, comb, Z, campus, and in three houses of ten today's bar, L, T, U, court, H (`OfficeMass`). What a block becomes follows
the kind's room weights (`BlockPlan.Weights`): much open space — open plans with pods; stores, servers, labs — single rooms up
to `BlockPlan.Hall` (90 m² × the kind's room scale squared); offices and meeting rooms — rows, back to back, departments.
Types follow the room program (`TypeOffice`): every room of the program takes the room that fits its type's rule and is
nearest its size — an open plan for open space, a plain rectangle for a control room, a kitchen or a meeting room, the
toilets by the stairs, private rooms away from the lobby; the rest by the kind's weights as before. Atriums are back (no
corridor of the floor above may run over one); rotundas, octagons and cut corners come with `Commit` as before. 40 houses of
each kind, two players, before → after (the linked houses in both):

| | Office | Business | Data centre | Warehouse | Headquarters |
|---|---|---|---|---|---|
| Longest corridor, m | 49 → 38 | 42 → 37 | 51 → 41 | 54 → 62 | 46 → 38 |
| Loops per 10 rooms, robots | 0.14 → 0.20 | 0.12 → 0.15 | 0.24 → 0.28 | 0.38 → 0.38 | 0.08 → 0.17 |
| Dead-end rooms, players | 22 % → 17 % | 19 % → 16 % | 22 % → 15 % | 7 % → 12 % | 28 % → 16 % |
| Dead-end rooms, robots | 54 % → 47 % | 43 % → 49 % | 66 % → 52 % | 38 % → 63 % | 57 % → 47 % |
| Same type one floor below | 44 % → 41 % | 47 % → 41 % | 49 % → 29 % | 31 % → 33 % | 33 % → 31 % |
| Rooms with under 3 pieces | 4 % → 3 % | 3 % → 1 % | 2 % → 2 % | 4 % → 2 % | 5 % → 2 % |
| Rooms entry → exit, players | 8.1 → 5.9 | 7.3 → 6.1 | 6.6 → 6.9 | 11.9 → 5.5 | 7.1 → 8.4 |
| Biggest house: generate, s | 3.9 → 4.0 | 1.6 → 4.6 | 2.0 → 2.7 | 2.2 → 2.1 | 1.7 → 8.1 |

Weak points: the warehouse lost on three counts (a longer straight corridor, more robot dead ends, a shorter way to the
exit); the way from the entry to the exit got shorter in three kinds of five (step 2 places the exit by walking distance);
the biggest headquarters takes 8 s to generate against 1.7 (not looked into).

## 11. Every kind of house its own layout (2026-11-27)

The rule (the user's): **the layouts of different kinds of house are different layouts, not versions of one**. After step 5
the hospital, the station and the five offices all came out of one generator — the same staggered blocks, the same corridors
grown between them — with other weights; on a plan they read as one building in different dress. Now a house of blocks has a
*scheme* (`PlanGenerator.Blocks.cs`, `Scheme`; an office's from `BuildingSettings.Layout`, which `HouseKinds.Apply` sets), and
a scheme has its own masses, its own way of cutting the floor and its own corridors:

| Kind | Scheme | Masses | Corridors | Rooms |
|---|---|---|---|---|
| Hospital | streets | figure of eight, comb, deep, pinwheel, campus, cross, stagger | wander between staggered blocks, cross, loop (4 shortcuts) | rows of wards, departments off a private hall |
| Abandoned station | hub | spokes (a hub, an arm out of each side), dumbbell (two hubs and a bridge) | a hall in the middle, a straight spoke down every arm, one shortcut at most | lab halls, pinwheels, quarters |
| Office | racetrack | slab, ell, zed, horseshoe — wings 24–30 m deep | a ring 6 m in from the facade, all the way round a core | cellular rooms outside the ring, service rooms and open floors in the core |
| Office (three in ten) | linked | pavilions joined by corridors (`PlanGenerator.Linked`) | one corridor between two pavilions | a pavilion is a handful of rooms |
| Business centre | court | court, courts — one deep block | a gallery round an atrium that rises three storeys; the lobby straight in front of it | suites: a reception and its rooms, open floors |
| Data centre | grid | box, boxes | an aisle between every two halls | halls as islands, whole or in two; small rooms by the lobby and the stairs |
| Warehouse | sheds | shed, sheds | one corridor behind the front of house, nothing else | a strip of rooms along the front, behind it halls up to 280 m² that open into each other |
| Headquarters | axis | tee, pavilion, trident, aitch — mirrored about the entrance | a gallery straight on from the lobby, another across it behind the lobby; the rest keeps straight | the block plan mirrored about the axis: rows of offices, departments |
| Repair shop | garage | hall, back strip, side block (`PlanGenerator.Garage`) | a gallery over the hall | the workshop hall with its bays, the booth |
| Apartment block | apartments | a slab with one stair (`PlanGenerator.Apartments`) | a landing | flats |

The garage and the apartment block are **not** moved onto blocks (that was the plan of step 5): they would have become two
more versions of the same thing. Their own plans get refined instead. The linked pavilions are the plain office's second plan
only (`LinkedChance` is 0 for the other four office kinds).

How it is built: the scheme's own parts are laid first (`Lay`) — a racetrack's core (`CoreOf`: what lies 9 m and more from the
facade, 4 m thick at least), the courts (blocks no corridor crosses, on every floor), the front of house and the corridor
behind the lobby (`FrontLine`), the mirrored halves (`Mirror`) — then the rest is cut into blocks as before. The corridors:
`Network.Ring` (the outline of the core or of a court), `Line` (a straight gallery), `AllEdges` (the grid), `Hub`, then `Join`
(every part gets the cheapest way to the start) and, where the scheme wants it, the old growing and shortcuts for what is left.
In a plan of islands a room with the corridor on two sides gets a door on each (`SecondDoorChance`).

What tells them apart, 40 houses of each kind, two players, the game's settings (LevelAudit, `LayoutSignature`):

| | Hospital | Station | Office | Business | Data centre | Warehouse | Headquarters | Garage | Apartments |
|---|---|---|---|---|---|---|---|---|---|
| Laid out | streets | hub | racetrack 35, linked 5 | court | grid | sheds | axis | garage | apartments |
| Corridor, share of the floor | 19 % | 18 % | 23 % | 21 % | 28 % | 9 % | 25 % | 10 % | 19 % |
| Islands a floor (what a corridor goes all round) | 0.3 | 0.0 | 2.9 | 2.2 | 4.1 | 0 | 0.6 | 0 | 0 |
| Biggest room, m² | 110 | 156 | 106 | 165 | 138 | 258 | 109 | 674 | 27 |
| Mirrored about the entrance | 0.60 | 0.58 | 0.51 | 0.73 | 0.57 | 0.46 | 1.00 | — | — |
| Widest place in the corridors, m | 6.8 | 8.6 | 5.7 | 4.8 | 4.5 | 4.6 | 7.4 | 4.0 | 3.0 |
| Longest straight corridor, m | 36 | 42 | 40 | 46 | 54 | 41 | 67 | 51 | 16 |
| Dead-end rooms, players | 34 % | 10 % | 23 % | 19 % | 60 % | 7 % | 29 % | 18 % | 58 % |
| Loops per 10 rooms, robots | 0.25 | 0.25 | 0.21 | 0.17 | 0.22 | 0.53 | 0.26 | 0.00 | 0.00 |
| Rooms entry → exit, players | 4.4 | 5.0 | 4.8 | 4.4 | 4.1 | 6.0 | 4.3 | 3.0 | 3.4 |
| Rooms with under 3 pieces | 1 % | 1 % | 3 % | 3 % | 2 % | 2 % | 4 % | 6 % | 29 % |
| Plans drawn before one held | 1.5 | 2.0 | 1.25 | 1.0 | 1.6 | 1.5 | 1.25 | — | — |
| Biggest house: generate, s | 4.3 | 4.4 | 5.2 | 5.7 | 2.9 | 4.0 | 6.1 | 0.4 | 0.4 |

Straight runs are kept down by the scheme itself: the corridor behind a warehouse's front and a headquarters' galleries step
3 m aside every 28 m (`FrontLine`, `StraightRun`, `Jog`), the office's ring steps 2 m in and out by squares of 28 m (`CoreOf`),
a data centre's aisle longer than 28 m loses the stretch between two crossings (`Network.BreakStraights`), a station's spoke
pays for every metre past 26. A room whose only wall on a corridor is too short for a door with its margins gets a door
nearer the corner instead of costing the whole plan (`TryOpen` with a tight margin; not the hospital, whose plans stay as
they were).

Weak points: the headquarters still has straight runs of about 65 m (the gallery across the front grows back together
where the corridors of the wings join it); the data centre's halls count as dead ends in the room graph (60 %: their only
neighbour is the aisle, though most have a door on two sides of it) and its aisles run 54 m; the way from the entry to the
exit is 4–6 rooms in every kind (step 2 places the exit by walking distance); a warehouse's halls are typed as open space
(no room type for a hall of racks yet), so its floors are 69 % alike; a plan takes 3–6 s for the biggest houses of every
block kind, the hospital's included.

Kept by tests: `HouseKindLayoutTests` — every kind has a scheme no other has, no two kinds share a mass, and each scheme's
own mark is in the finished plan (the office's island, the court, the data centre's islands, the warehouse's halls and little
corridor, the headquarters' gallery, the station's hub). A new kind of house fails there until it has a layout of its own.
Pictures of all kinds side by side: `PlanPictures.WriteKindPlans` (PLANKINDS, PLANCOUNT) → Logs/plans.
