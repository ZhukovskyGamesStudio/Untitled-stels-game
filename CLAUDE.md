# CLAUDE.md — «Айтишники: вылазка»

Кооперативный стелс-рогалик (прототип кор-лупа). Дизайн-документ: https://claude.ai/artifact/GdaGu25vZ1ezVUmZ8KWFap.
Обзор для людей, управление и таблица роботов — в [README.md](README.md). Этот файл — как устроен код и как с ним работать.

- Unity **6000.6.0f1**, URP, новый Input System (только `Keyboard.current` / `Mouse.current`).
- Сеть: Netcode for GameObjects 2.13 (`NetworkVariable`, `[Rpc(SendTo.*)]`). Транспорт Steam (Steamworks.NET, AppID 480) или UnityTransport UDP 7777.
- Интерфейс: uGUI (legacy `Text`, шрифт `LegacyRuntime.ttf`). IMGUI больше не используется.
- Навмеш: com.unity.ai.navigation, запекание в рантайме; зона 3 `Stairs` — по ней ходят только роботы с ногами.

## Ветки

Только три ветки: `main`, `george`, `sasha`. Новых не создавать. Работа Claude идёт в `george`; в `main` вливаем только по явной просьбе.
Как коммитить — скилл [`stealth-commit`](.claude/skills/stealth-commit/SKILL.md).

## Команды

Unity в batchmode не запустится, если проект открыт в редакторе — закрой его или работай через редактор.

```bash
Tools/dev.sh tests              # компиляция + все EditMode-тесты (итог и упавшие — в конце вывода)
Tools/dev.sh assets             # дособрать недостающие ассеты (существующие префабы не трогает)
FORCE=1 Tools/dev.sh assets     # пересобрать ВСЕ ассеты из кода (ручные правки в префабах пропадут)
Tools/dev.sh player             # Windows-билд в Builds/Windows/StealthPrototype.exe
SEED=8 LEVEL=5 Tools/dev.sh run gallery      # билд с автопилотом: лог [AUTOTEST] + скриншоты в Builds/shots/<сценарий>
Tools/dev.sh run full 1         # хост + 1 клиент по сети (без звука и за другими окнами; SOUND=1 / FRONT=1 — со звуком / на переднем плане)
```

Логи: `Logs/tests.log`, `Logs/build_assets.log`, `Logs/build_player.log`, `Builds/host.log`, `Builds/client*.log`.
Сценарии автопилота — в `Scripts/Game/DevAutomation*.cs` (`full`, `client`, `coop`, `move`, `ui`, `menu`, `floors`, `rooms`, `vent`, `perf`, `gallery`, `hunter`, `lasers`, `radio`, `hack`, `detect`, `darkbox`, `newbots`).
Как проверять изменения — скилл [`stealth-verify`](.claude/skills/stealth-verify/SKILL.md).

## Структура `Assets/_Game`

```
Art/Materials/<Environment|Props|Robots|Player|Items|FX>   материалы (создаёт AssetBuilder)
Art/Audio/<Player|Items|UI|Robots|Environment|Music>       WAV из Tools/Audio/generate_sounds.py
Art/Models/<Props|Level|Player|Items|Robots|Sensors>       FBX из Blender (Tools/Blender/*.py)
Art/Sprites/                                               текстуры UI (виньетка, дуга)
Data/            LevelArt, ItemVisuals, NetworkPrefabs, SoundBank (ScriptableObject)
Data/Config/     НАСТРОЙКИ: Player, Run, Level, Items, Audio (правятся руками в инспекторе)
Prefabs/Robots/<Префаб>Config.asset, Prefabs/Sensors/<Префаб>Config.asset — настройки каждого робота, устройства и лазера рядом с его префабом
Resources/GameConfig.asset            корень настроек (ссылки на Data/Config/*), его находит любой билд
Prefabs/Level    куски здания: LevelRoot (NavMeshSurface), блоки стен/перекрытий, Stairs/StairsMirrored, AutoDoor, VentFrame, RoomLight, StreetLamp, Hatch
Prefabs/Props    мебель
Prefabs/Robots   роботы + их конфиги · Prefabs/Sensors — датчики, камера, лазер + их конфиги
Prefabs/Player, Items, Network (Session), FX (RadioRipple), Audio (AudioDirector)
Prefabs/UI       экраны HUD, Inventory, Menu, StashBoard + UI/Elements (кнопка, слайдер, дуга, маркеры…)
Scenes/Game.unity                      единственная сцена: сеть, камера меню, экземпляры UI-префабов и AudioDirector, EventSystem
Scripts/<Audio|Config|Enemies|Game|Items|Level|Player|UI>   код игры (asmdef Stealth.Game — без Netcode и Steam)
Scripts/Net/                          сетевой слой (asmdef Stealth.Net): NGO, Steam, сетевые двойники компонентов
Editor/AssetBuilder*.cs               генератор ассетов (asmdef Stealth.Editor)
Tests/EditMode/                       тесты (asmdef Stealth.Tests.EditMode)
```

## Архитектура

Три слоя игры плюс отдельный сетевой слой:

1. **Чистые правила** — обычные C#-классы и статические функции без `MonoBehaviour`, сети и физики. Здесь живут решения, и здесь же — тесты.
   `MovementMath`, `Stamina`, `HealthState`, `GasExposure`, `FootstepNoise`, `RobotBrain`, `SuspicionMeter`, `SensorMath`, `Perception` (геометрия),
   `Hacking`, `RobotRadio`, `ScentTrail`, `SeismicSense`, `ScanLine`, `LevelGenerator`, `StairGeometry`, `RoomLighting`, `LootPlanner`, `RobotRoster`,
   `PromptRules`, `BannerQueue`, `GainTracker`, `UiMath`, `ItemCatalog`, `TeamStash`, `AudioRules`.
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

На мобильных роботах `SensorRig` показывает насадки-датчики (глаз-камера, микрофоны, щупы в пол, нос) по `Senses` из конфига.
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
Сейсмик — волны (`SeismicSense.Wave*`, `PulseStomp` → кольцо `StompRipple` и звук), подметальщик — лидар у пола (`RobotLidar`),
рабочие везут лут (`DetectorSnapshot.Cargo`, при взломе падает на пол). `RobotRoster.Newcomers` — новый вид гарантированно есть в доме, где он появляется.
`RobotAnimator`: наклоны в разгоне и повороте, испуг при «Обнаружен», обмякание при взломе, удар сейсмика, глаз-камера следит за замеченным игроком.
Настройки робота — в префабе: `RobotController.Senses` (`RobotSenses`: глаза, слух, сейсмика, нюх, сортировщик) и `Traits` (`RobotTraits`: скорости, лестницы, атака, тревога).
Цвета статусов — `SecurityPalette`. Описание для игрока — `RobotInfo`. Где и когда появляется — `RobotRoster`.

### Уровень

`LevelGenerator.Generate(seed, level)` — чистая генерация планировки (`LevelLayout`). `ActiveLevel.Load(layout, art)` строит сцену через `LevelBuilder`
**только из префабов `LevelArt`**: блоки-кубы масштабируются под размер, лестницы/двери/лазы/лампы/мебель ставятся как есть.
Генерация детерминирована: каждый пир строит уровень сам из общего сида (`GameManager.Level`). Навмеш, лут, роботы — только на сервере.
Размеры здания — `LevelMetrics`. Мебель — `LevelGenerator.FurnishRoom`: четыре квадранта комнаты в координатах «от центра к стенам»
(крест посередине свободен под двери), раскладки по типам комнат. Пристенная мебель держит ≥ 20 см от оси стены (жалюзи и подоконники окон).
Коллайдеры мебели — `MeshCollider` на каждой части модели. `LevelBuilderTests.FurnitureDoesNotClip` проверяет, что мебель и окна не пересекаются
со стенами, лестницами и друг с другом, `LevelNavMeshTests` — что до лута можно дойти.

### Сеть

Игровая сборка `Stealth.Game` **не ссылается** на Netcode/Steam (проверяет `NetworkBoundaryTests`). Всё сетевое — в `Stealth.Net`:

```
Игра (Stealth.Game)                         Сеть (Stealth.Net)
PlayerController + PlayerInventory   ⇄  PlayerNet + InventoryNet
GameManager (+ RunState)              ⇄  GameManagerNet
Detector (все роботы и датчики)        ⇄  DetectorNet
WorldItem                             ⇄  WorldItemNet
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
- Правила забега (квота, штрафы, тревоги, охотник, конец уровня) — чистый класс `RunState`.
- Склад команды у люка — `TeamStash` в `GameManager.Stash` (сервер), реплицирует `GameManagerNet`; запросы — `PlayerInventory.Request{Stash,Unstash,StashMove,SellStashed,StashWorld,SellWorld}`.
  Записи склада адресуются по значению (`InventoryEntry`), не по индексу: список мог измениться, пока запрос летел. Склад живёт весь забег, `RestartRunServer` его чистит.

Тестовые хуки собраны в `DevFlags` (сид, уровень, мирный режим, автопилот…) — в обычной игре не используются.

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

### Ассеты из кода

`Editor/AssetBuilder*.cs` создаёт материалы, спрайты, все префабы, `LevelArt`, список сетевых префабов и сцену.
Меню **Stealth → Build Missing Assets** дособирает только отсутствующее (ручные правки префабов сохраняются),
**Rebuild All Assets** (или `FORCE=1`) перезаписывает всё. Пути не меняются, поэтому GUID и сетевые хэши стабильны.
Если меняешь структуру префаба в коде — пересобери с `FORCE=1` и закоммить изменённые префабы вместе с кодом.
Сцена в режиме «только недостающее» не пересобирается: новый экран или объект сцены появится после `FORCE=1`.
Рендер: URP Forward+, луна (направленный свет с тенями) и тёмный эмбиент задаются в `AssetBuilder.Scene`, постобработка — `Data/PostProcess.asset`
(bloom, тонмаппинг, виньетка; пересоздаётся только с `FORCE=1`), атласы теней — `Assets/Settings/PC_RPAsset.asset`.
Модели — `Tools/Blender/*.py` (после экспорта откати неизменённые FBX: `git diff --name-only Assets/_Game/Art/Models | xargs git checkout --`),
звуки — `python Tools/Audio/generate_sounds.py` (детерминированно, с сидом), голос роботов — `python Tools/Audio/generate_voices.py` (Windows, ru-RU голос).

## Правила кода (коротко; подробно — скилл [`stealth-code`](.claude/skills/stealth-code/SKILL.md))

- В рантайме **не собирать объекты кодом** (`new GameObject` + `AddComponent`, `CreatePrimitive`, процедурные текстуры): только `Instantiate` префабов, на которые ссылаются поля/`LevelArt`.
- Один `MonoBehaviour` — один файл с тем же именем (иначе Unity теряет скрипт в префабах).
- Логику, которую можно проверить без движка, выносить в чистый класс и покрывать тестом. Время и случайность — параметрами.
- Число, которое будут подкручивать при балансе, — в группу настроек конфига (с `[Tooltip]`), а не константой в коде. Константы — только для геометрии и того, что не тюнится.
- Сервер решает, клиенты показывают. В игровом коде нет `NetworkVariable`/`Rpc`/`NetworkManager`: состояние — обычные поля, запросы — через link-интерфейсы,
  проверка — в серверном обработчике игрового компонента. Netcode — только в `Scripts/Net`.
- Приватные поля `m_Name`, статические `s_Name`, константы и публичное — `PascalCase`. Комментарии и идентификаторы — по-английски, тексты для игрока — по-русски.
- Найденный баг — чинить и, если можно, закрыть тестом.

## Точки стыковки для коллеги

- Войс-чат: `NoiseSystem.EmitServer(позиция, радиус, clientId)` на сервере — роботы с микрофонами услышат.
- Сеть: всё сетевое — в `Scripts/Net` (сборка `Stealth.Net`), игровой код от Netcode не зависит; как добавить сетевой объект — в скилле `stealth-code`.
- Предметы: `ItemCatalog` (форма + цвет), расстановка — `LootPlanner`, в мире — `WorldItem`, инвентарь — `PlayerInventory`.
- Мета-прогрессия: итог забега `GameManager.RunTotal`, конец забега `GameManager.Phase == RunPhase.Failed`.
- Новый робот или датчик: чек-лист в скилле `stealth-code`.
