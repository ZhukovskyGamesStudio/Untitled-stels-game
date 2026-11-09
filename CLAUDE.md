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
Tools/dev.sh run full 1         # хост + 1 клиент по сети
```

Логи: `Logs/tests.log`, `Logs/build_assets.log`, `Logs/build_player.log`, `Builds/host.log`, `Builds/client*.log`.
Сценарии автопилота — в `Scripts/Game/DevAutomation*.cs` (`full`, `client`, `coop`, `move`, `ui`, `menu`, `floors`, `rooms`, `vent`, `perf`, `gallery`, `hunter`, `lasers`, `radio`, `hack`).
Как проверять изменения — скилл [`stealth-verify`](.claude/skills/stealth-verify/SKILL.md).

## Структура `Assets/_Game`

```
Art/Materials/<Environment|Props|Robots|Player|Items|FX>   материалы (создаёт AssetBuilder)
Art/Models/<Props|Level|Player|Items|Robots|Sensors>       FBX из Blender (Tools/Blender/*.py)
Art/Sprites/                                               текстуры UI (виньетка, дуга)
Data/            LevelArt, ItemVisuals, NetworkPrefabs (ScriptableObject)
Data/Config/     НАСТРОЙКИ БАЛАНСА: Player, Run, Level, Items (правятся руками в инспекторе)
Data/Robots/     профили подвижных роботов: чувства и характеристики (Guard, Listener, Drone…)
Resources/GameConfig.asset            корень настроек (ссылки на Data/Config/*), его находит любой билд
Prefabs/Level    куски здания: LevelRoot (NavMeshSurface), блоки стен/перекрытий, Stairs/StairsMirrored, AutoDoor, VentFrame, RoomLight, StreetLamp, Hatch
Prefabs/Props    мебель
Prefabs/Robots   подвижные роботы · Prefabs/Sensors — датчики, камера, лазер
Prefabs/Player, Items, Network (Session), FX (RadioRipple)
Prefabs/UI       экраны HUD, Inventory, Menu + UI/Elements (кнопка, слайдер, дуга, маркеры…)
Scenes/Game.unity                      единственная сцена: сеть, камера меню, экземпляры UI-префабов, EventSystem
Scripts/<Enemies|Game|Items|Level|Player|UI>   код игры (asmdef Stealth.Game — без Netcode и Steam)
Scripts/Net/                          сетевой слой (asmdef Stealth.Net): NGO, Steam, сетевые двойники компонентов
Editor/AssetBuilder*.cs               генератор ассетов (asmdef Stealth.Editor)
Tests/EditMode/                       тесты (asmdef Stealth.Tests.EditMode)
```

## Архитектура

Три слоя игры плюс отдельный сетевой слой:

1. **Чистые правила** — обычные C#-классы и статические функции без `MonoBehaviour`, сети и физики. Здесь живут решения, и здесь же — тесты.
   `MovementMath`, `Stamina`, `HealthState`, `GasExposure`, `FootstepNoise`, `RobotBrain`, `SuspicionMeter`, `SensorMath`, `Perception` (геометрия),
   `Hacking`, `RobotRadio`, `ScentTrail`, `SeismicSense`, `ScanLine`, `LevelGenerator`, `StairGeometry`, `RoomLighting`, `LootPlanner`, `RobotRoster`,
   `PromptRules`, `BannerQueue`, `GainTracker`, `UiMath`, `ItemCatalog`.
2. **Компоненты** — тонкие `NetworkBehaviour`/`MonoBehaviour`: связывают правила с вводом, сетью, физикой и сценой.
3. **Презентеры UI** — `GameHUD`, `InventoryUI`, `GameMenu`: только показывают/двигают/красят элементы, заранее собранные в префабах.
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

Настройки робота — в префабе: `RobotController.Senses` (`RobotSenses`: глаза, слух, сейсмика, нюх, сортировщик) и `Traits` (`RobotTraits`: скорости, лестницы, атака, тревога).
Цвета статусов — `SecurityPalette`. Описание для игрока — `RobotInfo`. Где и когда появляется — `RobotRoster`.

### Уровень

`LevelGenerator.Generate(seed, level)` — чистая генерация планировки (`LevelLayout`). `ActiveLevel.Load(layout, art)` строит сцену через `LevelBuilder`
**только из префабов `LevelArt`**: блоки-кубы масштабируются под размер, лестницы/двери/лазы/лампы/мебель ставятся как есть.
Генерация детерминирована: каждый пир строит уровень сам из общего сида (`GameManager.Level`). Навмеш, лут, роботы — только на сервере.
Размеры здания — `LevelMetrics`.

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

Тестовые хуки собраны в `DevFlags` (сид, уровень, мирный режим, автопилот…) — в обычной игре не используются.

### Настройки (конфиги)

Логика отделена от данных: всё, что крутим при итерациях над игрой, лежит в ScriptableObject-конфигах, а не в коде.

| Ассет | Что внутри |
| --- | --- |
| `Data/Config/Player` | движение и прыжок, выносливость, неуязвимость после ударов, шум шагов, газ, оживление и взлом (время, дальность, длительность) |
| `Data/Config/Run` | квота (доля, рост, потолок), штраф за упавших, тревога, льготный период, когда приходит охотник |
| `Data/Config/Level` | размер здания по домам, веса типов комнат, темнота и мерцание, проходы/двери/лазы, сколько охраны и **кто** с какого дома (ростер), лут |
| `Data/Config/Items` | цвета лута: название, цвет, множитель ценности, частота |
| `Data/Robots/<Kind>` | профиль подвижного робота: глаза, слух, сейсмика, нюх, скорости, лестницы, атака, тревога |

- Чистые правила получают **группы настроек** параметром (`MovementSettings`, `StaminaSettings`, `RunSettings`, `BuildingSettings`,
  `SecuritySettings`, `RosterSettings`, `LootSettings`…); без параметра — значения по умолчанию из кода (их же видят тесты).
- Компоненты берут настройки через `Tuning` (`Tuning.Player.Movement`, `Tuning.Level.Security`…), который грузит `Resources/GameConfig`.
- Устройства со своими скриптами (турель, паук, пылесос, лидар, датчик движения, плита, камера, лазер) настраиваются полями своих префабов.
- Сборщик создаёт конфиги один раз и **никогда не перезаписывает** (даже `FORCE=1`). Добавил поле в группу настроек — в старом ассете оно получит значение по умолчанию из кода.
- Уровень строится детерминированно из сида на каждом пире: у хоста и клиентов должен быть один и тот же билд (одни и те же конфиги).

### Ассеты из кода

`Editor/AssetBuilder*.cs` создаёт материалы, спрайты, все префабы, `LevelArt`, список сетевых префабов и сцену.
Меню **Stealth → Build Missing Assets** дособирает только отсутствующее (ручные правки префабов сохраняются),
**Rebuild All Assets** (или `FORCE=1`) перезаписывает всё. Пути не меняются, поэтому GUID и сетевые хэши стабильны.
Если меняешь структуру префаба в коде — пересобери с `FORCE=1` и закоммить изменённые префабы вместе с кодом.

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
