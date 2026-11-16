# CLAUDE.md — «Айтишники: вылазка»

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
Tools/dev.sh cycle              # ОДИН запуск Unity: недостающие ассеты + EditMode-тесты + development-билд (только скрипты, если менялся лишь код)
NOASSETS=1 NOTESTS=1 Tools/dev.sh cycle  # шаги можно пропускать (NOPLAYER=1 — без билда, FULL=1 — полный билд); FILTER/FORCE/REBUILD как ниже
Tools/dev.sh tests              # компиляция + все EditMode-тесты (итог и упавшие — в конце вывода); в открытом редакторе — MCP run_tests
Tools/dev.sh assets             # дособрать недостающие ассеты (существующие префабы не трогает)
FORCE=1 Tools/dev.sh assets     # пересобрать ВСЕ ассеты из кода (ручные правки в префабах пропадут)
REBUILD_UI=1 Tools/dev.sh assets  # пересобрать только UI-префабы (HUD, меню, инвентарь, элементы)
REBUILD=RobotGuard,Safe Tools/dev.sh assets  # пересобрать только эти префабы (по имени файла), GUID сохраняются
FILTER=HousePlanTests Tools/dev.sh tests  # только эти тесты
PLANS="7:3,42:5" Tools/dev.sh plans       # картинки планов домов (сид:дом) в Logs/plans/
Tools/dev.sh player             # релизный Windows-билд в Builds/Windows/StealthPrototype.exe (cycle собирает development)
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
Prefabs/Vehicles машина (Car, сетевой), её модель для сцены поездки, кусок груза в кузове · Prefabs/Level/Sewer — набор канализации
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
   `RunMap` (маршрут), `RouteVotes`, `SafeLock`, `LegMath`, `StairStyles`, `HouseFeatures`, `Crafting`, `Cargo`, `CarPhysics`, `SewerPlan`.
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

На мобильных роботах `SensorRig` показывает насадки-датчики (глаз-камера, щупы в пол, нос; микрофонов-стоек нет — сзади они читались как непонятные диски) по `Senses` из конфига и садит их на тело
текущего стиля лучом по треугольникам корпуса (`MeshRay`; микрофоны на верх, щупы к передку у пола, глаз и нос на лицо) — при каждой смене мешей.
Восприятие плавное: зрение — доля видимого тела (`Perception.Exposure`, 7 лучей), дистанция, место в конусе, присед, неподвижность, свет
(`SensorMath.VisionFactor`); свет на игроке — `Perception.IlluminationOf`: сумма всех `LightSource` (потолочные лампы `RoomLight`, настольные/напольные
лампы, таблички «Выход», автоматы, фонари, луна) по формуле затухания URP, с конусом и тенью-лучом, плюс эмбиент → `SensorMath.LightFactor`; тот же расчёт
кормит индикатор заметности в HUD. Новый источник света, важный для стелса, — `Light` + компонент `LightSource` в префабе;
**свет не должен проходить сквозь стены**: эмбиента нет (`LightMath.NightAmbient` = чёрный, туман внутри тёмный), стены/плиты/крыша отбрасывают тени
с двух сторон, у ламп полные тени (сила 1, малое смещение), а лампы без теней — только маленькие споты, не достающие до соседей (`LightLeakTests`);
сценарий `darkbox` строит закрытую коробку в освещённом лобби и проверяет, что кадр внутри чёрный; у роботов и устройств статусный свет — спот с тенями;
**бюджет теней** — `LightBudget` на LevelRoot рисует только ближайшие к камере лампы, что влезают в атлас теней (иначе URP ужимает карты, и
плеер падал); геймплей от этого не зависит. Материалы: либо освещаются, либо светятся по-настоящему (HDR ≥ 1, bloom) — проверяет `LightLeakTests`; слух — громкость шага у робота с затуханием через стены/двери/перекрытия (`Perception.SoundTransmission`,
компонент `SoundBlocker` на префабах стен, плит и дверей). Полная шкала → `RobotState.Alerted` («Обнаружен нарушитель», `Traits.WarningTime`)
→ `Chase` («Веду преследование»). Голос — `AudioDirector` по смене состояния (`AudioRules.Voice`), шкала над роботом — `GameHUD` (`SuspicionMarker`).
Удар сбивает с ног (`HealthState.TryHit` → `Downed`): лежачих роботы не видят (`IIntruder.IsDown`), робот говорит «Нарушитель устранён»
и возвращается к работе; с HP > 0 игрок встаёт сам (`CanGetUp`, Пробел → `IPlayerLink.SendGetUp`), с 0 — только союзник; провал забега — когда все `KnockedOut`.
Потолочный пылесос (`CeilingVacuum.Sweep`, буфер лучей на 64 попадания — проверяет `VacuumFanTests` на настоящем префабе) — вертикальный лазерный веер от линзы робота вниз: `ScanLine.FanRays` лучей на половину, каждый останавливается на первом,
во что упёрся (стена, мебель, дверь, пол; игроки не заслоняют), меш веера собирается из этих точек каждый кадр, той же формой сервер ловит
игрока (`ScanLine.FanTouches`); линии на полу нет. Касание любой частью тела — тревога, перепрыгнуть нельзя. Паук время от времени переползает через потолок на
противоположную стену (`SpiderProfile.CeilingEvery`; ступни ног находят и крышу верхнего этажа).
Паук без глаз: его комната перетянута паутиной, как простой лабиринт (`SpiderWebs`, каждый пир натягивает те же нити; префаб
`Robots/SpiderStrand` — кусок паутины 1 × 0,5 м, текстура `Tools/Art/generate_web.py`, куски встык вдоль нити), коснулся — паук бежит по стенам к тебе и прыгает. Охранник на точке патруля стоит в три фазы, видные издалека
(`RobotBrain.PausePhase`: замер — оглядывается — разворачивается к следующей точке). Сейсмик — волны (`SeismicSense.Wave*`, `PulseStomp` → кольцо `StompRipple` и звук), подметальщик — лидар у пола (`RobotLidar`),
рабочие везут лут (`DetectorSnapshot.Cargo`, при взломе падает на пол). `RobotRoster.Newcomers` — новый вид гарантированно есть в доме, где он появляется.
`RobotAnimator`: наклоны в разгоне и повороте, испуг при «Обнаружен», обмякание при взломе, удар сейсмика, глаз-камера следит за замеченным игроком.
Настройки робота — в префабе: `RobotController.Senses` (`RobotSenses`: глаза, слух, сейсмика, нюх, сортировщик) и `Traits` (`RobotTraits`: скорости, лестницы, атака, тревога).
Цвета статусов — `SecurityPalette`. Описание для игрока — `RobotInfo`. Где и когда появляется — `RobotRoster`.
**Охотник** без своих датчиков (`RobotSenses.FollowsSightings`): «спит» там, где вошёл (`RobotBrain.Asleep`: не ходит, касание, взлом,
шум и тревоги его не будят), пока любой другой детектор не заметит игрока (`RobotController.Relayed`) — тогда бежит к нему, быстрый; потерял — засыпает снова. **Сторож** (`RobotKind.Warden`) не в ростере — его ставит `HouseFeatures.Wardens`.
**Звук роботов**: у каждого вида свой мотор (`AssetBuilder.MotorFor`) и высота голоса (`AudioRules.VoicePitch`); шаги шагающих — по
касанию ступни (`ProceduralLegs.FootDown` → `AudioDirector.OnFootDown`: охранник, ищейка, паук — свои клипы); всё, что звучит через
перекрытия, глохнет (`AudioDirector.Muffle` → `AudioRules.FloorFactor`, `AudioMix.ThroughFloor`; петли `LoopSound` тоже).
Громкость из настроек применяется сразу (вес кроссфейда отдельно от громкости), фон исследования тихий (`AudioMix.ExploreMusic`),
двери — `AudioMix.Doors`.

### Уровень

`LevelGenerator.Generate(seed, level)` — чистая генерация планировки (`LevelLayout`). `ActiveLevel.Load(layout, art)` строит сцену через `LevelBuilder`
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
Мебель знает про окна и про проходимость: высокое (`Furnisher.Tall`) и висящее на стене (`OnWall`) не встаёт перед окном (`WindowStrips`; в туалетах,
раздевалках и хранилище окон нет — `LevelBuilder.WindowCuts`), одного вида в комнате не больше `Furnisher.CapOf` (один кулер, одни часы), каждая группа
при постановке проверяется той же сеткой, что и `EnsureWalkable` (`StaysWalkable`: середина комнаты, её двери и лестницы остаются связаны) — проверка
после расстановки почти ничего не выбрасывает; туалет всегда пробует кабинки (или писсуары) и раковины (`Anchor`), полупустая комната добирает группы
(`Sparse`); в переговорных и туалетах середина — для мебели, патруль стоит у двери (`LevelGenerator.PatrolSpot`, `Furnisher.MiddleIsFurniture`).
Мебель — `Furnisher`: острова с проходами 1,25 м (их примерно в полтора раза больше, чем «достаточно»: дом обыскивают), пристенные группы с дорожкой
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
  и вызывает серверные обработчики игрового компонента (`OnReviveRequest`, `PickupServer`, `FinishLevelServer`…). Без двойника (тесты, офлайн) запросы выполняются сразу.
- `Session` — фасад сессии: `IsServer`, `ServerTime`, `Spawn`/`Despawn` (сетевые префабы), `Launcher` (меню: хост, подключение, Steam).
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
не видят, пинг и метка идут из вида камеры (`TerminalViewPose`). Карта у всех повёрнута одинаково (север вверху, `MapView`).
Выносливость выключена (`StaminaSettings.Enabled`). Ноги игроков — отдельные части `BodyLegL/BodyLegR` (поворот у бедра `generate_models.LEG_PIVOT`)
качаются в шаг (`PlayerController.SwingLegs`, `PlayerRules.LegSwing`), сидя в машине — согнуты. Оставленный лежать в доме возвращается с пустым рюкзаком.
**Чит-панель (F4)** — `CheatPanel` в HUD: выключить всех роботов, неуязвимость (и газ не берёт), невидимость для роботов (`PlayerController.Invisible` входит в `IsDown`; ни шума, ни следа),
квота, телепорт к люку, вся карта; выполняет сервер (`IPlayerLink.SendCheat` → `PlayerController.CheatServer`). Сценарий `cheats`.
**Карта, пинги, метки.** `ExplorationMap` — что команда видела (клетки плана 0,5 м, открываются веером лучей от глаз: луч останавливается на стене,
проходит в проём двери — мимо двери открывается клин комнаты, который видно сквозь неё; `HousePlan.SightShare`; сетка выходит на `Margin` = 8 м за дом —
снаружи открывается двор в радиусе `TeamSettings.OutsideRevealRadius`, на карте он — `MapPart.Ground`);
сервер открывает вокруг всех игроков, `GameManagerNet` реплицирует биты (`NetworkList<ulong>`, [0] = сид дома), клиент ещё и сам вокруг себя.
`MapSketch` превращает видимое в прямоугольники (пол, стены с проёмами, лестницы, мебель из `LevelShell.Props`, люк), `MapView` рисует
мини-карту и большую карту. Пинг и метка робота — `IPlayerLink.SendPing/SendMark` → `GameManager.CalloutServer` → `IGameLink.BroadcastPing`
→ `PingBoard` на каждом пире; помеченный робот — `DetectorSnapshot.MarkedUntil`, его модели на слое `Marked` (8), фича `MarkedXray` на
`PC_Renderer` рисуют их сквозь стены одним силуэтом (`Art/Shaders/MarkedXray.shader`: маска в стенсил → контур снаружи маски → заливка
ровно один раз на пиксель; три RenderObjects-фичи, проходы 0–2). Настройки — `PlayerConfig.Team`. Взлом и вскрытие — только без стены
между руками и целью (`Perception.ReachClear`, `IHackable.ReachableFrom`). **Взлом навсегда**: робот или устройство, взломанные руками или
с терминала (`GameManager.TeamHackServer`, `GameManager.Hacks.cs`), выключены, пока команда держит не больше `InteractionSettings.MaxHacked` (2);
взлом сверх лимита будит первого (`Detector.WakeServer`); занятые слоты — ключи `HackSlotKey` в `Opened`, в HUD — значки слева снизу (`GameHUD.HackSlots`).
Выключенный робот осел — его капсула идёт за телом до пола (`RobotAnimator.FitCapsule`). **Переподключение** — `GameManager.Reconnect.cs`:
кто вышел (`PlayerController.Identity` — Steam id или id компьютера, `ISessionLauncher.LocalPlayerId`), вернётся с рюкзаком туда, где стоял (тот же дом),
иначе — к хосту (`IPlayerLink.PlaceOwner` → `PlayerController.PlaceFromServer`). Выбывший видит рюкзак того, за кем смотрит (`InventoryUI`).

**Между домами — канализация и машина.** `RunPhase`: `Playing` (дом) → `Sewer` → `Playing` (следующий дом) или `Failed`. В доме **два люка**:
вход (`LevelLayout.HatchPosition`, лобби — команда начинает дом сидя в нём, `PlayerController.EnterHatchServer(0)`) и выход
(`ExitHatchPosition`, комната первого этажа дальше всех от входа, `LevelGenerator.ExitRoom`); `PlayerController.HatchIndex` (0/1, реплицирует
`PlayerNet`). Уйти — все в люках (в любом), и хотя бы один — в люке **выхода** (`RunState.TeamInHatch`) → отсчёт `LeaveAt` → `GoToSewerServer`. У каждого люка свой склад:
`GameManager.Stash` (вход: склад, куда при старте дома выгружается кузов (`UnloadCarServer`), и горловина бака — на его доске зона «сжечь в топливо»,
`StashBoard.SellZone` → `PlayerInventory.Sell*Server`/`DepositServer`; инструменты не горят) и `ExitStash` (выход: уезжает в кузов, `LoadCarServer`) — `GameManager.StashAt(позиция)`.
Вещь, брошенная из рук на шредер машины или во входной люк, — тоже топливо (`PlayerInventory.DepositServer`, `GameManager.CanBurnAt`); вещи из кузова
тащатся мышью в рюкзак (`IInventoryLink.RequestTakeCargo`). Продажи нет: добыча — **топливо**. `RunState.Collected` — бак
(живёт весь забег), `Quota` — сколько сожжёт поездка из этого дома (первая бесплатна); `RunState.Advance` списывает её, `Strand` — забег
окончен, если топлива не хватит даже со всей добычей (`CanStillGo`, предупреждение `RunSettings.StrandedGrace`).
Канализацию каждый пир собирает сам: `SewerPlan.Make(RunSeed, глубина)` (чистый: заглушка, участок машины под люком входа, ворота, участок
спуска из люка выхода, зал с тремя туннелями; число и варианты сегментов, лампы, их цвет, мусор — от сида) → `SewerLocation.Build` из кусков
набора `Tools/Blender/sewer.py` (`-- … kit`: `sewer_seg_0..3` + `_cut`, `sewer_hub`, `sewer_branch`, `sewer_plug`, `sewer_gate_*`, `sewer_lever`, `sewer_cap`;
у зала пять проёмов — три в торце и по одному в длинных стенах (`SewerPlan.Mouths`); три туннеля занимают случайные из них (`SewerPlan.TunnelMouths`),
остальные заложены кирпичом (`Sewer/Plug`), туннель без пути на этот раз — тоже кладкой (`SewerBranch.Plug`); туннели 110 м, зона выбора пути до
`SewerLocation.VoteTo` = 90 м, стена — `StopAt` = 95 м: можно уехать в туннель, пока идёт отсчёт;
префабы `Prefabs/Level/Sewer/*`, `AssetBuilder.Sewer`). Дорога — решётка (`SewerGrate`, alpha-clip, `Art/Textures/Sewer/grate.png`) над водой.
Ворота (`SewerGate`, рычаг `GateLever` на стороне спуска) — `GameManager.GateKey` в `Opened`; в начале забега открыты.
**Машина** — `Vehicles/Car` (сетевой префаб; двойник `CarNet` отдаёт владение водителю: owner-authoritative `NetworkTransform`):
4 места (`CarSeat`, `Car.SitServer`, игрок в машине — `PlayerController.SeatIndex`, `PlayerMotor.Seated`; руль слева — `CarSettings.LeftHandDrive`:
модель из Blender праворульная, левый руль — она же в зеркале, `CarLook.Mirror`, места водителя и пассажира меняются), езда — чистый `CarPhysics`
(велосипедная модель, `RunConfig.Car` → `CarSettings`), упор в стены `BoxCast`, подвеска/руль/колёса/дробилка анимирует `Car`, звук мотора
по оборотам, старт/стоп, гудок (`GameAction.Horn`, `ICarLink`). Бак-приёмник на капоте `CarHopper` (E: лут рюкзака, с пустым — весь кузов →
`FeedServer`). Кузов без ячеек — `Cargo` (куча: `Heights`, упаковка `CargoItem.Pack` в `NetworkList<ulong>`), брошенное над кузовом
ложится туда (`GameManager.DropOnCarServer`), взять — E по `CargoPiece`. Выбор пути — машина в зоне открытого туннеля
(`SewerLocation.BranchAt(машина)`, `GameManager.UpdateSewer`, отсчёт `RunSettings.VoteDelay`) → `DepartServer` → `DriveUntil`
(реплицируется): каждый пир показывает `DriveScene` (`LevelArt.DriveScene`: бесконечный туннель из `_cut`-сегментов, пикап в профиль, длинный
объектив) → `ArriveServer` → `StartLevel`; экран темнеет и проясняется на доме (`GameHUD.ShowLoadingFade`). Панели над туннелями —
`RouteSignView`/`SewerRoute` (коротко: значок типа дома и значок главного задания — `Tools/Art/generate_icons.py` `house_*`/`job_*`, тип дома и номер, этажи и охрана, отсчёт; висят в `SewerLocation.SignOffWall` перед торцевой стеной зала). Модель пикапа — `Tools/Blender/car.py` (`Art/Models/Car/pickup.fbx`). Затопление — `SewerLocation.Flood`.
Позади машины — длинный туннель, откуда она приехала (`SewerLocation.BackSegments`), за невидимой стеной `BackWall`; фары и у модели в сцене поездки.

**Особые места дома** — чистый `HouseFeatures` (каждое — свой поток рандома), настройки `LevelConfig.Features` (`FeatureSettings`):
типы домов `HouseKinds` + `Location` (офис — пять характеров, больница, автомастерская); главное место (`LevelLayout.PointOfInterest`,
встаёт вместо большого стола/дивана/стеллажа, сжимается до его габаритов `PropPlacement.Scale`): консоль программирования роботов
(`Programmer`, кубики-оглушители по игроку), медстол (`MedTable`: здоровье до `GadgetSettings.MedTableHp` = 3, раз за дом —
`GameManager.MedKey`), верстак (`Workbench`: `Crafting.Plan` — вещи одного цвета → `ItemCatalog.Diamond`); офис — больше и ценнее лут
(`LootSettings.OfficeItems/OfficeBonus`). **Скрытая комната** (`HideRoom`: окружённая комнатами, входы `EdgeType.Illusion` с каждой
соседней; `LevelArt.IllusionBlock` — стена без коллайдера с NotWalkable-объёмом; карта и `HousePlan.SightShare` не видят сквозь неё;
проверка проходимости робота её не требует). **Двери с ключом** (`KeyDoors`: дверь — единственный путь в 1–3 комнаты; ключ — предмет
`ItemCatalog.Key` в другой комнате; `AutoDoor.NeedsKey`, `KeyLock`). **Ящики** (`Crates`: стеллаж/паллета/коробки → `SearchCrate`,
содержимое `GameManager.CrateContents`, ключ `CrateKey`). **Сторожа** (`Wardens`: `RobotKind.Warden` перед единственным проходом комнаты
с лутом, `RobotController.SetPost` — ходит A↔B, смотрит наружу; колонки `LureSpots`). Инструменты — особые формы `ItemCatalog` (в инвентаре — свои цвета, которых нет у лута: `ItemCatalog.UiColor`)
(`LootShapes` = 8; `Diamond`, `Key`, `Stun`, `Speaker`, `CageKey`; горят в топливо по цене серого цвета; ключи, ключ клетки (1×2) и оглушитель
работают только из руки — `PlayerInventory.Holds/TakeHeldServer`; ключи — настоящие модели, как лопата, `ItemCatalog.KeyScale`): брошенная колонка (`WorldItem.PlayServer`, реплицирует
`WorldItemNet`) зовёт ближайшего робота (`GameManager.UpdateLures`), он её выключает; оглушитель — `GameAction.Gadget` (X) →
`GadgetServer` (все детекторы взломаны на `StunDuration`). Всё, что игрок использует по E, — `Interactable` (`UseKind`, `IPlayerLink.SendUse`
→ `GameManager.UseServer`). Модели — `Tools/Blender/poi.py`.

**Главное задание дома** (необязательное, видно ещё в канализации на панели туннеля: `SewerRoute` строит превью дома) — `LevelLayout.Objective`
(`HouseFeatures.PlanObjective` до мебели, `FinishObjective` после охраны; свой поток рандома; настройки `FeatureSettings` + `ItemsConfig.Objectives`):
**большой сейф** — комната за единственной дверью (`VaultRoom`/`VaultDoor`, `AutoDoor.Vault`: не взламывается, табло `VaultPanel` с тремя лампами;
дверь — круглая банковская на петле, `LevelArt.VaultDoor`, `AutoDoor.Swing`; диск увеличен (`AssetBuilder.VaultDoorScale`) и прижат к стене — перекрывает отверстие
и гильзу без щели; пульты её только разблокируют, открывает игрок по E (`VaultHandle`, `UseKind.VaultDoor` → `GameManager.OpenVaultServer`, ключ `VaultOpenKey`,
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
**Запертые двери и сейфы.** `LevelLayout.LockedDoors` (индексы `AutoDoors`, свой поток рандома, с `LockedDoorsFromHouse`): `AutoDoor.Locked`
не пускает игроков, роботов пускает; взлом — держать E (`InteractionSettings.DoorHackTime`) → `IPlayerLink.SendUnlockDoor`.
Сейф — `SafeBox` на префабе Props/Safe (индекс в `LevelLayout.Props`, сид кода от сида дома), мини-игра — `SafeLock` (настройки
`ItemsConfig.Safes`), экран — `SafeCrackView`; открытие → `IPlayerLink.SendOpenSafe` → лут на сервере. Открытые двери и сейфы дома —
`GameManager.Opened` (ключи `DoorKey`/`SafeKey`), реплицирует `GameManagerNet`.

**Терминалы охраны** — `LevelLayout.Terminals`. Камеры и взлом стоят в комнате управления роботами (`RoomType.ControlRoom`: если в доме её нет,
`LevelGenerator.EnsureControlRoom` делает ею офис 16–60 м²; пульты с креслами ставит `Furnisher` как якорь и пристенные группы, `LevelGenerator.PlaceTerminals` берёт их
первыми, лишние становятся столами; без комнаты — часть столов, свой поток рандома). Вид — `Tools/Blender/terminals.py`: пульт с тремя мониторами
(`terminal_console`) и для двери склада — стойка доступа с маячком (`terminal_access`), `SecurityTerminal.Console/Access`. `SecurityTerminal` на префабе
Props/Terminal: камеры (вид через `SecurityCamera.Pivot`) или взлом издалека — `IPlayerLink.SendRemoteHack` → `GameManager.RemoteHackServer`,
заряды (`InteractionSettings.TerminalHacks`) хранятся ключами `GameManager.TerminalKey` в `Opened`. Игрок за терминалом — `PlayerController.UsingTerminal`.
**Узкие щели** — `EdgeType.Squeeze` (`LevelMetrics.SqueezeWidth`, свой поток рандома): игрок протискивается, роботов не пускает `NavMeshModifierVolume`
в префабе `Level/SqueezeFrame`. **«Я застрял»** — `Unstuck` (кольца свободных точек вокруг игрока, потом точки патруля/спавны канализации; двигает себя
владелец, у клиента нет навмеша). **Стиль (F8)** — общий для команды: `GameManager.RequestStyle` → `StyleIndex` реплицирует `GameManagerNet`.
**World-space текст** (панели туннелей, доска склада) — на слое `WorldText` (9): обычные проходы его не рисуют, его рисует RenderObjects `WorldText` после
фильтра `StyleScreen` — буквы читаются в любом стиле. Выбывший игрок смотрит глазами союзника (`PlayerController.Watching`).

**Ноги роботов** — `ProceduralLegs`: ноги, разрезанные в Blender по колену (`AccentLeg{n}Upper` / `Lower`, `generate_models.split_leg`),
сборщик превращает в пары шарниров бедро/колено (`ProceduralLegs.Bake`); в игре ступни стоят на поверхности под ними (пол, у паука —
стена), отстающая ступня шагает дугой, ноги — двумя чередующимися группами, колени — двухзвенная ИК (`LegMath`). Охранник, пёс, паук.
Шаг — `AnimationSettings.StepStride/StepTime/StepLift` в конфиге робота.

**Город вокруг дома** — декорация (`CityPlan`, чистый, от сида дома): дом — одно из зданий квартала. Фасад (сторона лобби, `CityPlan.SideNearest`) выходит
на тротуар 3,5 м и двухполосную улицу, соседи примерно той же высоты стоят в 3–6 м по бокам на той же линии застройки, за кварталом — задний проезд, по краям —
поперечные улицы с зебрами; напротив и за проездом — ряды домов, позади — башни; фонари и деревья на тротуарах, машины у бордюров. Тротуар — на уровне пола дома
(`CityPlan.WalkTop`), дороги и земля — на бордюр ниже (`RoadTop`). Невидимая стена — в `CityPlan.Yard` = 10 м от дома (`LevelArt.Fence`); тротуары, дома, машины,
столбы и стволы в пределах досягаемости получают коллайдеры (`City.Ground`/`City.Solids` → блоки `Fence`). Всё — коробки (`CityBox`), `LevelBuilder.BuildCity`
склеивает их в один меш с подмешем на материал (`CitySlot` → `LevelArt.CityMaterials`, `AssetBuilder.City`, текстуры фасадов с горящими окнами — `python Tools/Art/generate_city.py`,
этаж фасада = этаж дома); свет фонарей — `Level/CityLamp` (конус 70° вниз, без теней и без `LightSource`: в дом не светит, на геймплей не влияет).
**Коммуникации** — декорация (`UtilityPlan`, чистый, по плану и оболочке дома; без коллайдеров): вентиляционный короб под потолком коридоров
(`DuctBottom..DuctTop`, над дверями, с решётками и хомутами, огибает высокие лазы и лестничные проёмы), электрощиток на стене коридора
каждого этажа с кабель-каналом к потолку и лотком вдоль стены, накладные розетки за столами (и на стенах комнат без столов),
стояки и труба вдоль стены в туалетах, на кухнях и в раздевалках; `LevelBuilder.BuildUtilities` склеивает в один меш (`UtilitySlot` → `LevelArt.UtilityMaterials`).
**Выключатели света** — настоящие: у каждого проёма внутри комнаты (не коридора) префаб `Level/LightSwitch` (`UtilityPlan.SwitchSpots`, номер в списке —
индекс; `LevelBuilder.BuildSwitches`), E (`UseKind.LightSwitch`) → `GameManager.SwitchLightsServer` переключает ключ `LightsKey(комната)` в `Opened`:
светлая комната гаснет, тёмная загорается (`LightMath.SwitchedOn`), лампы (`RoomLight.IsSwitched`) и клавиша с зелёным огоньком в темноте следуют на каждом пире;
роботы видят результат через `LightSource`, тревога по-прежнему зажигает все лампы.
**Порожки** — где в проёме встречаются разные полы (`ArchitecturePlanner.PlanThresholds`, `DecorPiece.Threshold`, материал `StyleKit.Threshold`).
**Дверцы шкафов** — две створки рядом на одной высоте открываются от середины (`StorageMath.RightHinged`: правая — на петле справа).
**Звуки людей** — `PeopleSounds`: в газе сначала тяжёлое дыхание, потом смех всё громче и чаще (`PlayerController.GasShare`, реплицирует `PlayerNet`,
клипы `gas_breath`/`gas_laugh_0..2` из `generate_sounds.py`); заложник окликает игрока, который видит клетку рядом (реплики `Voices/hostage_*` из
`generate_voices.py`); прыжки и приземления — `AirState` (подъём на мебель и дрожь на краю — не прыжок).
**Фасад без швов** — наружные стены каждого этажа доходят до пола следующего (кромка плиты за ними), над уличной дверью — стена, а не перемычка цвета обрамления,
столбы в стыках стоят точно в концах стен (ключ словаря округлён, позиция — нет).
**Стены у лестничного проёма** — над проёмом в перекрытии плиты нет, поэтому стены нижнего этажа вдоль него достраиваются полосой до пола следующего (`LevelBuilder.StairBands`).
Настенное (`Furnisher.OnWall`: доска, огнетушитель, часы…) висит вплотную к стене (`HungGap`), стоящая у стены мебель — с зазором `WallGap`.
Руль машины поворачивается в сторону поворота, в `CarSettings.SteeringRatio` раз дальше колёс (`CarLook.SteeringWheel`).
**Размер дома** — `BuildingSettings.GridFor(дом, игроки)`: растёт с каждым домом, мягко — с командой (`AreaByPlayers`); квота — `RunSettings.QuotaByPlayers`,
`RunState.SetPlayers` пересчитывает квоту, когда команда меняется посреди дома. Число игроков — часть `LevelInfo` (все пиры строят один дом).

**Сплеш студии** — `StudioSplash` в префабе меню (юнитевский сплеш выключен, `PlayerSettings.SplashScreen.show`): крыло Zhukovsky Games, раз за запуск,
автопилоту не показывается; на время сплеша музыка меню молчит.

Тестовые хуки собраны в `DevFlags` (сид, уровень, мирный режим, автопилот…) — в обычной игре не используются.

**Рандом — только по сиду.** Один сид забега (`GameManager.RunSeed`, `-seed`) даёт сиды домов (`LevelRandom.LevelSeed`), сид дома — отдельный
поток на каждую цель (`RandomChannel`: планировка, лут, роботы, патрули, устройства, звук) и на каждый объект (`LevelRandom.Next` — n-й робот).
`UnityEngine.Random` в игровом коде не используется (проверяет `LevelRandomTests`), он только засеян на всякий случай; новый бросок — новый канал
или поток объекта, чтобы не сдвигать остальные.

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
свет по `LightSource` и `VisualStyles.GameplayAmbient`, плотность газа — геймплей и не меняется. F8 листает стили, `-style <имя>` включает при запуске,
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
Игроки — айтишники четырёх отделов (`PlayerLooks`: бэкенд, тестирование, сисадмины, дизайн по слоту игрока; `VisualStyles.SetVariant`
выбирает модель отдела). Цвет команды — только `Accent` (рюкзак, повязка, шнурок/шарф), одежда — цвет отдела, лицо — кожа, детали головы — тёмные. Новые объекты одеваются через `VisualStyles.Dress` при спавне. **Архитектура** — `LevelBuilder` записывает
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
по семейству стиля и сиду дома или вид, который просит стиль (`StyleGeometry.Stairs`; у стиля по умолчанию — дерево), `StairLook` переключает при F8
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
**Стиль Саши `S16_Derelict`** «Заброшенный комплекс» (влит из `feature_visual_style_11_prototype`): семейство моделей `Derelict`, свои PBR-поверхности,
мебель и деградация (`Tools/Blender/derelict_*.py`, пятна и плакаты раскладывает `ArchitecturePlanner`), человек со скелетом вместо модели игрока
(`Tools/Blender/human.py`, `HumanRig`, `StyleGeometry.PlayerRig`), свет стиля только для глаз (`StyleWorld.LightScale`/`LampGlow`). Сценарий `perf` пишет, сколько рендереров рисуется и чьи они.

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
Туман задаётся в рантайме (газ снаружи, дымка внутри), поэтому вырезание вариантов тумана — ручное (`GraphicsSettings`: Exp2 оставлен) и туман включён
в самой сцене: иначе билд выбрасывает шейдеры с туманом и газа не видно. Материалы со свечением — `globalIlluminationFlags = BakedEmissive`
(с `None` URP при импорте выключает `_EMISSION`). GPU Resident Drawer выключен: с ним плеер падал на старте уровня.
Graphics jobs выключены (`BuildPlayerWindows`): с ними плеер изредка падал на старте уровня внутри движка (`ShaderVariantMonitor::FrameFinished`, гонка
потоков при загрузке новых вариантов шейдеров; стек расшифрован по PDB из `PlaybackEngines/windowsstandalonesupport/Variations`).
Модели — `Tools/Blender/*.py` (после экспорта откати неизменённые FBX: `git diff --name-only Assets/_Game/Art/Models | xargs git checkout --`;
перед экспортом каждая модель проходит `meshfix.fix_objects`, проверка — `review.py` / `audit_models.py`, как моделировать — скилл [`blender-modeling`](.claude/skills/blender-modeling/SKILL.md)),
звуки — `python Tools/Audio/generate_sounds.py` (детерминированно, с сидом), голос роботов — `python Tools/Audio/generate_voices.py` (Windows, ru-RU голос).

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

- Войс-чат: `NoiseSystem.EmitServer(позиция, радиус, clientId)` на сервере — роботы с микрофонами услышат.
- Сеть: всё сетевое — в `Scripts/Net` (сборка `Stealth.Net`), игровой код от Netcode не зависит; как добавить сетевой объект — в скилле `stealth-code`.
- Предметы: `ItemCatalog` (форма + цвет), расстановка — `LootPlanner`, в мире — `WorldItem`, инвентарь — `PlayerInventory`.
- Мета-прогрессия: итог забега `GameManager.RunTotal`, конец забега `GameManager.Phase == RunPhase.Failed`.
- Новый робот или датчик: чек-лист в скилле `stealth-code`.
