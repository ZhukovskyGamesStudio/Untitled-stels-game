# CLAUDE.md — USG (untitled stels game)

Кооперативный стелс-рогалик (прототип кор-лупа). Дизайн-документ: https://claude.ai/artifact/GdaGu25vZ1ezVUmZ8KWFap.
Обзор для людей, управление и таблица роботов — в [README.md](README.md). Этот файл — как устроен код и как с ним работать.

- Unity **6000.6.0f1**, URP, новый Input System (только `Keyboard.current` / `Mouse.current`).
- Сеть: Netcode for GameObjects 2.13 (`NetworkVariable`, `[Rpc(SendTo.*)]`). Транспорт Steam (Steamworks.NET, AppID 480) или UnityTransport UDP 7777.
- Интерфейс: uGUI (legacy `Text`, шрифт `LegacyRuntime.ttf`). IMGUI больше не используется.
- Тексты: **все** строки для игрока — в таблицах `Resources/Strings/*.csv` (`key,ru,en`), в коде только ключи (`Loc.T/F/Plural`, см. «Тексты и языки»).
- Навмеш: com.unity.ai.navigation, запекание в рантайме; зона 3 `Stairs` — по ней ходят только роботы с ногами.

## Ветки

Работа Claude идёт в `george`, своих веток Claude не создаёт; в `main` вливаем только по явной просьбе. Коллега (Саша) называет свои ветки как хочет —
в них не коммитить и не пушить, сливать в `george` только по просьбе.
Как коммитить — скилл [`stealth-commit`](.claude/skills/stealth-commit/SKILL.md).

## Команды

Unity в batchmode не запустится, если проект открыт в редакторе — закрой его или работай через редактор.

```bash
Tools/dev.sh cycle              # ОДИН запуск Unity: недостающие ассеты + EditMode-тесты без медленных + development-билд (только скрипты, если менялся лишь код)
SLOW=1 Tools/dev.sh cycle       # весь набор, с [Category("Slow")] (десятки домов, ~90% времени): перед коммитом и после правок генератора; время тестов — Logs/test_times.txt
NOASSETS=1 NOTESTS=1 Tools/dev.sh cycle  # шаги можно пропускать (NOPLAYER=1 — без билда, FULL=1 — полный билд); FILTER/FORCE/REBUILD как ниже
Tools/dev.sh tests              # компиляция + EditMode-тесты без медленных (SLOW=1 — все; итог и упавшие — в конце вывода); в открытом редакторе — MCP run_tests
Tools/dev.sh assets             # дособрать недостающие ассеты (существующие префабы не трогает)
FORCE=1 Tools/dev.sh assets     # пересобрать ВСЕ ассеты из кода (ручные правки в префабах пропадут)
REBUILD_UI=1 Tools/dev.sh assets  # пересобрать только UI-префабы (HUD, меню, инвентарь, элементы)
REBUILD=RobotGuard,Safe Tools/dev.sh assets  # пересобрать только эти префабы (по имени файла), GUID сохраняются
FILTER=HousePlanTests Tools/dev.sh tests  # только эти тесты
PLANS="7:3,42:5" Tools/dev.sh plans       # картинки планов домов (сид:дом) в Logs/plans/
Tools/dev.sh player             # релизный Windows-билд в Builds/Windows/USG.exe (cycle собирает development)
SEED=8 LEVEL=5 Tools/dev.sh run gallery      # билд с автопилотом: лог [AUTOTEST] + скриншоты в Builds/shots/<сценарий>
SEEDS=7,42,3 Tools/dev.sh run sewer          # несколько сидов сразу, по окну на сид (свой UDP-порт, -port): Builds/shots/<сценарий>_<сид>, Builds/host_<сид>.log
Tools/dev.sh run full 1         # хост + 1 клиент по сети (без звука и за другими окнами; SOUND=1 / FRONT=1 — со звуком / на переднем плане)
# Редактор открыт (batchmode не запустится) — то же через его MCP-мост (порт 6400), MCP-клиент не нужен:
python Tools/unity_mcp.py refresh_unity '{"mode":"force","scope":"all","compile":"request"}'   # компиляция; ошибки — read_console '{"action":"get","types":["error"]}'
Tools/unity_test.sh Stealth.Tests.HousePlanTests Stealth.Tests.SpaceRulesTests                 # тесты по классам / именам, ждёт итог
python Tools/unity_mcp.py execute_menu_item '{"menu_path":"Stealth/Build Windows Player"}'     # билд (и другие пункты меню Stealth)
Tools/shoot.sh tour 5 5 S06_Storybook 7811   # сценарий <имя> <сид> <дом> [стиль] [порт] тихим окном → Builds/shots/<имя>_<сид>_<дом>; python Tools/sheet.py <папка> 3 2 — контактные листы
python Tools/unity_mcp.py execute_menu_item '{"menu_path":"Stealth/Rebuild Listed Prefabs (Temp-rebuild.txt)"}'  # REBUILD в открытом редакторе: имена префабов через запятую — в Temp/rebuild.txt
Tools/unity_test.sh Stealth.Tests.SpaceAudit.WriteReport   # аудит пространства → Logs/space_audit.txt (мебель на окнах, пустые комнаты, повторы, наложения стен и панелей)
```

Логи: `Logs/tests.log`, `Logs/build_assets.log`, `Logs/build_player.log`, `Builds/host.log`, `Builds/client*.log`.
Сценарии автопилота — в `Scripts/Game/DevAutomation*.cs` (`full`, `client`, `coop`, `move`, `ui`, `menu`, `floors`, `rooms`, `themed`, `vent`, `perf`, `gallery`, `hunter`, `lasers`, `radio`, `hack`, `detect`, `darkbox`, `newbots`, `fog`, `styles`, `map`, `sewer`, `legs`, `plate`, `terminals`, `cheats`, `features`, `tour` — все освещённые комнаты дома по кадру, `-kinds Restroom,Meeting` — только эти типы).
Как проверять изменения — скилл [`stealth-verify`](.claude/skills/stealth-verify/SKILL.md).

## Структура `Assets/_Game`

```
Art/Materials/<Environment|Props|Robots|Player|Items|FX>   материалы (создаёт AssetBuilder)
Art/Audio/<Player|Items|UI|Robots|Environment|Music>       WAV из Tools/Audio/generate_sounds.py
Art/Models/<Props|Level|Player|Items|Robots|Sensors>       FBX из Blender (Tools/Blender/*.py)
Art/Models/Styles/<Lowpoly|Toy|Lofi|Clean|Realistic>/…/<имя>__<семейство>.fbx  все модели в пяти семействах стилей (Tools/Blender/style_families.py)
Art/Models/Styles/PerStyle/<стиль>/<имя>__<стиль>.fbx       свои роботы, датчики и мебель каждого стиля, с нуля (Tools/Blender/build_bestiary.py, bestiary/)
Art/Models/Styles/Players/player__<стиль>__<отдел>.fbx      игроки-айтишники четырёх отделов в каждом стиле (Tools/Blender/players.py)
Art/Models/StyleKit[/<Семейство>]/kit_*.fbx                 архитектура стилей: плинтус, карниз, панели, пилястра, арка, балки, трубы… (style_kit.py)
Art/Models/City/                                           модели города: улица, машины, деревья, кит фасадов и крыш (Tools/Blender/street.py)
Art/External/                                              CC0-ассеты Poly Haven (Tools/Art/fetch_cc0.py + Tools/Blender/import_cc0.py): Textures/<id>,
                                                           Models/<id>.fbx + текстуры, Pixel/ (64 px для PS1); список — CREDITS.md
Art/Textures/Finishes/*.png                                бесшовные текстуры отделки пола и стен (Tools/Art/generate_textures.py)
Art/Sprites/                                               текстуры UI (виньетка, дуга)
Data/            LevelArt, ItemVisuals, NetworkPrefabs, SoundBank (ScriptableObject)
Data/Config/     НАСТРОЙКИ: Player, Run, Level, Items, Audio (правятся руками в инспекторе)
Data/Styles/     визуальные стили (VisualStyle) + Styles.asset (список, материалы по видам, варианты мешей) + StyleKit + Kit/*.mat + материал экранного фильтра
Prefabs/StyleKit детали архитектуры стилей по семействам (без коллайдеров и теней) + Finish (квад отделки) + Scanned (CC0-мебель)
Prefabs/Robots/<Префаб>Config.asset, Prefabs/Sensors/<Префаб>Config.asset — настройки каждого робота, устройства и лазера рядом с его префабом
Resources/GameConfig.asset            корень настроек (ссылки на Data/Config/*), его находит любой билд
Prefabs/Level    куски здания: LevelRoot (NavMeshSurface), блоки стен/перекрытий, Stairs/StairsMirrored, AutoDoor, VentFrame, RoomLight, StreetLamp, Hatch
Prefabs/Props    мебель
Prefabs/Robots   роботы + их конфиги · Prefabs/Sensors — датчики, камера, лазер + их конфиги
Prefabs/Vehicles машина (Car, сетевой), кусок груза в кузове · Prefabs/Level/Sewer — набор канализации
Prefabs/Player, Items, Network (Session), FX (RadioRipple), Audio (AudioDirector)
Prefabs/UI       экраны HUD, Inventory, Menu, StashBoard + UI/Elements (кнопка, слайдер, дуга, маркеры…)
Scenes/Game.unity                      единственная сцена: сеть, камера меню, экземпляры UI-префабов и AudioDirector, EventSystem
Scripts/<Audio|Config|Enemies|Game|Items|Level|Player|UI|Vehicles>   код игры (asmdef Stealth.Game — без Netcode и Steam)
Scripts/Net/                          сетевой слой (asmdef Stealth.Net): NGO, Steam, сетевые двойники компонентов
Editor/AssetBuilder*.cs               генератор ассетов (asmdef Stealth.Editor)
Tests/EditMode/                       тесты (asmdef Stealth.Tests.EditMode)
```

## Архитектура

Три слоя игры плюс отдельный сетевой слой:

1. **Чистые правила** — обычные C#-классы и статические функции без `MonoBehaviour`, сети и физики. Здесь живут решения, и здесь же — тесты.
   `MovementMath`, `Stamina`, `HealthState`, `GasExposure`, `FootstepNoise`, `RobotBrain`, `SuspicionMeter`, `SensorMath`, `Perception` (геометрия),
   `LevelRandom`, `Hacking`, `RobotRadio`, `ScentTrail`, `SeismicSense`, `ScanLine`, `LevelGenerator`, `HousePlan`, `PlanGenerator`, `Furnisher`, `HouseKinds`, `StairGeometry`, `RoomLighting`, `LootPlanner`, `RobotRoster`,
   `ReticleRules`, `HintRules`, `BannerQueue`, `GainTracker`, `UiMath`, `ItemCatalog`, `TeamStash`, `AudioRules`, `ExplorationMap`, `MapSketch`, `PingBoard`, `Controls`,
   `RunMap` (маршрут), `RouteVotes`, `SafeLock`, `LegMath`, `StairStyles`, `HouseFeatures`, `Crafting`, `Cargo`, `CarPhysics`, `SewerPlan`, `DoorRules`, `Basement`, `Arrival`,
   `GearRules` (кофе, падения, топливоискатель), `HouseStats` (титулы дома), `PickingRules` (ряд экрана выбора), `HouseClockRules`, `HackSlotRules`.
2. **Компоненты** — тонкие `NetworkBehaviour`/`MonoBehaviour`: связывают правила с вводом, сетью, физикой и сценой.
3. **Презентеры UI и звук** — `GameHUD`, `InventoryUI`, `GameMenu`, `StashBoard` (доска склада над люком, world-space canvas): только
   показывают/двигают/красят элементы, заранее собранные в префабах. `AudioDirector` (префаб `Audio/AudioDirector` в сцене) озвучивает
   реплицированное состояние на каждом пире: музыка (меню / исследование / погоня), эмбиент, сирена, шаги и прыжки всех игроков,
   «?»/«!» детекторов, взломы. Разовые звуки из кода — через фасад `Sfx.At(Sfx.Bank?.Clip, позиция)` / `Sfx.Ui(...)`; без директора — no-op.
4. **Сеть** — сборка `Stealth.Net`, единственное место, где есть Netcode и Steamworks (см. ниже).

### Роботы и датчики

```
Detector (abstract NetworkBehaviour, IHackable)   — вид (RobotKind), точка для HUD, осведомлённость (AwarenessLevel/FocusClient),
 │                                                   взлом (Hacked), лампы статуса (StatusLights), реестр Detector.All
 ├─ RobotController (IRobotBody)                  — робот на навмеше: тело и датчики; решения — в RobotBrain
 └─ SecurityDevice (abstract)                     — шкала подозрения SuspicionMeter, Alert 0/1/2, тревога
     ├─ SecurityCamera, SentryTurret, SpiderBot, CeilingVacuum
     └─ LidarScanner, MotionSensor, PressurePlate
LaserGate — отдельная ловушка (не детектор)
```

Интерфейсы (`Scripts/Enemies/Contracts.cs`) отвязывают логику от движка, в тестах их подменяют фейками (`Tests/EditMode/Fakes.cs`):
- `IIntruder` — игрок глазами охраны (реализует `PlayerController`);
- `IHackable` — что можно взломать (реализует `Detector`);
- `IRobotBody` — что «мозг» робота делает телом (реализует `RobotController`).

На мобильных роботах `SensorRig` показывает насадки-датчики (глаз-камера, щупы в пол, нос; микрофонов-стоек нет — сзади они читались как непонятные диски) по `Senses` из конфига и садит их на тело (только на неподвижные части: не на подъёмник рабочего, колёса, винты и ноги — `SensorRig.Moves`)
текущего стиля лучом по треугольникам корпуса (`MeshRay`; микрофоны на верх, щупы к передку у пола, глаз и нос на лицо) — при каждой смене мешей.
Восприятие плавное: зрение — доля видимого тела (`Perception.Exposure`, 7 лучей), дистанция, место в конусе, присед, неподвижность, свет
(`SensorMath.VisionFactor`); свет на игроке — `Perception.IlluminationOf`: сумма всех `LightSource` (потолочные лампы `RoomLight`, настольные/напольные
лампы, таблички «Выход», автоматы, фонари, луна) по формуле затухания URP, с конусом и тенью-лучом, плюс эмбиент → `SensorMath.LightFactor`; тот же расчёт
кормит индикатор заметности в HUD. Новый источник света, важный для стелса, — `Light` + компонент `LightSource` в префабе;
**свет не должен проходить сквозь стены**: эмбиента нет (`LightMath.NightAmbient` = чёрный, туман внутри тёмный), стены/плиты/крыша отбрасывают тени
с двух сторон, у ламп полные тени (сила 1, малое смещение), а лампы без теней — только маленькие споты, не достающие до соседей (`LightLeakTests`);
сценарий `darkbox` строит закрытую коробку в освещённом лобби и проверяет, что кадр внутри чёрный; у роботов и устройств статусный свет — спот с тенями;
**бюджет теней** — `LightBudget` на LevelRoot рисует только ближайшие к камере лампы, что влезают в атлас теней (иначе URP ужимает карты, и
плеер падал); геймплей от этого не зависит. Материалы: либо освещаются, либо светятся по-настоящему (HDR ≥ 1, bloom) — проверяет `LightLeakTests`; неинтерактивное свечение (экраны мебели, абажуры, лампочки, огоньки стоек, стекло автоматов — `LightLeakTests.DimDecor`) — наоборот, тусклое, < 1; слух — громкость шага у робота с затуханием через стены/двери/перекрытия (`Perception.SoundTransmission`,
компонент `SoundBlocker` на префабах стен, плит и дверей). Полная шкала → `RobotState.Alerted` («Обнаружен нарушитель», `Traits.WarningTime`)
→ `Chase` («Веду преследование»). Голос — `AudioDirector` по смене состояния (`AudioRules.Voice`), шкала над роботом — `GameHUD` (`SuspicionMarker`).
Удар сбивает с ног (`HealthState.TryHit` → `Downed`): лежачих роботы не видят (`IIntruder.IsDown`), робот говорит «Нарушитель устранён»
и возвращается к работе; с HP > 0 игрок встаёт сам (`CanGetUp`, Пробел → `IPlayerLink.SendGetUp`), с 0 — только союзник; провал забега — когда все `KnockedOut`.
Потолочный пылесос (`CeilingVacuum.Sweep`, буфер лучей на 64 попадания — проверяет `VacuumFanTests` на настоящем префабе) — вертикальный лазерный веер от линзы робота вниз: `ScanLine.FanRays` лучей на половину, каждый останавливается на первом,
во что упёрся (стена, мебель, дверь, пол; игроки не заслоняют), меш веера собирается из этих точек каждый кадр, той же формой сервер ловит
игрока (`ScanLine.FanTouches`); линии на полу нет. Касание любой частью тела — тревога, перепрыгнуть нельзя. Паук время от времени переползает через потолок на
противоположную стену (`SpiderProfile.CeilingEvery`; ступни ног находят и крышу верхнего этажа).
Паук без глаз: его комната затянута паутиной, как простой лабиринт (`SpiderWebs`: полотна от пола до потолка от одной длинной стены к другой с просветом у чередующихся концов — не перешагнуть и не подлезть, каждый пир плетёт те же; префаб
`Robots/SpiderStrand` — единичный квадрат сети нитями-геометрией в трёх вариантах `Web0..2`: большая круглая сеть, сеть и клубок в углу, две сети — `Tools/Blender/web.py ... sheet` → `web_sheet.fbx`; растягивается на полотно, вариант выбирает `SpiderBot`; без альфа-текстуры: тонкие нити на
полупрозрачной карточке в дали превращались мип-уровнями в молочную плоскость), коснулся — паук бежит по стенам к тебе и прыгает. Охранник на точке патруля стоит в три фазы, видные издалека
(`RobotBrain.PausePhase`: замер — оглядывается — разворачивается к следующей точке). Сейсмик — волны (`SeismicSense.Wave*`, `PulseStomp` → кольцо `StompRipple` и звук), подметальщик — лидар у пола (`RobotLidar`; створки дверей его останавливают, как стоят — `AutoDoor.Clip`),
рабочие везут лут (`DetectorSnapshot.Cargo`, при взломе падает на пол). `RobotRoster.Newcomers` — новый вид гарантированно есть в доме, где он появляется.
`RobotAnimator`: наклоны в разгоне и повороте, испуг при «Обнаружен», обмякание при взломе, удар сейсмика, глаз-камера следит за замеченным игроком.
Настройки робота — в префабе: `RobotController.Senses` (`RobotSenses`: глаза, слух, сейсмика, нюх, сортировщик) и `Traits` (`RobotTraits`: скорости, лестницы, атака, тревога).
Цвета статусов — `SecurityPalette`; **светится только то, чем робот чувствует** (`SecurityPalette.Glow`, `StatusLights`): глаз-камера — голубым (видит), лампы корпуса — жёлтым (слышит), нос, щупы, лампы подъёмника — никогда; у робота без глаз и слуха (лидар, пылесос, плита, паук, охотник) нет ни свечения, ни статусного света; тревога красит то, что есть (`Detector.GlowEyes/GlowEars/AtEase`). Описание для игрока — `RobotInfo`. Где и когда появляется — `RobotRoster`.
**Ищейка** медленная и останавливается принюхаться (`RobotTraits.SniffEvery/SniffTime`, `RobotBrain.Sniffing`: в патруле, проверке и погоне, но не вплотную к игроку). **Охотник** без своих датчиков (`RobotSenses.FollowsSightings`): «спит» там, где вошёл (`RobotBrain.Asleep`: не ходит, касание, взлом,
шум и тревоги его не будят), пока любой другой детектор не заметит игрока (`RobotController.Relayed`) — тогда бежит к нему, быстрый; потерял — засыпает снова. **Сторож** (`RobotKind.Warden`) не в ростере — его ставит `HouseFeatures.Wardens`.
**Часы дома** (`GameManager.HouseTime`) идут с момента, когда первый игрок поднялся в дом (из подвала или по лестнице выхода, `UpdateHouseEntry`), без пауз — и пока команда в канализации под ним; на них — охотник, пробуждение охраны, подкрепление и льготный период датчиков.
**Подкрепление** (`GameManager.UpdateReinforcements`): через `RunSettings.ReinforceFirst` (3 мин) после начала дома входит ещё один робот, через `ReinforceSecond` (2 мин) — следующий, дальше каждые `ReinforceEvery` (1 мин) — `RobotRoster.ReinforcementsDue`; вид — `RobotRoster.PickReinforcement` (любой подвижный из ростера этого дома, кроме безобидных рабочих, охотника и сторожа; соль `Salt.Reinforcements`), входит у самого дальнего от игроков входа, баннер `banner.reinforcement`. В ранних домах больше патрульных (слушатели, пылесосы, подметальщики, дроны), охранников меньше (ростер `Level.asset` = `RobotRoster.DefaultRobots`).
**Зарядка**: робот на станции стоит вплотную к ней (`RobotController.HoldOnDock`: пока `Resting`, агент не двигает тело, оно подъезжает к точке станции `RobotBrain.DockSpot`; спавн — ровно в точке, без сэмплинга навмеша), станция `ChargingDock` без робота горит тускло и ровно, а с роботом на своей точке (`ChargingRooms.DockStandOff` перед ней, не на соседней станции) — пульсирует волной заряда.
**Мул** (`StorageBot`): в нём не жгут (`GameManager.CanBurnAt` — только люк входа и шредер машины); идёт пешком, если путь по навмешу есть и не длиннее `StorageBotRules.WalkFurthest` (45 м), вентиляцией — только иначе; двери открываются и для него (`AutoDoor`), по лестнице шахты подвала ходит только он (навмеш-переход `ShaftLink` в зоне `Ladder`, `LevelMetrics.LadderArea`; остальные роботы её исключают — `RobotController` areaMask), с пола встаёт на ближайшую точку навмеша. Ноги краба разрезаны по колену (`creatures/rollers.py storage`: `AccentLeg0..5Upper/Lower`) и шагают `ProceduralLegs`, как у охранника.
**Звук роботов**: у каждого вида свой мотор (`AssetBuilder.MotorFor`) и высота голоса (`AudioRules.VoicePitch`); шаги шагающих — по
касанию ступни (`ProceduralLegs.FootDown` → `AudioDirector.OnFootDown`: охранник, ищейка, паук — свои клипы); всё, что звучит через
перекрытия, глохнет (`AudioDirector.Muffle` → `AudioRules.FloorFactor`, `AudioMix.ThroughFloor`; петли `LoopSound` тоже).
Шагов нет у сидящего в машине и на лестнице. Короткие звуки предзагружаются со сценой (`AssetBuilder.Audio`: иначе плеер не создавал FMOD-звук). Громкость из настроек применяется сразу (вес кроссфейда отдельно от громкости), фон исследования тихий (`AudioMix.ExploreMusic`),
двери — `AudioMix.Doors`.

### Уровень

`LevelGenerator.Generate(seed, level)` — чистая генерация планировки (`LevelLayout`) явным списком шагов `LevelGenerator.Steps` (план → скрытая комната → тёмные комнаты → люки → типы комнат → особые комнаты → вентиляция → мебель → охрана → особенности → двери → габариты; общее состояние — `House`).
**Правила локации** — одна стратегия `HouseRules` (`Office`/`Hospital`/`Garage`/`Apartments`, `HouseRules.Of(layout)` по `PlanKind`): своя планировка в `BuildingSettings` (`Shape`), можно ли менять типы комнат, свои проёмы, комната выхода, шанс и вид дверей, уличная дверь, вид в любом стиле (`Look`), уникальная особенность, карта до убежища; таблицы мебели по типу комнаты остаются в `Furnisher`, варианты якорей — именованные `Furnisher.Option`. `ActiveLevel.Load(layout, art)` строит сцену через `LevelBuilder`
**только из префабов `LevelArt`**: блоки-кубы масштабируются под размер, лестницы/двери/лазы/лампы/мебель ставятся как есть.
Генерация детерминирована: каждый пир строит уровень сам из общего сида (`GameManager.Level`). Навмеш, лут, роботы — только на сервере.
**План дома** (дизайн — [Docs/HouseGenerator.md](Docs/HouseGenerator.md)) — `HousePlan`: растр клеток 0,5 м на этаж (`CellKind`: комната,
карман за наклонной/дуговой стеной, пустота атриума), стены выводятся из границ растра плюс наклонные и дуговые сегменты (`PlanWall`),
проёмы (`PlanOpening`: дверь, открытый проход, лаз, щель) лежат на стенах, перила — края галерей над атриумом. `PlanGenerator`: масса здания
(бар/Г/Т/П/двор/Н + пристройка) → лобби у фасада → лестничные холлы → коридоры → BSP на комнаты любого размера, Г/Т-слияния → типы по площади
и пропорциям → атриумы в 2–3 этажа → формы (ротонда, восьмиугольник, срез и скругление углов) → связность (Краскал, петли, лазы, щели).
`HouseKinds` — характер дома из сида (офис, бизнес-центр, дата-центр, склад, штаб-квартира): клон `BuildingSettings` с другой программой комнат
и формами. `LevelGenerator` — «сгенерируй и проверь»: до 16 вариантов плана (`LevelLayout.PlanAttempt`), берётся первый, где до каждой комнаты
можно дойти. `LevelLayout` — адаптеры для остального кода (`Rooms`, `Doorways`, `Stairs`, `RoomAt`, `OpenBetweenFloors`…).
Общие правила генератора: что значит тип комнаты — `RoomTraits` (проход, своя планировка, одна дверь, середина под мебель, «мокрая», занятый пол);
связность комнат — `RoomGraph` (`Passage`: игроки / роботы / пешеходы; шаги, достижимость, «что за этой дверью»); комната выхода и входа — один раз
(`LevelLayout.ExitRoom/EntryRoom`, её тип не трогают правила и зонирование — `PlanRoom.Kept`); дверь своего назначения — `LevelLayout.EnsureDoor`,
обязательные двери есть и в тихом доме (`LevelGenerator.OwnDoors`); по какому плану построен дом — `LevelLayout.PlanKind` (тип дома, но офис,
если своя планировка локации не применялась — тест комнат); достижимость по вентиляции — `VentPlan.VoidReach/CanReach`. Мебельщик —
`Furnisher*.cs` (partial: таблицы, расстановка, проходимость, комнаты, стены, углы); сдвинутый проём — `Furnisher.DoorwayMoved`.
Мебель знает про окна и про проходимость: высокое (`Furnisher.Tall`) и висящее на стене (`OnWall`) не встаёт перед окном (`WindowStrips`; в туалетах,
раздевалках и хранилище окон нет — `LevelBuilder.WindowCuts`), одного вида в комнате не больше `Furnisher.CapOf` (один кулер, одни часы), каждая группа
при постановке проверяется той же сеткой, что и `EnsureWalkable` (`StaysWalkable`: середина комнаты, её двери и лестницы остаются связаны) — проверка
после расстановки почти ничего не выбрасывает; туалет всегда пробует кабинки (или писсуары) и раковины (`Anchor`), полупустая комната добирает группы
(`Sparse`); в переговорных и туалетах середина — для мебели, патруль стоит у двери (`LevelGenerator.PatrolSpot`, `Furnisher.MiddleIsFurniture`).
Мебель — `Furnisher`: острова с проходами 1,25 м (их примерно в полтора раза больше, чем «достаточно»: дом обыскивают), пристенные группы — вплотную друг к другу от самого угла (`Furnisher.AlongWall`: после 15 % групп шаг голой стены — `BreakChance`; не встало — следующая попытка сразу за помехой, `PastBlock`; куски стены комнаты на одной линии — одна полоса, `RunsOf`; в диагностике, где аппарат стоит посередине, проверка при постановке держит связанными ещё четыре точки вокруг него — иначе ряды шкафов у торцов разрезали комнату и финальная проверка выбрасывала аппарат) с дорожкой
перед ними и второй проход по стенам у всех комнат, в коридорах — только тонкое у стен (огнетушитель, доска, урны) далеко друг от друга, вокруг проёмов, лестниц, люка и
точек патруля — свободно; тематические комнаты (`RoomType` от `Gym` до `GameRoom`, плюс кухня и туалет) сначала получают свой узнаваемый
предмет (`Furnisher.Anchor`: варианты от большого к компактному, у стены или посреди комнаты), потом второй проход по стенам; модели —
`Tools/Blender/rooms_*.py` и `rooms2_*.py` (по пять предметов на каждый тип комнаты, висящие на стене — `Furnisher.Hung`), префабы — `AssetBuilder.Rooms` (`LevelArt.RoomProps`), полы по типу комнаты — `StyleGeometry.RoomFloors`, стены и потолки — `StyleGeometry.RoomLooks`
(`RoomLook`: отделка стен, нижний пояс плитки/мягких панелей до `LowerHeight` с рейкой поверху, `Plain` — без панелей и рейки стиля, свой потолок — тогда без
лотков; комната грани — `ArchitecturePlanner.RoomOf`; пока у «Книжки-чертежа»: туалет, раздевалка, кухня, спортзал, медпункт, лаборатория, серверная,
склад, студия, игровая, библиотека — `AssetBuilder.StorybookRoomLooks`), сушилка для рук — `HandDryer` (E: гудит `GadgetSettings.DryerTime` и шумит для роботов); `EnsureWalkable` (сетка 0,25 м) убирает мебель, отрезавшую комнату, и лут, до которого не дойти. Картинки планов
без запуска игры — `PLANS="seed:дом,…" Tools/dev.sh plans` → `Logs/plans/`. Размеры — `LevelMetrics`. Пристенная мебель держит ≥ 20 см
от оси стены (жалюзи и подоконники окон). Коллайдеры мебели — `MeshCollider` на каждой части модели. `LevelBuilderTests.FurnitureDoesNotClip` проверяет, что мебель и окна не пересекаются
со стенами, лестницами и друг с другом, `LevelNavMeshTests` — что до лута можно дойти.

### Тексты, языки и HUD

**Тексты — только по ключу.** `Loc` (`Scripts/Game/Loc.cs`, namespace `Stealth`) читает все `Resources/Strings/*.csv` (UTF-8, `key,ru,en`, кавычки по RFC 4180,
`\n` — перенос, строки с `#` — заголовки разделов; ключ живёт ровно в одном файле). `Loc.T(key)`, `Loc.F(key, args)`, `Loc.Plural(key, n)` (`key.one/few/many`);
нет перевода — русский, нет ключа — сам ключ. Язык — настройка игрока (пауза → Настройки, PlayerPrefs `stealth.lang`), по умолчанию — язык системы;
`Loc.Version`/`Loc.Changed` — для кэшей. Подписи в префабах — `L("key")` в сборщике → компонент `LocText`. Текст, который летит по сети (баннеры),
летит ключом: `Loc.Pack(key, args)` (аргумент `@key` — тоже ключ), каждый пир делает `Loc.Unpack` на своём языке. `LocalizationTests`: все строки с ru и en,
одинаковые `{n}`, ключи из кода есть в таблицах, в коде не осталось кириллицы (кроме `[Tooltip]`, комментариев, `DevAutomation*`, строк с `// loc-ok`).

**HUD почти без текста** (`GameHUD`, решения — `HudRules.cs`): прицел — точка или иконка того, что сделает E с тем, **на что смотрит прицел**
(`Aim`, `PlayerController.View`: цель E — среди того, через что проходит луч взгляда, а не ближайшее; у `Interactable` — `AimRadius`) (`ReticleRules`: поднять, взломать, замок,
сейф, ящик, медстол, верстак, ключ, рычаг, сиденье, бак, терминал, взять), тусклая — если сейчас нельзя, кольцо — удержание. Клавиши момента — стопка
справа снизу (`HintRules` → `HintView`/`KeyCapView`: нарисованная клавиша или мышь + одно слово, полупрозрачно; самое важное — внизу; очевидного
вроде WASD нет). Слева снизу — глаз (освещённость, 8 кадров от закрытого до открытого), динамик (шум: 0–3 волны) и сердечки (третье — золотое
от медстола). Иконки рисует `python Tools/Art/generate_icons.py` → `Art/Sprites/Icons` (белые с тёмным контуром, красит `Image.color`).

### Сеть

Игровая сборка `Stealth.Game` **не ссылается** на Netcode/Steam (проверяет `NetworkBoundaryTests`). Всё сетевое — в `Stealth.Net`:

```
Игра (Stealth.Game)                         Сеть (Stealth.Net)
PlayerController + PlayerInventory   ⇄  PlayerNet + InventoryNet
GameManager (+ RunState)              ⇄  GameManagerNet
Detector (все роботы и датчики)        ⇄  DetectorNet
WorldItem                             ⇄  WorldItemNet
Car (машина в канализации)            ⇄  CarNet
LaserGate                             ⇄  LaserGateNet
Session (фасад)                        ⇐  NgoSessionBackend, GameNetwork (ISessionLauncher), Steam*
```

- Игровой компонент — обычный `MonoBehaviour` с простыми полями. Его **сетевой двойник** (`…Net : NetworkBehaviour` на том же объекте)
  при спавне вызывает `Bind(...)`/`Spawned(...)`, на авторитетной стороне каждый кадр копирует поля в `NetworkVariable` (`PushState`, пишет только изменения),
  на остальных — применяет их (`ApplyReplicated`/`ApplyServerState`/`ApplyOwnerState`).
- Сообщения идут через интерфейсы из `Scripts/Game/Session.cs`: `IPlayerLink`, `IInventoryLink`, `IGameLink`, `IDetectorLink`. Двойник реализует их RPC-шками
  и вызывает серверные обработчики игрового компонента (`OnReviveRequest`, `PickupServer`, `FinishLevelServer`…). Без двойника (тесты, офлайн) запросы идут через `Offline*Link` (`Scripts/Game/OfflineLinks.cs`) прямо в серверные обработчики:
  код, который шлёт, не различает эти случаи (`Link` вместо `m_Link`). Дальность на сервере — одно правило `ServerReach` (запас на отставание позиции).
- Состояние дома для сцены — `IWorldState` (`World.State`, ставит `GameManager.Bind`): открытое по ключам `OpenKeys`, лифт, часы; статических хуков у компонентов нет.
- `Session` — фасад сессии: `IsServer`, `ServerTime`, `Spawn`/`Despawn` (сетевые префабы), `Launcher` (меню: хост, подключение, Steam). Статусы в меню — только наши слова из `net.csv` («Стим недоступен», «Не удалось создать лобби»): технические причины (`SteamManager.Error`, код лобби, причина разрыва) — только в лог.
  Без запущенной сессии этот пир — сам себе сервер, время — локальное, спавн — обычный `Instantiate`.
- Движение игрока — owner-authoritative (`NetworkTransform` Owner), здоровье, лут и роботы — server-authoritative.
- Правила забега (топливо и цена поездки, штрафы, тревоги, охотник, конец уровня) — чистый класс `RunState`.
- Склад команды у люка — `TeamStash` в `GameManager.Stash` (сервер), реплицирует `GameManagerNet`; запросы — `PlayerInventory.Request{Stash,Unstash,StashMove,SellStashed,StashWorld,SellWorld,StashAll}`.
  Сидя в люке, руки (Tab) тоже работают: доска склада висит перед глазами (`StashBoard.PlaceInFront`). «Сложить всё» — клавиша `GameAction.StashAll` (G) или кнопка на доске (`TeamStash.AddAll`).
  Записи склада адресуются по значению (`InventoryEntry`), не по индексу: список мог измениться, пока запрос летел. Склад живёт весь забег, `RestartRunServer` его чистит.

**Управление** — `Controls` (`Scripts/Player`): действия `GameAction` с клавишами по умолчанию, переназначение (PlayerPrefs), чтение устройств.
Игровой код спрашивает `Controls.Held/Pressed(GameAction.X)`, а не `Keyboard.current`; тексты для игрока называют клавиши через `Controls.Label`.
Новое действие — в `GameAction` и `Controls.All` (название, группа, клавиша), оно само появится в списке на паузе (`ControlsPanel`).

**Терминал камер**: камеру на экране игрок поворачивает сам (`SecurityCamera.SteerServer`, угол — `DetectorSnapshot.Aim`;
`IPlayerLink.SendTerminal` раз в 0,1 с); управляемая камера никого не замечает, сидящего за терминалом (`PlayerController.AtTerminal`) камеры
не видят, пинг и метка идут из вида камеры (`TerminalViewPose`). Карта (у терминала карты) — север вверху (`MapView`).
Выносливость выключена (`StaminaSettings.Enabled`). Ноги игроков — отдельные части `BodyLegL/BodyLegR` (поворот у бедра `generate_models.LEG_PIVOT`)
качаются в шаг (`PlayerController.SwingLegs`, `PlayerRules.LegSwing`), сидя в машине — согнуты. Оставленный лежать в доме возвращается с пустым рюкзаком.
**Чит-панель (F4)** — `CheatPanel` в HUD: выключить всех роботов, неуязвимость (и газ не берёт), невидимость для роботов (`PlayerController.Invisible` входит в `IsDown`; ни шума, ни следа),
квота, телепорт к люку, вся карта, **начать дом любого типа** (вторая колонка, кнопка на каждый `HouseKind`: в любой момент — в доме, в канализации, в поездке, после провала; `Cheat.StartHouse + kind` → `GameManager.StartHouseServer`, сид подбирает `HouseKinds.SeedOf`, номер дома прежний); выполняет сервер (`IPlayerLink.SendCheat` → `PlayerController.CheatServer`). Сценарий `cheats`.
**Карта, пинги, метки.** `ExplorationMap` — что команда видела (клетки плана 0,5 м, открываются веером лучей от глаз: луч останавливается на стене,
проходит в проём двери — мимо двери открывается клин комнаты, который видно сквозь неё; `HousePlan.SightShare`; сетка выходит на `Margin` = 8 м за дом —
снаружи открывается двор в радиусе `TeamSettings.OutsideRevealRadius`, на карте он — `MapPart.Ground`);
сервер открывает вокруг всех игроков, `GameManagerNet` реплицирует биты (`NetworkList<ulong>`, [0] = сид дома), клиент ещё и сам вокруг себя.
`MapSketch` превращает видимое в прямоугольники (пол, стены с проёмами, лестницы, мебель из `LevelShell.Props`, люк), `MapView` рисует
карту. **Карты на HUD нет** (ни мини-карты, ни клавиши M): карта — у **терминала карты** (`TerminalKind.Map`, `SecuritySettings.MapTerminals` = 2 стола
после терминалов взлома и камер, каждый — самый дальний от прежних, не пульт комнаты управления; столов нет (жилой дом) — стойка-киоск вместо кресла, шкафа, тумбы или гардероба (не мельче её основания): `LevelLayout.KioskTerminals`; экран `screen_map`): сидящий за ним видит всё здание (`MapView.WholeHouse`: свой
`ExplorationMap` с `RevealAll`, скрытая комната — глухой стеной) по этажу (`PlayerController.TerminalFloor`: колесо, A/D, ЛКМ/ПКМ) и на нём — игроков,
всех роботов и устройства (серые — взломанные; устройство — в точке крепления, не объектива), лазеры (`LaserSpots`), вход, выход, уличные двери, склад и терминалы; исследованное (`ExplorationMap`
команды) по-прежнему считается — для задания «найдите выход» и тестов. Пинг и метка робота — `IPlayerLink.SendPing/SendMark` → `GameManager.CalloutServer` → `IGameLink.BroadcastPing`
→ `PingBoard` на каждом пире; помеченный робот — `DetectorSnapshot.MarkedUntil`, его модели на слое `Marked` (8), фича `MarkedXray` на
`PC_Renderer` рисуют их сквозь стены одним силуэтом (`Art/Shaders/MarkedXray.shader`: маска в стенсил → контур снаружи маски → заливка
ровно один раз на пиксель; три RenderObjects-фичи, проходы 0–2). Настройки — `PlayerConfig.Team`. Взлом и вскрытие — только без стены
между руками и целью (`Perception.ReachClear`, `IHackable.ReachableFrom`). **Взлом навсегда**: робот или устройство, взломанные руками или
с терминала (`GameManager.TeamHackServer`, `GameManager.Hacks.cs`), выключены, пока команда держит не больше `InteractionSettings.MaxHacked` (2);
взлом сверх лимита будит первого (`Detector.WakeServer`); занятые слоты — ключи `OpenKeys.HackSlot` в `Opened`, в HUD — значки слева снизу (`GameHUD.HackSlots`).
Выключенный робот осел — его капсула идёт за телом до пола (`RobotAnimator.FitCapsule`), а его коллайдеры становятся триггерами — игроки проходят сквозь (`RobotAnimator.PassThrough`). **Переподключение** — `GameManager.Reconnect.cs`:
кто вышел (`PlayerController.Identity` — Steam id или id компьютера, `ISessionLauncher.LocalPlayerId`), вернётся с рюкзаком туда, где стоял (тот же дом),
иначе — к хосту (`IPlayerLink.PlaceOwner` → `PlayerController.PlaceFromServer`). Выбывший видит рюкзак того, за кем смотрит (`InventoryUI`).

**Между домами — канализация и машина.** `RunPhase`: `Playing` (дом) → `Sewer` → `Playing` (следующий дом) или `Failed`. В доме **два люка**:
вход (`LevelLayout.HatchPosition` — команда начинает дом сидя в нём, `PlayerController.EnterHatchServer(0)`) и выход
(`ExitHatchPosition`, `LevelGenerator.ExitRoom`). Люки — там, где здание встречается с канализацией, как в настоящем: вход — в **техпомещении**
(`RoomType.Utility`, 4 × 3,5 м, вырезано из наружного угла лобби — `PlanGenerator.UtilityCorner`, одна дверь — в лобби, без окон и уличной двери;
у связанных блоков — только где угол ни с чем не соприкасается), выход — в дальней комнате с канализацией (туалет, кухня, раздевалка; иначе
самая дальняя, где у стены есть место), в мастерской — в дальнем заднем углу цеха (камера маслоуловителя). Сам люк — всегда у стены, лучше в углу
(`LevelGenerator.HatchCorner`), рядом стояк с ревизией и отвод вдоль стены (`UtilityPlan.Drains`). После слияния R6–R9 Саши входного люка как такового нет: в углу техпомещения стоит крышка шахты (`PropKind.ShaftHatch`, проём — ровно 3 × 3 клетки плана: центр шахты на центре клетки, `LevelGenerator.OnCells`; рамка крышки — вплотную вокруг проёма, петли — от стороны схода с лестницы `LevelLayout.ShaftExit`), под ней — **подвал прибытия** (`Basement`, рамка `LevelLayout.Cellar`: начало и поворот на 90°·k, туннель идёт под домом — `Basement.YawUnder`): участок канализации, где остановилась машина команды, — позади невидимая стена и несколько видимых сегментов с заглушкой (`BasementBackWall`), машина у закрытых ворот (`Basement.CarStop`; у ворот подвала нет рычага и короба над сводом), за воротами сегмент и заглушка; лестница — в нише сбоку от машины (`sewer_seg_alcove` из `sewer.py kit_alcove`, префаб `Level/Sewer/Alcove`: ниша, кирпичная шахта до пола дома, лестница, лампа в шахте; крышка снизу `MetalShutLid` в подвале скрыта). Команда начинает дом сидя в машине (кто не в машине — `Basement.Spawns` на мостках), подвал считается входом. В канализации после дома последний сегмент перед воротами — та же ниша, но крышка закрыта и лестница не лазается (`SewerSegment.Alcove`): обратно в дом не подняться; комната выхода — дальняя «мокрая», но обязательно достижимая по вентиляции над потолками (`VentPlan.CanReach`, `VentPlan`). Решётки двух видов: в потолке (короба Саши, `VentGrate`, бывают прикручены — отвёртка) и в лазах в стенах (`VentGrille`, ключ `OpenKeys.Crawl`). В палатах, операционных и диагностике подъём к решётке ставится после своей мебели (`Furnisher.Climb`).
Проверяет `HatchPlacementTests`; `PlayerController.HatchIndex` (0/1, реплицирует
`PlayerNet`). **Входить и выходить можно в любой момент**: под домом всё время стоит его канализация — продолжение подвала за его воротами (`SewerLocation.UnderHouse`, `GameManager.EnsureHouseSewer`, строится по кадрам `SewerLocation.BuildSteps`; ворота и хвост подвала прячутся), поэтому подвал глубокий (`Basement.Depth` = 8 м: высокие залы и короб ворот под землёй). Люк выхода — такой же, как вход: крышка `ShaftHatch` (`Exit`, ключ `OpenKeys.ExitShaft`, рамка с лампами цвета выхода — префаб `Level/Hatch`) над шахтой с лестницей; спускаются и поднимаются сами (`ShaftLadder.All`, у каждой лестницы свой ключ крышки `LidKey`). Шахта стоит в конце **прохода** от канализации: `SewerPlan.Make(…, люк в рамке подвала)` удлиняет спусковой участок, чтобы зал был за люком (`HallPastHatch`), ставит сегмент с проёмом в стене (`SewerSegment.Door`, `sewer_seg_door`) и раскладывает узкий ход 1,5 м (`SewerPlan.Passage`: поперёк туннеля, поворот, вдоль, поворот, поперёк — `sewer_passage`, `_corner`, `_end` из `sewer.py`; `SewerLocation.BuildPassage`); люк выхода генератор ставит только туда, куда проход дотянется (`SewerPlan.PassageFits`: за невидимой стеной подвала и не ближе 8 м к оси его туннеля, `LevelGenerator.ExitReachable`). Проверяет `SewerPassageTests`. Машина въезжает в туннель — путь выбран (`UpdateRouteServer`, следующий дом уже генерируется в фоне); уезжает, когда хватает топлива на этот путь и все, кто в сознании, сидят в машине (`AllAboard`, отсчёт `VoteDelay`; до того на табличке «Все в машину») → `DepartServer`/`LeaveHouseServer` (оставшиеся без сознания — штраф и пустой рюкзак, склад выхода — в кузов, склад входа пропадает); в этот кадр канализация с машиной уходит на глубину `SewerLocation.Origin` (`ShiftSewerForDrive`), старый дом разбирается по кускам (`ActiveLevel.ClearSteps`). У каждого люка свой склад:
`GameManager.Stash` (вход: склад, куда при старте дома выгружается кузов (`UnloadCarServer`), и горловина бака — на его доске зона «сжечь в топливо»,
`StashBoard.SellZone` → `PlayerInventory.Sell*Server`/`DepositServer`; инструменты не горят) и `ExitStash` (выход: уезжает в кузов, `LoadCarServer`) — `GameManager.StashAt(позиция)`.
Вещь, брошенная из рук на шредер машины (в канализации и в подвале дома) или во входной люк, — тоже топливо (`PlayerInventory.DepositServer`, `GameManager.CanBurnAt`).
**Шредер** — как двухвальный: брошенное в бак машины ложится кучей в горловину в своём размере, валы снизу хватают до `Shredder.Lanes` (3) вещей сразу (`Shredder`, чистый: падение `FallTime`, разрыв `ChewTime`; время старта каждой вещи назначается при броске — когда освободится полоса; топливо — только когда вещь разорвана; сервер — `GameManager.GrindServer/UpdateShredderServer`, куча со стартами реплицируется `GameManagerNet` смещениями от самого раннего), `CarShredder` показывает эти вещи: схваченные трясутся, заваливаются и уходят вниз в воронку, куча оседает на освободившиеся валы, летят опилки цвета рвущейся вещи, на каждой разорванной — горсть обломков и глоток, воет `grind`; валы крутятся быстро (`Car.Grinding`). Вещи из кузова
тащатся мышью в рюкзак (`IInventoryLink.RequestTakeCargo`). Продажи нет: добыча — **топливо**. `RunState.Collected` — бак
(живёт весь забег), `Quota` — базовая цена поездки из этого дома (первая бесплатна); у каждого туннеля своя доля от неё (`RunSettings.RouteFuel`, перемешаны по сиду: `RunState.RouteShare/RouteCost`, `GameManager.CostOf`; на HUD — `FuelNeeded`: путь, где стоит машина, иначе самый дешёвый; цена — на табличке туннеля); `RunState.Advance(цена)` списывает её, `Strand` — забег
окончен, если топлива не хватит даже со всей добычей (`CanStillGo`, предупреждение `RunSettings.StrandedGrace`).
Канализацию каждый пир собирает сам: `SewerPlan.Make(RunSeed, глубина)` (чистый: заглушка, участок машины под люком входа, ворота, участок
спуска из люка выхода, зал с тремя туннелями; число и варианты сегментов, лампы, их цвет, мусор — от сида; первая канализация забега, где команда появляется, освещена лучше — лампы на обеих стенах, без сломанных, ярче в `SewerPlan.StartLampBoost` раз, и туннель за машиной вблизи — `SewerPlan.StartLights`) → `SewerLocation.Build` из кусков
набора `Tools/Blender/sewer.py` (`-- … kit`: `sewer_seg_0..3` + `_cut`, `sewer_hall_*`, `sewer_branch`, `sewer_plug`, `sewer_gate_*`, `sewer_lever`, `sewer_cap`;
**зал развилки — каждый раз другой** (`SewerHall`, чистый, свой поток `Salt.SewerHall`; `SewerPlan.Hall`): сетка клеток 8 м под кирпичными
крестовыми сводами на колоннах — коробка 2–4 × 2–4 клетки, потом Г, Т, крест или «рваный» с эркерами и вырезами; всегда связный, клетки не касаются
только углом, клетка перед входом есть всегда; свод низкий или высокий (`SewerHall.Springing`: 3,4 / 4,2 м; квадратные или круглые столбы). Туннель
входит в клетку (0, 0) в точке (0, 0, 0) зала по +X; клетка (i, k) — x ∈ 0,6 + 8i…+8, z ∈ 8k ± 4. Проёмы (`SewerPlan.Mouths`, 3–5) — в сторонах границы,
смотрят по +X или ±Z (назад — никогда), за каждым целая клетка, чтобы машина выровнялась, и его туннель не проходит ни сквозь зал, ни сквозь входящий,
ни сквозь другой туннель; три занимают туннели (`TunnelMouths`, слева направо от входа), остальные заложены кирпичом (`Sewer/Plug`), туннель без пути на
этот раз — тоже кладкой (`SewerBranch.Plug`). `SewerLocation.BuildHall` собирает зал из плиток `sewer.py kit_hall` (`-- <root> hall`: пол клетки, свод,
стена, стена с проёмом, стена входа, колонна на углах клеток, арка между соседними клетками; рамка стороны — `SewerHall.Frame`), префабы `Level/Sewer/Hall*`,
поля `SewerLocation.HallFloor/HallKits`; лампы — на половине глухих стен и части колонн. Проверяют `SewerHallTests` (машина доезжает до каждого проёма,
туннели не пересекаются, формы разные) и `SewerKitTests`; туннели 110 м, зона выбора пути до
`SewerLocation.VoteTo` = 90 м, стена — `StopAt` = 95 м: можно уехать в туннель, пока идёт отсчёт;
префабы `Prefabs/Level/Sewer/*`, `AssetBuilder.Sewer`). Дорога — решётка (`SewerGrate`, alpha-clip, `Art/Textures/Sewer/grate.png`) над водой.
Ворота (`SewerGate`, рычаг `GateLever` на стороне спуска) — `OpenKeys.Gate` в `Opened`; в начале забега открыты.
**Машина** — `Vehicles/Car` (сетевой префаб; двойник `CarNet` отдаёт владение водителю: owner-authoritative `NetworkTransform`):
4 места (`CarSeat`, `Car.SitServer`, игрок в машине — `PlayerController.SeatIndex`, `PlayerMotor.Seated`; руль слева — `CarSettings.LeftHandDrive`:
модель из Blender праворульная, левый руль — она же в зеркале, `CarLook.Mirror`, места водителя и пассажира меняются), езда — чистый `CarPhysics`
(велосипедная модель, `RunConfig.Car` → `CarSettings`), упор в стены `BoxCast`, подвеска/руль/колёса/дробилка анимирует `Car`, звук мотора
по оборотам, старт/стоп, гудок (`GameAction.Horn`, `ICarLink`). Бак-приёмник на капоте `CarHopper` (E: лут рюкзака, с пустым — весь кузов →
`FeedServer`). Кузов без ячеек — `Cargo` (куча: `Heights`, упаковка `CargoItem.Pack` в `NetworkList<ulong>`), брошенное над кузовом
ложится туда (`GameManager.DropOnCarServer`), взять — E по `CargoPiece`. Выбор пути — машина в зоне открытого туннеля
(`SewerLocation.BranchAt(машина)`, `GameManager.UpdateSewer`, отсчёт `RunSettings.VoteDelay`) → `DepartServer` → `DriveUntil`
(реплицируется). **Прибытие одной поездкой настоящей машины** (`GameManager.Arrival.cs`, путь — чистый `DrivePath`, тайминги — `Arrival`): канализация стоит глубоко под домом (`SewerLocation.Origin`, −60 м) и не выгружается, пока машина в ней; сервер запоминает, откуда машина уехала (`DepartAt`, `DriveFrom`, `DriveBranch` — реплицируются), и каждый пир сам двигает машину по общему времени (`GameManager.Driving` → `Car`: поза ставится в `Update` и `LateUpdate`, сетевой transform её не перебивает; чужие сидящие игроки держатся на своих сиденьях локально — `PlayerController.HoldRemoteSeat`). Машина выезжает в свою полосу и разгоняется до `DrivePath.Cruise`; в конце туннеля (110 м) тёмная стена убирается, переходник `sewer_reducer` открывает его в широкий туннель набора, который бежит вокруг машины (`DriveTunnel`: целые сегменты по узору из двух, лампа на чередующихся стенах, период `DrivePath.Pattern` = 12 м). С отъезда каждый пир генерирует следующий дом в фоновом потоке (`PrepareNextHouse`: какой — знает каждый, по маршруту и полосе; генерация чистая, статистика `Furnisher.Last*` — `[ThreadStatic]`), сервер через `Arrival.BuildAfter` строит его по кускам (`StartLevelSteps` → `ActiveLevel.LoadSteps`/`LevelBuilder.Steps`, `ActiveLevel.Ready`; ни один шаг не длиннее кадрового бюджета `FrameBudget` (6 мс): разбор старого дома, одевание стилем `VisualStyles.ReapplySteps` (декор `StyleDecor.RebuildSteps`, склейка `PropBatches.RebuildSteps`), спавн `SpawnContentSteps`, навмеш печётся асинхронно (`BakeNavMeshAsync`), превью домов на табличках — в фоне; после постройки подземный воздух ставится сразу (`PlayerController.SnapAir`), иначе он секунду выглядел как смена стиля; шаги дольше 30 мс development-билд пишет в лог `[Perf]`; клиенты строят так же, услышав о доме) и объявляет смену туннеля `DriveUntil` и остановку `ArriveAt` (`DrivePath.ArriveAtFor`: не раньше `Arrival.SwapAt`, далеко за переходником и в тот же момент узора). У подвала позади тот же узор: задний участок (`Basement.BackSegments`) и бегущий подъезд `BasementApproach` (заглушка спрятана, пока он есть). В `DriveUntil` машина переходит из туннеля канализации в подъезд подвала в том же месте узора на той же скорости — вокруг на дальность дымки одно и то же, смены не видно ни из кабины, ни снаружи; потом тормозит до остановки у ниши (`DrivePath.InBasement`), канализацию пир убирает через секунду после смены, кузов выгружается на склад при остановке (`UnloadCarServer`). Кто не сел в машину, попадает на мостки подвала, когда дом достроен. Под землёй (`LevelLayout.Underground`) газа нет, карта не открывается, туман — как в канализации: густой и почти чёрный, вдали всё уходит в черноту (`PlayerController.UpdateFog`), эмбиент стиля (картинка, не геймплей — `VisualStyles.StyleAmbient`) приглушён до 15 %, чернильные контуры стилевого фильтра гаснут там, где ничего не освещено (`_StyDark` в `StyleScreen.shader`); лампы канализации и бегущего туннеля приглушены (`SewerPlan` LampIntensity, `DriveTunnel.LampIntensity`); фары машины всегда горят дальним светом (`Car.Headlights`: широкий яркий луч, 72° на 65 м, чуть врозь). Панели над туннелями —
`RouteSignView`/`SewerRoute` (коротко: значок типа дома и значок главного задания — `Tools/Art/generate_icons.py` `house_*`/`job_*`, тип дома и номер, этажи и охрана, отсчёт; стоят на уровне глаз в проёме, со стороны водителя, сбоку от полосы машины — `SewerLocation.SignSpot`: над проёмом их закрывала крыша машины; панель — лицо дорожного указателя: зелёная с белой каймой на щите с двумя оцинкованными стойками до пола — `Level/Sewer/RouteSignStand`, `Tools/Blender/road_sign.py`). Модель пикапа — `Tools/Blender/car.py` (`Art/Models/Car/pickup.fbx`). Затопление — `SewerLocation.Flood`.
Позади машины — длинный туннель, откуда она приехала (`SewerLocation.BackSegments`), за невидимой стеной `BackWall`.

**Автомастерская** (`HouseKind.Garage`) — своя планировка, не офис: `HouseKinds.Apply` ставит `BuildingSettings.Garage` (не в тесте комнат с
`EveryRoomType`), два этажа, без связанных блоков, атриумов и скруглений; `PlanGenerator.Garage.cs`: масса — коробка из цеха с задней полосой и
двухэтажного блока сбоку (`GarageMass`, постов 3–6 по `PlanGenerator.BayWidth` = 4 м, больше в поздних домах); цех (`RoomType.Workshop`) — на
два этажа вдоль улицы (z = 0), в блоке у фасада приёмка (лобби) и за ней лестница (`ReserveGarage`), сзади цеха полоса комнат (`CutStrip`:
покрасочная камера `RoomType.PaintBooth`, склад запчастей, если он есть, раздевалка), наверху галерея буквой Г над цехом с перилами
(`GarageGalleries`) и комнаты над блоком и задней полосой (`ZoneGarage`); цех и камеру не трогают `RoomRules.Enforce` и `Zoning`; у камеры, как у
склада, одна дверь (`RoomTraits.OneDoor`) — в цех, широкая (`LevelMetrics.VehicleDoorWidth`, `LevelGenerator.WidenBoothDoorway` → `HouseFeatures.WidenDoorway`,
`LevelLayout.BoothDoorway/BoothDoor`): в ней большая раздвижная `LevelArt.VehicleDoor` (машину туда заезжают), остальные двери мастерской — стальные `LevelArt.ShopDoor`;
наружные двери — не в цехе и не в камере; в цехе внизу окон нет (там ворота), на втором ярусе — фонари.
**Вид мастерской в любом стиле** — `ShopLook.For` (в `StyleDecor.Rebuild` поверх геометрии стиля): без арок, пилястр, панелей, рейки и карниза — простые
коробки дверей, трубы и лотки открыто; цех, склад и раздевалка — крашеный бетонный блок с тёмной полосой до 1,2 м, эпоксидный пол, профнастил сверху
(`StyleKit.ShopWall/ShopWallLower/ShopFloor/ShopCeiling`), камера — белые фильтрующие панели и решётка (`BoothWall/BoothFloor/BoothCeiling`); текстуры
`blockwall`, `epoxy`, `boothwall`, `grating` из `generate_textures.py`; приёмка и конторы — в отделке стиля.
Мебель — `Tools/Blender/garage.py` (21 модель: седан на полу / с открытым капотом / на подставках, двухстоечный подъёмник с машиной (под ней
проходят) и пустой, инструментальная тележка, шиномонтаж, балансировка, шинный стеллаж и стопка, двигатель на стенде, кран, компрессор,
маслосборник, катушка шланга, домкрат, бочки, сварочная тележка, разметка поста, секционные ворота, стеллаж с краской; материалы `CarRed/CarBlue/
CarSilver/CarGlass/ToolRed/ShopYellow`); посты — `Furnisher.WorkshopBays` (у уличной стены по посту на 4 м: ворота, разметка, машина или
подъёмник носом в цех; где мешает проём — без ворот, без разметки, просто машина), оборудование вдоль стен — `WallGroup` цеха, мелочь в проходах —
`Island`, в камере машина посередине (`MiddleIsFurniture`: патруль у двери; люк выхода, сейф и клетка — не в камере), лобби мастерской — стойка,
кресла ожидания, витрина шин. Ось X моделей при импорте зеркалится (как руль пикапа): несимметричные габариты в `Furnisher.Foot` — зеркально.
Проверяют `GarageTests`, `LevelBuilderTests.FurnitureDoesNotClip(7,1)`, `LevelNavMeshTests(7,1)`; картинки — `PlanPictures.WriteGaragePlans` → `Logs/plans/garage_*.png`.
**Больница** (`HouseKind.Hospital`) — своя планировка, как палатный корпус: `HouseKinds.Apply` ставит `BuildingSettings.Hospital` (не в тесте комнат), 2–3 этажа;
`PlanGenerator.Hospital.cs`: «пластина» глубиной 16 м, длиной 20–44 м — на 40% меньше площади дома (`BuildingSettings.HospitalSize` = 0,6; роботы и устройства считаются от полной площади, их столько же), — передняя полоса комнат 7 м, коридор 3 м во всю длину на каждом этаже, задняя 6 м (`HospitalMass`);
лобби посередине фасада, лестницы ядром за коридором (`ReserveHospital`); полосы режет `CutStrip` (ширина по типу — `WidthOf`, палата `PlanGenerator.WardWidth` = 4 м,
хвост короче 4 м уходит соседней комнате): первый этаж — приёмное (смотровые и процедурные `Infirmary`, рентген и КТ `RoomType.Imaging`, лаборатория, ординаторские,
склад, раздевалка, комната персонала), выше — палаты `RoomType.Ward` по обе стороны и пост `RoomType.NurseStation` посередине передней полосы, открытый в коридор
целой стеной, санузел и комната персонала у лестницы, на верхнем этаже в дальнем торце — две операционные `RoomType.Operating`. У палат, операционных, диагностики
и поста одна дверь — в коридор (`RoomTraits.OneDoor`, `PlanGenerator.OwnDoor`); эти типы (и мастерской) не трогают `RoomRules.Enforce`/`Zoning` (`RoomTraits.Planned`).
Мебель — `Tools/Blender/hospital.py` (16 моделей): палата — две кровати головой к стене, отодвинутые на 0,45 м, над каждой изголовье с газовыми розетками,
вокруг шторка на потолочной направляющей, тумбочка (`AnchorGroup` Ward); пост — стойка лицом в коридор за серединой комнаты, стулья за ней (`Furnisher.StationCounter`);
операционная — стол, наркозный аппарат у изголовья, инструментальный столик и монитор с дальней от двери стороны, лампы над столом (`Put`); диагностика —
томограф (от 30 м²) или рентген; коридоры — каталки, кресла, тележки, дозаторы; лобби — два ряда кресел ожидания у стены и стойка регистратуры.
Шторка и операционные лампы — без коллайдеров, проверка пересечений их не щупает. **Вид в любом стиле** — `ClinicLook.For`: простые коробки и плинтус,
полоса стеновой защиты до 1 м с отбойником поверху, моющаяся покраска (в палатах теплее), подвесной потолок, линолеум (`vinyl` из `generate_textures.py`),
операционные и диагностика — белые панели и зеленоватый пол (`StyleKit.Clinic*/Theatre*`); двери — светлые `LevelArt.ClinicDoor`. Проверяет `HospitalTests`;
картинки — `PlanPictures.WriteHospitalPlans`.
**Глубина и проёмы-руины** (ветка Саши, R2–R5): `RoomDepth` — шаги от лобби (`LevelLayout.RoomSteps`, `DepthShare`), приватные комнаты, лут и охрана (`SecuritySettings.DeepRoomWeight`) тяготеют вглубь; петли — двери или руины по весам `BuildingSettings.Loop*Weight` (`EdgeType.Breach` — пролом, ходят все, `Hole` — дыра у пола, `InnerWindow` — окно без стекла; префабы `BreachFrame/HoleFrame/InnerWindowFrame`, модели `ruins.py`), лазы предпочитают тупиковые комнаты (`DeadEndSneakBonus`); соседство типов — `RoomRules.AdjacencyDefaults` → `Zoning`; у комнаты свой главный предмет (`Furnisher.Signatures`); склад — стеллажи `StorageRack` (`storage.py`) и своя дверь `LevelArt.StorageDoor` с табло. Статистические тесты планировки (`RoomDepthTests`, `StealthLayoutTests`, `ZoningTests`) меряют офисные дома: у больницы и мастерской план свой.
**Жилой дом** (`HouseKind.Apartments`) — один подъезд, этажей больше (4–6, `HouseKinds.Apply` ставит `BuildingSettings.Apartments`), на этаже три квартиры; `PlanGenerator.Apartments.cs`: «пластина» 7 + 3 + 7 м (корпус 17 м, размеры по типовой серии П-44Т: квартиры 48–72 м², кухня 12 м², спальня 14 м², гостиная ~16–22 м², санузел 3 × 3 м под наши широкие двери) — площадка-коридор во всю длину, две квартиры спереди, одна сзади у лестничной клетки 4 м в торце. Вентшахта (1 × 0,5 м, клетки `Pocket`) в углу каждого санузла у площадки, сквозь все этажи (`PlanGenerator.Shaft`). Лазов, щелей и дыр между квартирами и в подъезд нет; внутри квартиры комнаты открываются только в прихожую (кухня — ещё и в гостиную), спальня — одной дверью (`FlatAllows`). Санузел задней квартиры — в дальнем от лестницы конце. **Балконы** — на этажах выше первого у гостиных (и части кухонь) на длинных фасадах, один над другим (`PlanGenerator.Balconies` → `HousePlan.Balconies`/`PlanBalcony`): дверь в наружной стене (застеклённая двустворчатая, открывается в комнату) и плита 3,2 × 1,1 м с парапетом 1 м (`Level/Balcony`; коллайдер парапета ниже прыжка — `BalconyClimb`, с балкона можно спрыгнуть); балкон — часть дома для газа (`LevelLayout.IsInside`). **Всё, что идёт через этажи, стоит одно над другим**: одна клетка, марши каждого этажа строго над нижними (`Stair.Stacked`: тонкие марши `StairGeometry.Boxes(true)`, проём в плите только над маршами — `PlanGenerator.HoleOf`, префабы `Level/StairsStacked(Mirrored)`, четыре вида `AssetBuilder.StackedLooks`), а этажи со второго — один типовой: те же квартиры (сторона санузла — от места квартиры, не от этажа), санузлы и кухни стояком (стояки — `UtilityPlan`), те же двери и лазы (`OpeningsFloor`). Квартира: прихожая `RoomType.FlatHall` и санузел `Bathroom` (узкая дверь `LevelMetrics.NarrowDoorWidth`, `LevelArt.NarrowDoor`) вдоль площадки, кухня, спальня `Bedroom` и гостиная `Living` у окон; между квартирами — только лазы, на площадку — только дверь прихожей (`FlatAllows`, `PlanRoom.Flat`; входная — `LevelArt.FlatDoor`), внутри квартир колец нет, двери ближе к углу (0,6 м). **Лифт** — не в каждом доме: от `BuildingSettings.LiftAlwaysFrom` (6) этажей всегда, ниже — с шансом `LiftChance` (свой поток рандома); шахта — полоса 3 м между задней квартирой и лестничной клеткой, запечатанная (клетки `Pocket`) на всех этажах одна над другой, колодец кабины — `HousePlan.LiftWell` (сквозь все перекрытия, на первом — приямок), на каждой площадке проём `EdgeType.Lift` (`PlanGenerator.LiftDoorways`) с раздвижными дверями `Level/LiftDoor` и кнопкой вызова. Кабина `Level/LiftCab` (`LiftCab`, кнопки этажей на панели у дверей) ездит по состоянию сервера `GameManager.Lift` (`LiftState`: откуда, куда, когда; реплицирует `GameManagerNet` одной переменной); чистое правило — `LiftRules`: вызов и кнопка этажа действуют, только пока лифт стоит открытым, двери закрываются → едет → открываются на новом этаже и остаются открытыми; дверь этажа, где стоит кабина, открыта; кто стоит в проёме — двери не закроются (`LiftRules.Hold`). Игрока в кабине везёт его мотор (`LiftCab.CarryAt`); роботы в лифт не ходят (кабина вне навмеша, в проёме — no-walk объём). Модели — `Tools/Blender/doors.py lift_*`, префабы — `AssetBuilder.BuildLiftDoor/BuildLiftCab`; проверяет `LiftTests`.
Первый этаж: подъезд (единственный выход на улицу) перед лестницей, техпомещение с люком входа и кладовая/консьержная; `RoomRules.Enforce`/`Zoning` дом не трогают. Мебель — `Tools/Blender/apartments.py` (кровать, шкаф, комод, ванна, унитаз, раковина с зеркалом, стиральная машина, обувница, вешалка, почтовые ящики, двери лифта; шкаф, комод и обувница открываются — `Items.Storage`), вид в любом стиле — `HomeLook.For` (обои и паркет в квартирах, плитка в санузлах и над кухонным рядом, в подъезде крашеная панель до 1,5 м под побелкой). **Карта до убежища** (`ItemCatalog.ShelterMap`, 3 × 3, держится в руке, не горит и не возится роботами — `Unburnable`/`IsTool`): одна на дом, в квартире выше первого этажа (`HouseFeatures.PlanShelterMap` → `LevelLayout.MapFlat/MapRoom/MapSpot`), сервер прячет её в шкаф/комод/обувницу этой квартиры (`GameManager.HideShelterMap`) или кладёт на пол; в руке перед лицом раскрывается пустая карта — 3D-объект `PlayerController.HeldMap` (модель `held_map`), его видят все; в кузове — пачка 3 × 3 (`item_map_piece`). Проверяет `ApartmentTests`; картинки — `PlanPictures.WriteApartmentPlans`. Статистические тесты планировки меряют дома офисного генератора (`HouseKinds.OwnPlan`).
**Особые места дома** — чистый `HouseFeatures` (каждое — свой поток рандома), настройки `LevelConfig.Features` (`FeatureSettings`):
типы домов `HouseKinds` + `Location` (офис — пять характеров, больница, автомастерская, жилой дом). **Уникальная особенность локации** — у каждой своя, одна и не повторяется:
офис — стол с глушилками (`Programmer`, кубики-оглушители по игроку), автомастерская — верстак (`Workbench`: `Crafting.Plan` — вещи одного цвета → `ItemCatalog.Diamond`),
больница — медпункт (`MedTable`: здоровье до `GadgetSettings.MedTableHp` = 3, раз за дом — `OpenKeys.Med`), жилой дом — спрятанная карта до убежища
(`ShelterMap`, ниже). Стол, верстак и медпункт — `LevelLayout.PointOfInterest` (`HouseFeatures.PointOfInterestKind`): встают вместо большого стола/дивана/стеллажа,
сжимаются до его габаритов `PropPlacement.Scale`, но сначала ищется мебель, под которую сжимать почти не нужно (`HouseFeatures.MinPointScale` = 0,85: медпункт на месте дивана или стеллажа был игрушечным); у жилого дома такого предмета нет; офис — больше и ценнее лут
(`LootSettings.OfficeItems/OfficeBonus`). **Скрытая комната** (`HideRoom`: окружённая комнатами, входы `EdgeType.Illusion` с каждой
соседней; `LevelArt.IllusionBlock` — стена без коллайдера с NotWalkable-объёмом; карта и `HousePlan.SightShare` не видят сквозь неё;
проверка проходимости робота её не требует). **Двери с ключом** (`KeyDoors`: дверь — единственный путь в 1–3 комнаты; ключ — предмет
`ItemCatalog.Key` в другой комнате, только на открытой поверхности (`HouseFeatures.OnOpenTop`: стол, стойка, верстак; не в шкафу и не на полке стеллажа) или на полу; `AutoDoor.NeedsKey`, `KeyLock`). **Ящики** (`Crates`: стеллаж/паллета/коробки → `SearchCrate`,
содержимое `GameManager.CrateContents`, ключ `OpenKeys.Crate`). **Сторожа** (`Wardens`: `RobotKind.Warden` перед единственным проходом комнаты
с лутом, `RobotController.SetPost` — ходит A↔B, смотрит наружу; колонки `LureSpots`). Инструменты — особые формы `ItemCatalog` (в инвентаре — свои цвета, которых нет у лута: `ItemCatalog.UiColor`)
(`LootShapes` = 8; `Diamond`, `Key`, `Stun`, `Speaker`, `CageKey`; горят в топливо по цене серого цвета; ключи, ключ клетки (1×2) и оглушитель
работают только из руки — `PlayerInventory.Holds/TakeHeldServer`; ключи — настоящие модели, как лопата, `ItemCatalog.KeyScale`): брошенная колонка (`WorldItem.PlayServer`, реплицирует
`WorldItemNet`) зовёт ближайшего робота (`GameManager.UpdateLures`), он её выключает; оглушитель — `GameAction.Gadget` (X) →
`GadgetServer` (все детекторы взломаны на `StunDuration`). Любая цель рук — `Interactable`, сейф, терминал, замок двери — `IReachTarget`, один общий поиск `ReachSearch.Nearest`;
запросы по E — один `IPlayerLink.SendUse(UseKind, индекс)` (замок — `UseKind.DoorLock`, сейф — `Safe`, дверца/ящик мебели — `StoragePart`, индекс `OpenKeys.StoragePart`);
всё, что открывается (двери, крышка шахты, решётка, ящик, ворота, дверцы шкафов), движется по `OpenTween` (первый кадр — без анимации и звука), звук дверей — `DoorSounds`.
Лестница шахты подвала берёт игрока, только когда он внутри шахты (`LadderMath.GetsOn`: проходя мимо открытой крышки в лобби, его больше не переносит на выход) или когда сверху идёт в открытый проём с любого места его края (`LadderMath.GetsOnFromAbove`/`Lower`: опускает к перекладинам на `LowerDepth`, дальше W/S); наверху он вылезает сам — поднимается над краем и сходит на пол лобби (`LadderMath.ClimbOut`), без телепорта; в люк выхода тоже съезжает к середине, а не переносится (`PlayerController.EnterHatchIfNeeded`). **Люк выхода** выглядит как крышка шахты: квадратная стальная рама с жёлтой кромкой, тёмный колодец, рифлёная крышка на петлях вдоль +X, лампы цвета люка в углах рамы (`AssetBuilder.BuildHatch`); петли крышки — у ближайшей стены (`ShaftHatch.HingeAgainstWall`, при первом открытии): открытая, она стоит у стены, а не плитой посреди комнаты. Предмет из рюкзака, брошенный в занятую руку, меняется с ней местами (`PlayerInventory.CanSwapHand/SwapHandServer`: то, что было в руке, — на его место, иначе в свободное). Пустую поясную сумку можно снять, взяв за пояс, и сжечь в топливо. Режимы рук игрока (диск, пульт, терминал) выходят вместе — `PlayerController.LeaveAllModes`; сам игрок разбит на partial-файлы `PlayerController.*.cs`.
Всё, что игрок использует по E, — `Interactable` (`UseKind`, `IPlayerLink.SendUse`
→ `GameManager.UseServer`). Модели — `Tools/Blender/poi.py`.

**Классы и стартовые предметы.** Новый забег начинается с **экрана выбора** (`GameManager.Picking.cs`, реплицирует `GameManagerNet`; автопилот его пропускает — `DevFlags.SkipPicking`): команда (до 4) стоит в ряд за машиной в первой канализации, по центру кадра при любом числе игроков (`PickingRules.Spot`), камера каждого — близко перед рядом, угол обзора свой (`PickingRules.Camera/Fov`, `PlayerController.ShowPickingView`), инвентаря на экране нет, под каждой фигурой колонка `StartSlotView` (презентер `StartScreen` в HUD вне Content, на всегда включённом держателе — включает и выключает дочерний `Screen`): стрелки класса и предмета (работают только свои; ←/→, ↑/↓; иконки `arrow_left/right`, под мышью светятся и растут — `UiHover`; у экрана свой канвас с рейкастером), смена класса сразу переодевает фигуру, «Готов» (пробел/Enter); выбор — `PlayerController.LoadoutClass/Item/Ready` (`IPlayerLink.SendLoadout`, реплицирует `PlayerNet` одним `ushort`), все готовы → `GiveLoadoutServer` кладёт инструмент класса в руку и предмет в рюкзак. Классы (`ItemCatalog.ClassTools`): отвёртка, клавиатура, рация, планшет; стартовые предметы (`ItemCatalog.StartItems`): фонарик, аптечка, кофе, топливоискатель, ботинки. Всё это лежит и в домах (строки таблицы лута `Level.asset`). Что делают (числа — `GadgetSettings`, правила — `GearRules`): **клавиатура** в руке — без неё роботов взломать нельзя (устройства можно), а робота с ношей (ключ клетки, лут) без неё можно только обыскать — ноша падает, робот не взломан (`RobotController.SearchServer`, прицел `Search`); **рация** в руке — голоса союзников слышны с любого расстояния так же громко и чисто, как стоящего вплотную (`VoiceAcoustics.OverRadio`: не тише, чем с `RadioDistance` = 0,6 м, без приглушения стенами, без эха комнаты, без панорамы), а когда союзник говорит, сервер делает шум у каждой рации в руке (`VoiceHub.MakeNoise`, `RadioNoise`); **планшет** в руке — мини-карта увиденного прямо на экране 3D-модели в руке (world-space канвас `GameHUD.TabletScreen` с `MapView` ложится на часть `EyeScreen`: `PlayerController.HeldScreen`; слой WorldText), клавиша руки подносит планшет к глазам (`PlayerController.TabletOpen`, `UpdateRaise`); на HUD мини-карты нет (терминал карты показывает весь дом); **аптечка** — клавиша руки: здоровье до полного; **кофе** — клавиша руки: 60 с бег ×1,5 и любое удержание E ×2 (`PlayerController.CoffeeUntil`, реплицируется; `Motor.SpeedScale`, `WorkDt`); на бегу под кофе — полосы скорости по краям экрана и шире обзор (`RushRules`, `PlayerController.Rush`, `GameHUD.SpeedLines`, картинка — `Tools/Art/generate_speed_lines.py`, `Motor.FovBoost`); **топливоискатель** в руке мигает тем чаще, чем ближе лут и чем точнее на него смотришь (`GearRules.FinderReading`: сзади — как в `FinderBehind` раз дальше; `FinderBlink` на модели: настоящий `Light` + `LightSource` — вспышки подсвечивают держащего для роботов, звука нет); **ботинки** — пока в рюкзаке, падение с любой высоты безопасно; без них падение с 3 м замедляет на 5 с (выше — сильнее), выше 6 м — ещё и удар (`GearRules.Landing`, владелец → `IPlayerLink.SendFallHurt`). **Мусор** (`ItemCatalog.FirstJunk..LastJunk`: томаты, шахматы, журнал, таблетки, паста, изолента, спички, бумага, огрызок, мыло, перчатки, ожерелье, швабра, игрушка, носки) — лут без действия; у каждой вещи **своя фиксированная редкость** (`ItemCatalog.Rarity`: цвет из `Colors`, `FreshColor` и `Value` берут её, что бы ни выпало), редкие реже в таблице, богатое место берёт лучшую из двух (`LootSettings.RichBonus`). Модели — `Tools/Blender/items_more.py`, куски сетки — `generate_models.py SHAPES`, иконки — `item_icons.py`, материалы — `AssetBuilder.GearLooks`.
**Часы дома и итоги.** Часы (`GameManager.HouseClockAt`, реплицируется) **идут вверх** с момента, как команда поднялась в дом; панель слева сверху под панелью дома (`GameHUD.ShowHouseClock`, по мотивам таймера сложности Risk of Rain) показывает время, **секунду, на которой придёт следующий робот подкрепления**, полосу до него и краснеет с каждым пришедшим (`HouseClockRules.Heat`, с потолком). В момент входа всем — баннер во весь экран «Роботы ИЩУТ вас», больше ничего (`ShowEntryBanner`). Сервер считает по игрокам (`GameManager.Stats.cs` → `HouseStats`: топливо, падения, подъёмы союзников, метры, шум, разговор, взломы, подобранное, замки, присед, «попался», жёсткие посадки) и при отъезде из дома шлёт всем титулы (`IGameLink.BroadcastHouseStats`): каждому — то, в чём он выделился больше других («Кормилец команды», «Любитель полежать», «Самый многословный»…, `HouseStats.Titles`); панель итогов — на всю поездку (`GameHUD.ShowHouseStats`).
**Слоты взлома** в HUD: свободные — синие молнии слева, занятые — серая иконка взломанного вида (`HackSlotRules`; вид — в ключе `OpenKeys.HackSlot(slot, kind)`, `GameManager.HackedKind`), слева всегда следующий на замену. Иконки видов роботов и устройств — `generate_icons.py robot_*` (и на карте терминала: `MapView.KindIcons`).

**Главное задание дома** (необязательное, видно ещё в канализации на панели туннеля: `SewerRoute` строит превью дома) — `LevelLayout.Objective`
(`HouseFeatures.PlanObjective` до мебели, `FinishObjective` после охраны; свой поток рандома; настройки `FeatureSettings` + `ItemsConfig.Objectives`):
**большой сейф** — комната за единственной дверью (`VaultRoom`/`VaultDoor`, `AutoDoor.Vault`: не взламывается, табло `VaultPanel` с тремя лампами;
дверь — круглая банковская на петле, `LevelArt.VaultDoor`, `AutoDoor.Swing`; диск увеличен (`AssetBuilder.VaultDoorScale`) и прижат к стене — перекрывает отверстие
и гильзу без щели; пульты её только разблокируют, открывает игрок по E (`VaultHandle`, `UseKind.VaultDoor` → `GameManager.OpenVaultServer`, ключ `OpenKeys.Stage(JobStage.VaultOpen)`,
`AutoDoor.IsSwungOpen`): сначала крутится штурвал (`AutoDoor.Wheel`, часть модели `TrimWheel`), потом дверь поворачивается внутрь (`AutoDoor.VaultStep`) и остаётся открытой;
в комнате сейфа нет других проёмов (`HouseFeatures.OnlyWayIn`: ни двери на улицу, ни лаза); стены — стальные панели с заклёпками, пол — рифлёная сталь в любом стиле
(`ArchitecturePlanner.PlanVaultFace`, `DecorPiece.Vault`, материалы `StyleKit.VaultWall/VaultFloor`, текстура `vaultsteel` из `generate_textures.py`), без постеров и пятен; проём хранилища шире (`LevelMetrics.VaultDoorWidth`, `HouseFeatures.WidenVaultDoorway` двигает его вдоль стены), стальное кольцо проходит сквозь стену, а углы между кругом и прямоугольником проёма — сама стена (`AutoDoor.WallParts` → `LevelShell.WallParts`: `StyleDecor` одевает их в отделку стены с её раскладкой, как пазухи арки; меши — `AssetBuilder.WallCornerMeshes`), у такого проёма нет обрамления и откосов стиля (`WallOpening.Vault`); внутри — ячейки, полки с деньгами,
золото на поддонах и тележки, `Furnisher.VaultRoom`; запертые двери — с навесным замком `AutoDoor.Padlocks`, двери фасада — тяжёлые распашные `LevelArt.ExteriorDoor`: две створки открываются наружу (`AutoDoor.SwingB`,
`SwingTime`; `LevelBuilder` поворачивает +Z двери на улицу), стальная коробка ровно по проёму — обкладка откосов и наличники на обеих сторонах стены),
три пульта на стенах ближайших комнат (`VaultConsoles`, `VaultConsole`: диск — мини-игра сейфа, переключатели `SwitchPuzzle`, последовательность
`SequenceLock`; экран `VaultLockView`, цифры — `Controls.DigitPressed`), все три решены → дверь разблокирована, внутри сокровища (`VaultTreasure`);
задание показывается по шагам (`ObjectiveRules.Step`, `GameManager.ObjectiveStage`: найдите сейф → разблокируйте дверь (k/3) → откройте её;
найдите заложника → заберите ключ у охранника (заложник по E помечает его для всех) → освободите); **заложник** — клетка `Cage` (`PropKind.Cage`) в большой комнате (в «Книжке-чертеже» — своя модель: латунная клетка-«птичья» на деревянном
постаменте, замок, связанный заложник — `rooms2_special.py cage_storybook`; вид по стилю переключает `StyleParts`), ключ `ItemCatalog.CageKey` на спине охранника (`KeyCarrier`, `RobotController.CarryServer`;
взломан — роняет), с ключом E → награда (алмаз). Награды не входят в квоту дома. Сервер — `GameManager.Objective.cs`, строка в HUD — `ObjectiveRules`;
сценарий `objective` проходит оба задания настоящими клавишами.
**Двери** — `DoorRules` (чистый, после мебели: `LevelLayout.DoorLooks` параллельно `AutoDoors`): вид (`DoorKind`), сторона открывания и петли.
Почти все — на петлях: тонкая деревянная филёнчатая между комнатами (`WoodDoor`/`NarrowDoor`), двустворчатая со стеклом в большие комнаты и гостиную
(`WoodDoubleDoor`), стёганая стальная у квартиры (`FlatDoor`), толстая подъездная с домофоном и доводчиком на улицу жилого дома (`EntranceDoor`), стальная
(`ShopDoor`: мастерская, техпомещение, склады; уличная мастерской), больничная (`ClinicDoor`) и маятниковая с иллюминаторами в коридорах и операционных
(`ClinicSwingDoor`, `AutoDoor.BothWays`: от того, кто подошёл); раздвижная стеклянная (`AutoDoor`) — только серверная, лаборатория, пульт и коридоры
дата-центра; склад, покрасочная камера и сейф — свои. Одностворчатые — «полуторные» (проём 1,6 м): основная створка ~0,95 м пропускает игрока,
узкая открывается только для робота (`AutoDoor.SecondForRobots`, свой `BlockerB`). Куда открывается: квартира — наружу на площадку, санузел и
кладовка (< 6 м²) — из себя, из коридора — в комнату, между комнатами — в более глубокую (`RoomSteps`); петли — у косяка ближе к углу, выключатель —
у ручки (`DoorRules.HandleWay` → `UtilityPlan.SwitchSpots`); из четырёх вариантов берётся первый, чей размах (`DoorRules.Sweeps`) не задевает стену
чужой комнаты, лестницу, мебель и соседние двери (`CountClashes`). Всегда с дверью: вход в квартиру, санузел, туалет, спальня, техпомещение
(`DoorRules.Always`, их не занимают лазеры и плиты); в квартирах и больнице дверей больше (`DoorRules.Chance`). Префаб открывается в свой +Z
(`LevelBuilder` поворачивает его на 180° для `Back`), главная створка — на −X, `AutoDoor.HingeRight` зеркалит петли; сборщик — `AssetBuilder.BuildHingedDoor`,
модели створок и фурнитуры (ручка, D-ручка, петля, доводчик, домофон) — `Tools/Blender/doors.py`; звуки `door_open`/`door_shut` (`AutoDoor.Sound`).
Проверяют `DoorRulesTests`.
**Запертые двери и сейфы.** `LevelLayout.LockedDoors` (индексы `AutoDoors`, свой поток рандома, с `LockedDoorsFromHouse`): `AutoDoor.Locked`
не пускает игроков, роботов пускает; взлом — держать E (`InteractionSettings.DoorHackTime`) → `SendUse(UseKind.DoorLock)`.
Сейф — `SafeBox` на префабе Props/Safe (индекс в `LevelLayout.Props`, сид кода от сида дома), мини-игра — `SafeLock` (настройки
`ItemsConfig.Safes`), экран — `SafeCrackView` (хромированный диск с рифлёной ручкой, окна кода со светодиодами, осциллограф: каждый щелчок — импульс, тем выше и зеленее, чем ближе число); пульты хранилища — `VaultLockView` (стеклянные лампы в хромовых ободах с ореолом, клавиши-колпачки); картинки миниигр — `python Tools/Art/generate_minigame_ui.py` → `Art/Sprites/UI/Minigame` (`AssetBuilder.MgSprite`, рамки 9-slice там же); открытие → `SendUse(UseKind.Safe)` → лут на сервере. Открытые двери и сейфы дома —
`GameManager.Opened` (ключи `OpenKeys`: вид в старшем байте, индекс под ним — `OpenKeysTests`; сцена читает их через `IWorldState`/`World`, без статических хуков), реплицирует `GameManagerNet`.

**Терминалы охраны** — `LevelLayout.Terminals`. Камеры и взлом стоят в комнате управления роботами (у взлома — роботы и устройства, но не камеры: они только у терминала камер, `SecurityTerminal.Targets`) (`RoomType.ControlRoom`: если в доме её нет,
`LevelGenerator.EnsureControlRoom` делает ею офис 16–60 м²; пульты с креслами ставит `Furnisher` как якорь и пристенные группы, `LevelGenerator.PlaceTerminals` берёт их
первыми, лишние становятся столами; без комнаты — часть столов, свой поток рандома). Вид — `Tools/Blender/terminals.py`: пульт с тремя мониторами
(`terminal_console`) и для двери склада — стойка доступа с маячком (`terminal_access`), `SecurityTerminal.Console/Access`. `SecurityTerminal` на префабе
Props/Terminal: камеры (вид через `SecurityCamera.Pivot`) или взлом издалека — `IPlayerLink.SendRemoteHack` → `GameManager.RemoteHackServer`,
заряды (`InteractionSettings.TerminalHacks`) хранятся ключами `OpenKeys.Terminal` в `Opened`. Игрок за терминалом — `PlayerController.UsingTerminal`.
На мониторах — картинка своего дела (`SecurityTerminal.Pictures/AccessPictures`, часть `EyeScreen` с UV 0..1 на каждый экран): камеры, взлом, доступ к двери; потраченный терминал тускнеет; у пультов хранилища на табло — замок, закрытый / открытый (`VaultConsole.LockedPicture/OpenPicture`). Картинки — `python Tools/Art/generate_screens.py` → `Art/Textures/Screens`.
**Датчики движения** висят в углу вплотную: задняя плита касается обеих стен (`LevelMetrics.SensorCornerInset`, смотрят по диагонали; `Wards` и `LevelGenerator`; проверяет `RobotTests.MotionSensorsSitFlushInTheirCorners`).
**Узкие щели** — `EdgeType.Squeeze` (`LevelMetrics.SqueezeWidth`, свой поток рандома): игрок протискивается, роботов не пускает `NavMeshModifierVolume`
в префабе `Level/SqueezeFrame`. **«Я застрял»** — `Unstuck` (кольца свободных точек вокруг игрока, потом точки патруля/спавны канализации; двигает себя
владелец, у клиента нет навмеша). **Стиль (F8)** — F8 открывает список стилей (`StylePanel` в HUD, вне `Content`: работает и в меню; кнопка на каждый стиль списка, текущий подсвечен), выбор общий для команды: `GameManager.RequestStyle` → `StyleIndex` реплицирует `GameManagerNet`.
**World-space текст** (панели туннелей, доска склада) — на слое `WorldText` (9): обычные проходы его не рисуют, его рисует RenderObjects `WorldText` после
фильтра `StyleScreen` — буквы читаются в любом стиле. Выбывший игрок смотрит глазами союзника (`PlayerController.Watching`) и видит своё лежащее тело (`PlayerController.ShowOwnBody`: обычно у владельца оно только тень). Выбывший может **позвать** (`GameAction.CallHelp`, Z): пинг на своё тело `PingKind.Help` (один на игрока, живёт `MarkDuration`), его модель видна всем сквозь стены, пока зов не погас (`PlayerController.ShowCalling`, слой `Marked`).

**Ноги роботов** — `ProceduralLegs`: ноги, разрезанные в Blender по колену (`AccentLeg{n}Upper` / `Lower`, `generate_models.split_leg`),
сборщик превращает в пары шарниров бедро/колено (`ProceduralLegs.Bake`); в игре ступни стоят на поверхности под ними (пол, у паука —
стена), отстающая ступня шагает дугой, ноги — двумя чередующимися группами, колени — двухзвенная ИК (`LegMath`). Охранник, пёс, паук, сейсмик (восемь ног из `creatures/rollers.py`, разрезаны по колену).
Шаг — `AnimationSettings.StepStride/StepTime/StepLift` в конфиге робота.

**Город вокруг дома** — декорация (`CityPlan`, чистый, от сида дома): дом — одно из зданий квартала. Фасад (сторона лобби, `CityPlan.SideNearest`) выходит на тротуар 3,5 м и двухполосную улицу, соседи примерно той же высоты стоят в 3–6 м по бокам на той же линии застройки, за кварталом — задний проезд, по краям — поперечные улицы с зебрами; напротив и за проездом — ряды домов, позади — башни. Тротуар — на уровне пола дома (`CityPlan.WalkTop`), дороги и земля — на бордюр ниже (`RoadTop`), по краям тротуаров — гранитный бордюр. Невидимая стена — в `CityPlan.Yard` = 10 м от дома (`LevelArt.Fence`); тротуары, дома, машины, столбы, стволы, скамейки в пределах досягаемости получают коллайдеры (`City.Ground`/`City.Solids` → блоки `Fence`). Коробки (`CityBox`) — только земля и тела домов (фасадные текстуры с горящими окнами — `python Tools/Art/generate_city.py`, этаж фасада = этаж дома; у висящей коробки есть дно); всё остальное — модели (`City.Props`: `CityProp` — `CityPropKind`, поза, растяжение, `Paint`/`Pane`) из `Tools/Blender/street.py` → `Art/Models/City/city_<вид>.fbx`: фонари, три дерева, скамейки, урны, гидранты, столбики, светофоры, остановка, люки, кашпо; припаркованные седан (тот же, что в автомастерской, `garage.sedan`), хэтчбек и фургон шести цветов; на фасадах — кит: пояса между этажами, карниз и парапет на всех видимых из двора гранях (грани, которых со двора не видно, пустые), а у домов ближе `CityPlan.DetailReach` = 40 м ещё настоящие окна поверх нарисованных (`CityPlan.WindowOf` × этаж — совпадают с `generate_city.py` и `street.py`; стекло — `Pane`: горит, синий телевизор или тёмное), витрины с вывесками и маркизами и вход на первом этаже улицы, пилястры, руст по углам, витражные секции, водостоки, балконы, французские балконы, пожарная лестница, кондиционеры, тарелки; на крышах — машинное отделение, кондиционеры, вытяжки, мачты, бак. Машины — только в `CityPlan.CarReach` = 60 м от дома. Имя объекта модели — её `CitySlot` (`Livery` — краска копии: цвет машины, отделка дома, маркиза; `Pane` — стекло копии); детали — свой поток сида на дом (`Salt.CityDetail`). Профили растягиваются по стене, на углах один заходит за угол, другой не доходит — двух копий в одной плоскости нет. `AssetBuilder.City` собирает части моделей в `LevelArt.CityMeshes` (`CityMesh`: меши, слоты, положения), `LevelBuilder.BuildCity` склеивает коробки и все копии моделей в один меш с подмешем на материал (`CitySlot` → `LevelArt.CityMaterials`; `CityPlan.AddModel`: UV в метрах мира, растянутое не мажет текстуру; ни объектов, ни рендереров на копию). Свет фонарей — `Level/CityLamp` (конус 70° вниз под линзой фонаря, без теней и без `LightSource`: в дом не светит, на геймплей не влияет). Асфальт лежит под всем кварталом — шахту в подвал из него вырезает `CityPlan.CutHole`. Проверяет `CityPlanTests`.
**Коммуникации** — декорация (`UtilityPlan`, чистый, по плану и оболочке дома; без коллайдеров): вентиляционный короб под потолком коридоров
(`DuctBottom..DuctTop` — вплотную под потолком, 8 см до него, с решётками и хомутами, огибает высокие лазы и лестничные проёмы), электрощиток на стене коридора
каждого этажа с кабель-каналом к потолку и лотком вдоль стены, накладные розетки за столами (и на стенах комнат без столов),
стояки и труба вдоль стены в туалетах, на кухнях и в раздевалках; `LevelBuilder.BuildUtilities` склеивает в один меш (`UtilitySlot` → `LevelArt.UtilityMaterials`).
**Выключатели света** — настоящие: у каждого проёма внутри комнаты (не коридора) префаб `Level/LightSwitch` (`UtilityPlan.SwitchSpots`, номер в списке —
индекс; `LevelBuilder.BuildSwitches`), E (`UseKind.LightSwitch`) → `GameManager.SwitchLightsServer` переключает ключ `OpenKeys.Lights(комната)` в `Opened`:
светлая комната гаснет, тёмная загорается (`LightMath.SwitchedOn`), лампы (`RoomLight.IsSwitched`) и клавиша с зелёным огоньком в темноте следуют на каждом пире;
роботы видят результат через `LightSource`, тревога по-прежнему зажигает все лампы.
**Решётки в потолке** не задевают ламп (`VentPlan.ClashesLamp`; где места нет — лампа сдвигается, `LevelBuilder.LightSpots`), свой потолок комнаты, лоток и короб коридора их обходят (`ArchitecturePlanner.GratesOf`). **Окна** не ставятся за лестницей (`LevelBuilder.WindowCuts`).
**Лазы закрыты решёткой** — `VentGrille` на `Level/VentFrame`/`HighVentFrame`: только с отвёрткой в руке держать E (`InteractionSettings.VentGrilleTime`) → решётка снята и прислонена к стене, лаз открыт для всех (`OpenKeys.Vent` в `Opened`).
**Табличка «Выход»** — только над проёмами на лестничную клетку и из лобби в коридор (`LevelBuilder.OnEscapeRoute`). **Номера квартир** — на табличке входной двери со стороны площадки (`AutoDoor.Number`, считаются с 1 снизу вверх, `LevelBuilder.FlatNumber`).
**Варианты мебели** — `PropVariants` на префабе: слоты (крючок, полка), в каждом все варианты дочерними объектами; при постройке дома каждый слот по сиду (`LevelRandom.Mix(seed, 0x7A21, индекс)`) показывает один вариант или ничего и красит его из палитры (`Cloth*`, `Book*`). Вешалка-стойка и настенная вешалка — голые модели + одежда на крючках (`Tools/Blender/variants.py`: `coat_stand_bare`, `hall_hooks_bare`, `garment_*`, точки крючков — `variants_contract.json`; `AssetBuilder.HookGarments`: длинное — через крючок, на верхних крючках рейки только кепка); книжные полки — ряды книг `book_*` разных размеров и цветов, по четыре ряда на полку на выбор (`AssetBuilder.ShelfBooks`: высоты полок берутся из меша стеллажа); там, где у стиля своя модель стеллажа (бестиарий), показываются его книги (`PropVariants.Frame/Own`, `VisualStyles.IsOwnStyleMesh`, событие `VisualStyles.GeometryChanged`).
**Порожки** — где в проёме встречаются разные полы (`ArchitecturePlanner.PlanThresholds`, `DecorPiece.Threshold`, материал `StyleKit.Threshold` — тусклый, тёмный, без блеска: незаметная деталь).
**Дверцы шкафов** — две створки рядом на одной высоте открываются от середины (`StorageMath.RightHinged`: правая — на петле справа), кроме мебели из отдельных отделений (`Storage.Individual`: шкафчики раздевалки — все на петлях справа). Часть другого материала на дверце (`TrimDoor{n}`/`AccentDoor{n}`: зеркало на дверце зеркального шкафчика над раковиной, ручки тумбы раковины в туалете) едет с ней (`Storage.Follows`; `meshfix.MOVING` их не двигает). Открывается всё, у чего есть дверцы или ящики (`Items.Storage`, части `BodyDoor{n}`/`BodyDrawer{n}`): шкаф, шкафчики, гардероб, комод, обувница, холодильник, кухонная тумба и мойка, тумба в санузле и раковина с зеркалом в туалете (`MirrorSink`), посудомойка, ТВ-тумба, комоды и тумбы офиса, картотека, стол руководителя, прикроватная тумбочка (`AssetBuilder.Rooms.Opens`, `AssetBuilder.Opening`); у открывающейся мебели нет стилевых моделей бестиария (`build_bestiary.OPENING`) — стиль показывает версию семейства с подвижными частями. Не открываются пока: плита (дверца вниз), микроволновка, остров, стиральная машина.
**Звуки работы руками** — пока держится E: стук клавиш при взломе, тики отмычки у замка, трещотка отвёртки у решётки (`PlayerController.WorkSounds`, клипы `work_*` из `generate_sounds.py`); звучат, только пока прогресс удержания растёт (`PlayerController.m_WorkWas`: прогресс сохраняется, когда E отпущена, и виден при наведении — это не работа); снятый замок двери щёлкает на каждом пире (`AutoDoor`); навесной замок распахнутой двери (её открыл робот) прячется, а не висит в проёме (`AutoDoor.ShowLock`).
**Предмет в руке** — что лежит в слоте руки, видно в руке у всех (`PlayerController.HeldThing`: модель инструмента или кусок лута его цвета; у фонарика и карты — свои), логичным концом вперёд (`ItemCatalog.HeldPoseOf`: импорт зеркалит X моделей Blender — часть с Blender +X у нас на −X; рация стоймя антенной вверх экраном к глазам, топливоискатель за рифлёную ручку лампой вперёд, планшет поперёк экраном к глазам, аптечка защёлками вправо; ключи — головкой в кулаке, бородкой вперёд и ребром вбок, отвёртка — за ручку, жалом вперёд, компактное — на кулаке, банка кофе — стоймя крышкой вверх); владелец видит правую руку, сжатую на предмете (`PlayerController.HeldHand`, модель `poi.py view_grip`), там же, где рука с фонариком. Импорт зеркалит X моделей из Blender: рука с фонариком и рука хвата отзеркалены обратно (`localScale.x = −1`) — правые.
**Звуки людей** — `PeopleSounds`: в газе сначала тяжёлое дыхание, потом смех всё громче и чаще (`PlayerController.GasShare`, реплицирует `PlayerNet`,
клипы `gas_breath`/`gas_laugh_0..2` из `generate_sounds.py`); заложник окликает игрока, который сам подошёл и видит клетку (глазами тела, не через камеру терминала) (реплики `Voices/hostage_*` из
`generate_voices.py`); прыжки и приземления — `AirState` (подъём на мебель и дрожь на краю — не прыжок).
**Фасад без швов** — наружные стены каждого этажа доходят до пола следующего (кромка плиты за ними), над уличной дверью — стена, а не перемычка цвета обрамления,
столбы в стыках стоят точно в концах стен (ключ словаря округлён, позиция — нет).
**Стены у лестничного проёма** — над проёмом в перекрытии плиты нет, поэтому стены нижнего этажа вдоль него достраиваются полосой до пола следующего (`LevelBuilder.StairBands`).
Настенное (`Furnisher.OnWall`: доска, огнетушитель, часы…) висит вплотную к стене (`HungGap`), стоящая у стены мебель — с зазором `WallGap`.
Руль машины поворачивается в сторону поворота, в `CarSettings.SteeringRatio` раз дальше колёс (`CarLook.SteeringWheel`).
**Размер дома** — `BuildingSettings.GridFor(дом, игроки)`: растёт с каждым домом (`BaseRooms` 9 + `RoomsPerHouse` 3,5, до `MaxRooms` 54; комната ≈ 100 м²), мягко — с командой (`AreaByPlayers`); квота — `RunSettings.QuotaByPlayers`,
`RunState.SetPlayers` пересчитывает квоту, когда команда меняется посреди дома. Число игроков — часть `LevelInfo` (все пиры строят один дом).

**Сплеш студии** — `StudioSplash` в префабе меню (юнитевский сплеш выключен, `PlayerSettings.SplashScreen.show`): крыло Zhukovsky Games, раз за запуск,
автопилоту не показывается; на время сплеша музыка меню молчит.

Тестовые хуки собраны в `DevFlags` (сид, уровень, мирный режим, автопилот…) — в обычной игре не используются.

**Рандом — только по сиду.** Один сид забега (`GameManager.RunSeed`, `-seed`) даёт сиды домов (`LevelRandom.LevelSeed`), сид дома — отдельный
поток на каждую цель (`RandomChannel`: планировка, лут, роботы, патрули, устройства, звук) и на каждый объект (`LevelRandom.Next` — n-й робот).
`UnityEngine.Random` в игровом коде не используется (проверяет `LevelRandomTests`), он только засеян на всякий случай; новый бросок — новый канал
или поток объекта, чтобы не сдвигать остальные; соль потока — именованная константа в `Salt` (одна на поток: `LevelRandomTests.SaltsAreDistinct`,
«голая» соль в `Mix` — ошибка теста).

### Настройки (конфиги)

Логика отделена от данных: всё, что крутим при итерациях над игрой, лежит в ScriptableObject-конфигах, а не в коде.

| Ассет | Что внутри |
| --- | --- |
| `Data/Config/Player` | движение и прыжок, выносливость, неуязвимость после ударов, шум шагов, газ, оживление и взлом (время, дальность, длительность) |
| `Data/Config/Run` | квота (доля, рост, потолок), штраф за упавших, тревога, льготный период, когда приходит охотник |
| `Data/Config/Level` | размер здания по домам, веса типов комнат, темнота и мерцание, проходы/двери/лазы, сколько охраны и **кто** с какого дома (ростер), лут |
| `Data/Config/Items` | цвета лута: название, цвет, множитель ценности, частота |
| `Data/Config/Perception` | как роботы и устройства замечают: зрение (дистанция, доля видимого тела, край обзора, за спиной, присед, неподвижность), свет (порог темноты и полного света, насколько темнота прячет), слух (сколько пропускают стена, дверь, перекрытие, мебель; подозрение от шага), вибрации |
| `Data/Config/Audio` | громкость групп (музыка, эмбиент, сирена, UI, шаги, роботы, предметы, петли), дальность слышимости, длина шага, смена музыки |
| `Prefabs/Robots/Robot<Kind>Config` | подвижный робот (`RobotProfile`): глаза, слух, сейсмика, нюх, скорости, лестницы, атака, радиус тревоги, внимание (паузы, затухание подозрения), анимация |
| `Prefabs/Robots/RobotTurretConfig` и др. | устройства: турель, паук, пылесос (`TurretProfile`, `SpiderProfile`, `VacuumProfile`) |
| `Prefabs/Sensors/<Префаб>Config` | лидар, датчик движения, плита, камера, лазер (`LidarProfile`, `MotionSensorProfile`, `PressurePlateProfile`, `CameraProfile`, `LaserGateProfile`) |

- Чистые правила получают **группы настроек** параметром (`MovementSettings`, `StaminaSettings`, `RunSettings`, `BuildingSettings`,
  `SecuritySettings`, `RosterSettings`, `LootSettings`…); без параметра — значения по умолчанию из кода (их же видят тесты).
- Компоненты берут настройки через `Tuning` (`Tuning.Player.Movement`, `Tuning.Level.Security`…), который грузит `Resources/GameConfig`.
- **В префабах роботов и устройств нет чисел** — только ссылки на части модели и на свой конфиг. Конфиг — отдельный файл рядом с префабом
  (`<Префаб>Config.asset`): один правит префаб, другой — числа, конфликтов нет. Общая база — `DetectorProfile` (анимация) → `DeviceProfile`
  (радиус тревоги, остывание, забывание) → конфиг устройства. Проверяют `TuningTests` (конфиг рядом с каждым префабом, в префабах нет числовых полей).
- Сборщик создаёт конфиги один раз и **никогда не перезаписывает** (даже `FORCE=1`). Добавил поле в группу настроек — в старом ассете оно получит значение по умолчанию из кода.
- Уровень строится детерминированно из сида на каждом пире: у хоста и клиентов должен быть один и тот же билд (одни и те же конфиги).

### Визуальные стили

`VisualStyle` (`Data/Styles/*.asset`) — один вид игры: грейдинг на URP-объёме, экранный фильтр `Art/Shaders/StyleScreen.shader`
(пиксели, цел-ступени, «масло», контуры, штриховка, бумага, 1 бит, камкордер, неон — `FullScreenPassRendererFeature` на `PC_Renderer` после пост-обработки),
воздух (эмбиент, дымка, цвет газа) и палитра материалов по видам (`StyleConfig`). `VisualStyles.Apply` меняет только картинку на этом пире: роботы считают
свет по `LightSource` и `VisualStyles.GameplayAmbient`, плотность газа — геймплей и не меняется. F8 открывает выбор стиля, по умолчанию — `S16_Derelict` «Тихий комплекс» (`StyleConfig.Default`), `-style <имя>` включает при запуске,
сценарий `styles` снимает одни и те же замороженные кадры во всех стилях. Ассеты стилей создаются один раз из `AssetBuilder.Styles`
и дальше правятся руками; `REBUILD_STYLES=1 Tools/dev.sh assets` пересоздаёт их из кода.
Стиль меняет и форму мира (`StyleGeometry`): **семейство моделей** `Family` — лоуполи / игрушки / PS1 / чистые / реализм: свой вариант каждой модели
(меши подменяются по имени, коллайдеры и пивоты прежние), своя версия каждого материала (`MaterialVariants`: плоский, глянцевый пластик, пиксельный,
PBR из сканов) и свой набор архитектуры (`StyleConfig.KitFor`); реализм вдобавок заменяет мебель CC0-сканами целиком (`PropReplacement`: наша модель
прячется, кроме светящихся Eye-частей, скан вписан в её габариты). Модели одного стиля (`StyleConfig.StyleModels`: все роботы, датчики и мебель стиля из `Styles/PerStyle/<стиль>`, игроки по отделам —
`Variant` = слот) сильнее семейства. **Бестиарий** (`Tools/Blender/bestiary/`, бриф и контракт — `bestiary/STYLES.md`): у каждого стиля
свой файл `styles/<стиль>.py` — язык форм (`Lang` + словарь мебели `furniture.py`) и свои конструкции роботов и предметов поверх общих
(`designs.py`, `props.py`); сборка `build_bestiary.py` проверяет контракт оригинала (те же части, центры колёс и винтов, суставы ног,
габариты мебели ±6 см), смотреть — `bestiary/review_style.sh`.
Игроки — айтишники четырёх отделов (`PlayerLooks`: бэкенд, тестирование, сисадмины, дизайн; отдел — от класса, `PlayerLooks.DepartmentOfClass`: монтажник — сисадмин, хакер — бэкенд, связист — тестировщик, картограф — дизайнер; до выбора — `DefaultClass` слота; цвет команды и лицо — от слота; `VisualStyles.SetVariant`
выбирает модель отдела). Поверх модели во всех стилях показывается настоящий человек (ступни после анимации ставятся на пол под ним: `HumanRig.PlantFeet`, `HumanMath.PlantShift` — капсула висит над полом на толщину кожи) со скелетом и анимациями своего отдела (в машине — поза `Sit`, скорость машины не идёт в ходьбу: `HumanMath.WalkSpeed`) (`VisualStyles.PlayerRigFor(слот)`: свой `StyleGeometry.PlayerRig` стиля, если он задан (сейчас — ни у одного, и у `S16_Derelict` тоже отделы), иначе `StyleConfig.PlayerHumans` — `Tools/Blender/human.py -- <out> backend|qa|sysadmin|designer [preview=<png>] [static=<fbx>] [draft]` → `Humans/human_<отдел>.fbx` и карты `Art/Textures/Humans/human_<отдел>_{skin,outfit}_{base,normal,mask}.png`, префабы `Player/HumanRig_<Отдел>`). **Четыре разных человека** (`Tools/Blender/humanlib/`): тело — анатомия полем расстояний (`body.py`: таз, грудная клетка, плечевой пояс, мышцы конечностей; `Build` — сложение человека, суставы у всех одни, поэтому дубли общие, а рост — масштаб модели в префабе, `AssetBuilder.HumanHeight`; голова на теле крупнее меток `face.py` в `HEAD_S` раз), голова — череп, челюсть, нос, губы и веки по меркам настоящей головы, а `Face` делает из неё человека (`face.py`, `PEOPLE`: бэкенд — худое длинное лицо, копна тёмных волос, щетина, толстовка и наушники; тестировщица — круглое лицо, каштановое каре, веснушки, очки, рубашка и бейдж; сисадмин — широкое лицо, стрижка под машинку и короткая борода (`face.beard`: по челюсти, сходит на нет по линии щеки и шеи, губы открыты), жилет поверх рубашки с закатанными рукавами, налобный фонарь, моток кабеля; дизайнер — лицо сердечком, чёрные волосы в узел под беретом, шарф, джемпер), одежда — тело, отпущенное на припуск ткани, с немногими складками там, где ткань ломается (`clothes.py`; у каждого свой крой брюк: прямые джинсы, узкие, карго с карманами, широкие укороченные), кисти и обувь — тоже поля. Каждый кусок: плотная сетка (`sdf.mesh`, surface nets, вершины досажены на поверхность) → раскраска по вершинам → бюджет треугольников (всего ~15 тыс.) → развёртка → **запекание** плотной скульптуры на игровую сетку (`build.bake`: нормали в касательном пространстве, цвет, окклюзия, гладкость) → проверка топологии (`build.audit`, строки `TOPO`: неманифолдные рёбра, дыры, вырожденные грани, веса). Поза покоя — А-поза; дубли (`anim.py`, позы пишутся в осях фигуры — вперёд / в сторону / вокруг своей оси): Idle (перенос веса с ноги на ногу, дыхание, взгляд в сторону), Walk, Run, CrouchIdle, CrouchWalk, Down, Sit и новые **Climb** (лестница: рука и противоположная нога), **Terminal** (печатает, склонившись над пультом), **Work** (руки на замке / решётке / роботе). Чем занято тело — `PlayerController.Pose` (`BodyPose`: лестница → терминал, диск сейфа, пульт хранилища → растущий прогресс удержания E, `HumanMath.PoseOf`; владелец решает, `PlayerNet` реплицирует байтом), `HumanRig` включает дубль, Climb идёт со скоростью подъёма (вниз — задом наперёд, на месте — стоит: `HumanMath.ClimbRate`). Части (рендереры): `BodyClothes` — одежда отдела (красится цветом отдела), `AccentHead` — кожа (тон игрока), `AccentBand` — повязка и шарф / шнурок / кабель (цвет команды), `EyeDetails` — наушники, очки, ремень (тёмные; есть не у всех), `Sclera` — глаза, `Outfit` — брюки, обувь, волосы и всё своего цвета; материалов три на человека (`AssetBuilder.HumanMaps`: кожа, одежда, глаза — URP Lit с запечёнными картами, гладкость по текселям в маске). Цвет команды — только `Accent` (рюкзак, повязка, шнурок/шарф), одежда — цвет отдела, лицо — кожа, детали головы — тёмные. Новые объекты одеваются через `VisualStyles.Dress` при спавне. **Архитектура** — `LevelBuilder` записывает
оболочку дома (`ActiveLevel.Shell`: грани стен, полы, проёмы, окна), чистый `ArchitecturePlanner` раскладывает по ней детали `StyleKit`
(плинтусы, панели, карнизы, наличники/пилястры/арки у дверей, кабельные лотки под потолком в обход ламп и лестничных проёмов (балок и кессонов нет: `CeilingKind` — только `None`/`Trays`), трубы, световые щели,
отделка пола и стен в обход окон), `StyleDecor` ставит их под корень уровня. Декор без коллайдеров и теней — геймплей и свет не меняются (`ArchitectureTests`).
**Рендер большого дома** — `MeshBatcher` склеивает статичную геометрию в один меш на клетку комнаты и этаж (вместе с мебелью — неизменная оболочка дома:
стены, столбы, плиты, крыша, рамы окон, лазов и щелей, таблички «Выход», корпуса ламп — `PropBatches.IsShell`; static batching оставляет каждый рендерер,
а их число съедало кадр на сортировке и отсечении теней; замер — `perf`: `DEV=1 Tools/dev.sh player` даёт разбивку по маркерам профайлера, `PerfProfile`) (сабмеш на материал, тайлинг отделки — в UV):
декор стиля (`StyleDecor`, отдельных объектов у деталей нет) и мебель (`PropBatches`: объекты мебели с коллайдерами и светом остаются, их рендереры —
`forceRenderingOff`, перед сменой стиля склейка снимается и собирается заново). Стены, плиты, лестницы и окна — static batching (`LevelBuilder.CombineStatic`).
**Лестницы** — коллайдеры-кубы `StairGeometry` (навмеш и шаги не меняются; `Stair.Flip` — марши вдоль другой стены угла, зеркальный вариант,
`StairGeometry.Placement/ToWorld`) невидимы, видна одна из четырёх конструкций (`StairStyle`: бетон с парапетом,
дерево с балясинами, открытая сталь с сеткой, белый камень со стеклом), меши генерирует `AssetBuilder.Stairs` (`Art/Models/Stairs`); выбор — `StairStyles.Pick`
по семейству стиля и сиду дома или вид, который просит стиль (`StyleGeometry.Stairs`; у стиля по умолчанию — бетон), `StairLook` переключает при F8
и одевает лестницу в архитектуру стиля: ступени — отделка пола, тело — отделка стен, балясины и поручни — его молдинг (`StairStyles.Parts`: роль каждого слота материала). Объём, который заняли коллайдеры под площадкой и верхним маршем, всегда закрыт панелью (торец под верхом марша — сплошной от последней ступени);
сценарий `stairs` ставит лестницу одну во дворе и снимает каждый вид со всех сторон.
**Лазы** — короб вентиляции: порог `LevelMetrics.VentSill` (кусок стены), над ним `VentHeight`, облицовка, фланцы и снятая решётка у стены (префаб `Level/VentFrame`).
Часть лазов — под потолком (`PlanOpening.High`, `Doorway.High`; `LevelGenerator.RaiseVents`, свой поток рандома, `BuildingSettings.HighVentChance`): порог `HighVentSill`,
под ним с обеих сторон обычный письменный стол у стены (`Furnisher.PlaceVentDesks`, не убирается проверкой проходимости) — запрыгнуть на стол, пригнуться, шагнуть в короб;
ставится, только где стол и место перед ним влезают в комнату (не коридор), не задевают лестницы и чужие проёмы (`LevelGenerator.DesksFit`). Проверка — сценарий `vent`.
**Перила галерей** — невидимый коллайдер `Level/RailingBlock` и пролёты `Level/RailingBay` в тех же четырёх исполнениях, что лестницы (`StairLook`).
**Декор у проёмов и стен**: отделка стены заходит в откосы и на потолок проёма (`ArchitecturePlanner.PlanReveals`), пазухи арки идут до середины толщины стены;
плинтус, панели и рейка обрываются там, где начинается обрамление двери, фланцы лаза или рамка щели (`ArchitecturePlanner.ReachOf`/`TrimSpans`), пилястры — до низа карниза,
карниз идёт над капителью; на лазах и щелях дверного обрамления нет (`WallOpening.Squeeze`); пазухи арки — часть стены (`StyleDecor.IsWallPart`): отделка стены
с её же раскладкой текстуры (`MeshBatcher.WallProjection`, откосы тоже по миру — `ArchitecturePlanner.Across`), в плоскости отделки, без фасок и сглаживания
(`style_kit.py`, `export(..., wall=)`); сценарий `doors` снимает стыки крупно в пяти стилях (кадры `*_debug_normalink` — усиленный контур по нормалям);
обшивка, карниз, трубы ставятся только на куски стены нужной высоты (под лазом — нет); стыки скошенных и дуговых стен — повёрнутый столб и декор ровно до стыка
(`WallFace.JointA/B`), грани таких стен находятся сквозь «карман» (`LevelBuilder.InnerNormals`); столб посреди прямой стены (Т-стык) две соседние грани
делят пополам (`WallFace.StraightA/B`, `LevelBuilder.RunsOn`) — панели и карниз не кладутся дважды; кусок дуги, идущий внутри прямой стены у точки касания,
не строится (`HousePlan.ShapedWalls`); пол комнаты заходит в её «карман» за скошенной стеной (`ArchitecturePlanner.PlanFloors`). Проверяет `SpaceRulesTests`.
Держатель склеенного меша — `StyleConfig.Batch`.
**Воксельный стиль `S11_Noir`** («Нуар-детектив», по мотивам Shadows of Doubt): роботы, датчики, мебель и игроки этого стиля пересобраны из кубиков 4 см
(`Lang.voxel`, `Tools/Blender/voxel.py` — Remesh Blocks по каждой части, мельче для тонких деталей, светящиеся части — на сетке вдвое мельче), без пиксельного фильтра.
**Стиль Саши `S16_Derelict`** «Тихий комплекс» (влит из `feature_visual_style_11_prototype`): настроение заброшенного комплекса (грейдинг, зерно, сырой воздух,
мёртвый город за окнами, свет стиля только для глаз — `StyleWorld.LightScale`/`LampGlow`) без деградации: семейство `Realistic`, у каждого типа комнаты своя отделка
(`AssetBuilder.RealisticRoomLooks/RealisticRoomFloors`: кухня — кафель и шахматный пол, туалет — плитка до 2 м, офис — ковролин, библиотека — тёмные панели и ёлочка…),
больница, мастерская и жилой дом выглядят собой; своя мебель — исследовательский комплекс конца 80-х (трубчатые рамы-салазки, толстый ламинат, стальные шкафы, глубокие ЭЛТ-мониторы, эмалевые абажуры: `bestiary/styles/S16_Derelict.py`, `PerStyle/S16_Derelict`). Сам заброшенный вид — у дома своего типа (ниже). Семейство моделей `Derelict` (`Tools/Blender/derelict_*.py`: мебель, PBR) собирается,
но ни один стиль его сейчас не берёт; люди отделов, как во всех стилях (учёный в халате `Player/HumanRig_Scientist` собирается, но не показывается; `Tools/Blender/human.py`, `HumanRig`).
`REBUILD_STYLES=S16_Derelict,…` пересоздаёт из кода только названные стили. Сценарий `perf` пишет, сколько рендереров рисуется и чьи они.
**Заброшенный комплекс** (`HouseKind.Abandoned`, `Location.Abandoned`; примерно каждый одиннадцатый дом — из доли офисов) — брошенная научная станция: своя планировка
на «пластине» больницы (`BuildingSettings.Station`, `PlanGenerator.Station.cs`: коридор-хребет, спереди лаборатории, пульт и кабинеты, сзади серверные, склады,
раздевалка и столовая; выше — лаборатории спереди, жилые комнаты `Bedroom`, гостиная, кухня и туалет сзади; на верхнем этаже спереди — серверные вокруг пульта),
типы комнат не переделываются, темнее (`DarkChance` +0,25), двери стальные, у лабораторий и серверных — стеклянные раздвижные; уникального предмета нет.
**Вид в любом стиле** — `DerelictLook.For`: стальные панели, рифлёные плиты, вентиляционные панели потолка, ржавые трубы и лотки, пятна, потёки, плесень и плакаты
(`StyleKit.Derelict*`, `FloorDecay/WallDecay/Posters` — из материалов `Dr_*`/`Dc_*` Саши, одинаковые у всех семейств), кафельный пояс в мокрых комнатах. Проверяет `AbandonedTests`.

### Ассеты из кода

`Editor/AssetBuilder*.cs` создаёт материалы, спрайты, все префабы, `LevelArt`, список сетевых префабов и сцену.
Меню **Stealth → Build Missing Assets** дособирает только отсутствующее (ручные правки префабов сохраняются),
**Rebuild All Assets** (или `FORCE=1`) перезаписывает всё. Пути не меняются, поэтому GUID и сетевые хэши стабильны.
Если меняешь структуру префаба в коде — пересобери с `FORCE=1` и закоммить изменённые префабы вместе с кодом.
Сцена в режиме «только недостающее» не пересобирается: новый экран или объект сцены появится после `FORCE=1`.
Сглаживание: SMAA камеры работает внутри постобработки, до стилевого фильтра, поэтому после `StyleScreen` идёт свой FXAA-проход
(`Art/Shaders/ScreenFxaa.shader`, фича `ScreenFxaa` на `PC_Renderer`; стили из пикселей и 1 бит он пропускает).
Рендер: URP Forward+, луна (направленный свет с тенями) и тёмный эмбиент задаются в `AssetBuilder.Scene`, постобработка — `Data/PostProcess.asset`
(bloom, тонмаппинг, виньетка; пересоздаётся только с `FORCE=1`), атласы теней — `Assets/Settings/PC_RPAsset.asset`.
Газ снаружи — поле `GasView`/`GasCommon.hlsl`: полный слой до `StyleWorld.GasLayer` (8 м: нижние этажи соседей в нём), под землёй его нет; контуры стилевого фильтра (`StyleScreen.shader`) гаснут в нём вместе с тем, что обводят (`GasVeil`). Туман задаётся в рантайме (газ снаружи, дымка внутри), поэтому вырезание вариантов тумана — ручное (`GraphicsSettings`: Exp2 оставлен) и туман включён
в самой сцене: иначе билд выбрасывает шейдеры с туманом и газа не видно. Материалы со свечением — `globalIlluminationFlags = BakedEmissive`
(с `None` URP при импорте выключает `_EMISSION`). GPU Resident Drawer выключен: с ним плеер падал на старте уровня.
Graphics jobs выключены (`BuildPlayerWindows`): с ними плеер изредка падал на старте уровня внутри движка (`ShaderVariantMonitor::FrameFinished`, гонка
потоков при загрузке новых вариантов шейдеров; стек расшифрован по PDB из `PlaybackEngines/windowsstandalonesupport/Variations`).
Модели — `Tools/Blender/*.py` (после экспорта откати неизменённые FBX: `git diff --name-only Assets/_Game/Art/Models | xargs git checkout --`;
перед экспортом каждая модель проходит `meshfix.fix_objects`, проверка — `review.py` / `audit_models.py`, как моделировать — скилл [`blender-modeling`](.claude/skills/blender-modeling/SKILL.md)),
звуки — `python Tools/Audio/generate_sounds.py` (детерминированно, с сидом; мотор и гудок машины — CC0-записи: `python Tools/Audio/import_cc0_sounds.py`, список — `Art/Audio/CREDITS.md`), голос роботов — `python Tools/Audio/generate_voices.py` (Windows, ru-RU голос).

## Правила кода (коротко; подробно — скилл [`stealth-code`](.claude/skills/stealth-code/SKILL.md))

- В рантайме **не собирать объекты кодом** (`new GameObject` + `AddComponent`, `CreatePrimitive`, процедурные текстуры): только `Instantiate` префабов, на которые ссылаются поля/`LevelArt`.
- Один `MonoBehaviour` — один файл с тем же именем (иначе Unity теряет скрипт в префабах).
- Логику, которую можно проверить без движка, выносить в чистый класс и покрывать тестом. Время и случайность — параметрами.
- Число, которое будут подкручивать при балансе, — в группу настроек конфига (с `[Tooltip]`), а не константой в коде. Константы — только для геометрии и того, что не тюнится.
- Сервер решает, клиенты показывают. В игровом коде нет `NetworkVariable`/`Rpc`/`NetworkManager`: состояние — обычные поля, запросы — через link-интерфейсы,
  проверка — в серверном обработчике игрового компонента. Netcode — только в `Scripts/Net`.
- Приватные поля `m_Name`, статические `s_Name`, константы и публичное — `PascalCase`. Комментарии и идентификаторы — по-английски, тексты для игрока — ключами из таблиц строк (русский + английский).
- Текстов на экране — минимум: игрок уже прошёл обучение. Никаких объяснений по центру; клавиши — в стопке подсказок справа снизу, интерактивность — иконкой прицела.
- Найденный баг — чинить и, если можно, закрыть тестом.

## Точки стыковки для коллеги

- Войс-чат — сделан, свой стек ([Docs/VoiceChat.md](Docs/VoiceChat.md), там же исследование и дорожная карта, в т.ч. покупка Dissonance):
  плагин `Plugins/x86_64/StealthVoice.dll` (Opus + RNNoise от Xiph, `Tools/Voice/build_native.sh`), чистые правила `Scripts/Audio/Voice/*`
  (`VoiceGate`, `VoiceGain`, `VoiceJitter`, `VoiceAcoustics`, `VoiceNoise`, `VoicePacket`), микрофон — `VoiceCapture` на префабе AudioDirector,
  голос в мире — `VoicePlayback` (ребёнок `Voice` префаба игрока: плоский клип × голос в `OnAudioFilterRead`, потом Low-pass/Echo/Reverb),
  маршрут — `VoiceHub` (сервер делает шум `NoiseSystem.EmitServer` по сырому уровню и пересылает), сеть — `Net/VoiceNet` (named messages, Unreliable);
  настройки — `Data/Config/Audio` → `Voice`, выбор игрока — `VoicePrefs` (вторая колонка страницы настроек), кнопка — `GameAction.PushToTalk` (CapsLock).
- Сеть: всё сетевое — в `Scripts/Net` (сборка `Stealth.Net`), игровой код от Netcode не зависит; как добавить сетевой объект — в скилле `stealth-code`.
- Предметы: `ItemCatalog` (форма + цвет), расстановка — `LootPlanner`, в мире — `WorldItem`, инвентарь — `PlayerInventory`.
- Мета-прогрессия: итог забега `GameManager.RunTotal`, конец забега `GameManager.Phase == RunPhase.Failed`.
- Новый робот или датчик: чек-лист в скилле `stealth-code`.
