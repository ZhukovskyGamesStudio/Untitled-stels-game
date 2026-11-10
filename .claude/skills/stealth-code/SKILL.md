---
name: stealth-code
description: How to write and change code in the «Айтишники: вылазка» Unity project — layering (pure rules / components / UI presenters), the detector hierarchy and interfaces, prefab-only runtime, networking rules, naming, and checklists for adding a robot, a sensor, a level piece or a UI element. Use before writing or refactoring any C# in Assets/_Game.
---

# Как писать код в этом проекте

Сначала прочитай `CLAUDE.md` (архитектура, структура папок). Здесь — правила и чек-листы.

## Три слоя

1. **Чистое правило** (plain C# / static, без `UnityEngine.Object`, сети, физики, `Time.time`).
   Время (`now`, `dt`) и случайность (`System.Random`) — параметрами. Сюда — всё, что решает «что должно произойти».
   Каждое правило покрыто тестом в `Tests/EditMode`.
2. **Компонент** (`NetworkBehaviour` / `MonoBehaviour`) — читает ввод, физику, сеть; вызывает правило; применяет результат.
   Никаких развесистых `if` с балансом внутри компонента: если логика растёт — выноси в правило.
3. **Презентер UI** — только показывает элементы префаба (`Ui.Show`, `Ui.Set`, `UiPool<T>`). Что показывать — решает правило (`PromptRules`, `BannerQueue`…).

Пример: `RobotController` (тело и датчики) → `RobotBrain` (решения) → `IRobotBody` (что сделать телом).
Тест мозга работает с `FakeBody` и `FakeIntruder` из `Tests/EditMode/Fakes.cs`.

## Объекты в рантайме — только из префабов

- Нельзя: `new GameObject(...)` + `AddComponent`, `GameObject.CreatePrimitive`, `new Texture2D` для UI, сборка UI в `OnGUI`.
- Можно: `Instantiate(prefabField)`, пулы `UiPool<T>` поверх префаба-элемента, включение/выключение заранее собранных детей.
- Префабы создаёт `Editor/AssetBuilder*.cs` (раздел по смыслу: `.Level`, `.Actors`, `.UI`, `.Scene`, `.Materials`).
  Новый префаб: метод `Build…()` в нужном partial-файле + вызов `Prefab(path, Build…)` + ссылка в поле, откуда его инстанцируют.
- Один `MonoBehaviour` = один файл с тем же именем. Иначе в префабе будет «Missing Script» (`PrefabIntegrityTests` это ловит).

## Сеть — отдельная сборка

- `Stealth.Game` не ссылается на Netcode и Steam (`NetworkBoundaryTests` упадут). `NetworkBehaviour`, `NetworkVariable`, `Rpc`, `NetworkManager` — только в `Scripts/Net` (`Stealth.Net`).
- Сетевой игровой объект = игровой компонент (`MonoBehaviour`, обычные поля) + **двойник** `…Net : NetworkBehaviour` на том же префабе.
  - Состояние: игровой компонент держит поля; двойник на авторитете зовёт `PushState()` (только изменения → `NetworkVariable`), на остальных — `Apply…(...)` в игровой компонент.
  - Запросы клиента: игровой компонент зовёт метод link-интерфейса (`IPlayerLink.SendRevive`, `IInventoryLink.RequestPickup`…), двойник шлёт RPC,
    на сервере вызывает обработчик игрового компонента (`OnReviveRequest`, `PickupServer`), который **перепроверяет** условия. Без link (тесты) — вызывай обработчик сразу.
  - Эффекты для всех: link-метод (`IDetectorLink.BroadcastPulse`, `IGameLink.BroadcastBanner`) → RPC `SendTo.Everyone` → `Receive…/Show…` в игровом компоненте.
- Спавн/деспавн сетевых префабов — `Session.Spawn(prefab, pos, rot, configure)` / `Session.Despawn(go)`; настройку делай в `configure`, двойник отправит её вместе со спавном.
- Сервер ли мы — `IsServer` компонента (выставляет двойник) или `Session.IsServer`; общее время — `Session.ServerTime` (детерминированные качания камер, лидаров, лазеров).
- Новый сетевой объект: игровой компонент + двойник в `Scripts/Net` + `AddComponent` двойника в `AssetBuilder.Actors.cs` + строка в `NetworkBoundaryTests.EveryNetworkedGameComponentHasItsTwin`.
- Новое поле, которое должны видеть все: поле в игровом компоненте → `NetworkVariable` + строка в `PushState`/`Pull` двойника. Для `Detector` — поле в `DetectorSnapshot`.
- Тестовые хуки — только в `DevFlags`. Не добавляй статические «ForTest»-флаги в игровые классы.

## Стиль

- Приватные поля `m_Name`, статические приватные `s_Name`, всё публичное и константы — `PascalCase`.
- Идентификаторы и комментарии — английские; всё, что видит игрок, — русское.
- `<summary>` у каждого публичного типа: что это и зачем (не пересказ кода).
- Числа баланса — в конфиги (`Data/Config/*`; у робота, устройства и лазера — свой `<Префаб>Config.asset` рядом с префабом): поле в нужной группе настроек
  (`[Serializable]` класс рядом с правилом, например `RunSettings` в `RunState.cs`) с `[Tooltip]` и значением по умолчанию.
  Правило получает группу параметром (`new RunState(settings)`, `LevelGenerator.Generate(seed, level, building, security)`),
  компонент — через `Tuning.Player/Run/Level/Items`, робот/устройство — через своё поле `Profile`.
- В компонентах префабов роботов и устройств **не бывает** `public float/int` полей (`TuningTests.PrefabsHoldNoTuningNumbers`): только ссылки.
- Геометрия и то, что не тюнится (размер клетки, высота этажа), — константы (`LevelMetrics`).
- Никаких `GetComponent` в `Update` по всем объектам — держи ссылки или используй виртуальные свойства базового класса (`Detector.IsChasing`).

## Чек-лист: новый робот или датчик

1. Значение в `RobotKind` (в конец перечисления) → `RobotInfo.Name/Senses` → строка в ростере `RobotRoster.DefaultRobots/DefaultDevices`
   (и в `Data/Config/Level.asset` → Security → Roster, если ассет уже создан), `CanUseStairs`, `IsDevice`.
2. Подвижный на навмеше: только префаб — `MobileRobot(...)` в `AssetBuilder.Actors.cs`; начальные `RobotSenses`/`RobotTraits`/`AnimationSettings`
   из лямбды попадут в новый `Robot<Kind>Config.asset` рядом с префабом (дальше настраивается он, сборщик его не трогает). Новый канал чувств — в `RobotController.Sense()` + формула в `SensorMath` + тест.
   Своё движение: класс `: SecurityDevice`, датчики в `TickServer()` через `Notice/Forget/Trip`, визуал в `TickLocal()`; префаб — `Device<T>(...)`.
   Его числа — класс `…Profile : DeviceProfile` (свой файл в `Scripts/Config`), поле `public …Profile Profile` + `Device => Profile`,
   в сборщике `x.Profile = Profile<…Profile>("ИмяПрефаба")`.
3. Модель — в `Tools/Blender/robots.py` (части `Body`/`Accent*`/`Eye*`; `AccentLeg*`, `AccentWheel*`, `AccentRotor*`, `AccentHead` анимируются автоматически).
   Насадки-датчики (`SensorRig`: глаз, микрофоны, щупы, нос) ставятся `MobileRobot` сам по `eyePos`/`radius` — проверь на скриншоте `gallery`, что они не утонули в модели.
   Новое чувство — новая насадка в `robots.py` (`attach_*`) и строка в `SensorRig.Mounts`.
4. `FORCE=1 Tools/dev.sh assets`, `Tools/dev.sh tests` (`PrefabIntegrityTests` проверит ссылки и вид), сценарий `gallery` со скриншотом.

## Чек-лист: кусок уровня или мебель

1. Префаб в `AssetBuilder.Level.cs`, поле в `LevelArt`, постановка в `LevelBuilder`. Мебель: модель в `generate_models.py`, значение в конец `PropKind`,
   `PropObject(...)` (коллайдер — меш каждой части), место в раскладке комнаты (`LevelGenerator.*Quadrant`, координаты квадранта `Quad.P(u, v)`).
2. Если это данные планировки — поле в `LevelLayout`, генерация в `LevelGenerator` (детерминированно от `rng`), тест в `LevelGeneratorTests`.
3. Проходимость — `LevelNavMeshTests`; клиппинг — `LevelBuilderTests.FurnitureDoesNotClip`; внешний вид — сценарии `floors`/`rooms`.

## Чек-лист: звук

1. Клип — функция в `Tools/Audio/generate_sounds.py` + `write(...)` в `main()`, запусти скрипт.
2. Поле в `SoundBank` + строка в `AssetBuilder.Audio.cs` (`BuildSoundBank`), `FORCE=1 Tools/dev.sh assets` (`AudioTests.SoundBankHasEveryClip`).
3. Разовый звук — `Sfx.At(Sfx.Bank?.Clip, позиция, громкость, группа)` там, где событие видно **на каждом пире** (реплицированное состояние,
   пульс детектора), или `Sfx.Ui(...)` для своего игрока. Слежение за состоянием (смены, переходы) — в `AudioDirector`, решения — в `AudioRules` с тестом.
   Петля на объекте — `AddLoop(root, clip)` в сборщике (`LoopSound` берёт громкость и дальность из `Data/Config/Audio`).

## Чек-лист: элемент интерфейса

1. Элемент-префаб в `AssetBuilder.UI.cs` (`BuildUiElements`) и/или место на экране (`BuildHud`/`BuildInventoryScreen`/`BuildMenu`).
2. Поле-ссылка в презентере; логика выбора — в чистом классе (`HudRules.cs`) с тестом.
3. Разметка под 1280×720, масштабируется по высоте окна (`CanvasScaler`). Проверка — сценарии `ui` / `menu` со скриншотами.
