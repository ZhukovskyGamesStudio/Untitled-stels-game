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

## Сеть

- Сервер авторитетен для здоровья, лута, роботов, тревог. Клиент шлёт запрос `[Rpc(SendTo.Server)]`, сервер **перепроверяет** условия (дистанция, состояние).
- Состояние для всех — `NetworkVariable` (запись только сервером, кроме явно owner-полей игрока). Эффекты для всех — `[Rpc(SendTo.Everyone)]`.
- Детерминированное (уровень из сида, качание камер и лидаров) считается на каждом пире от `NetworkManager.ServerTime`, не синхронизируется.
- Тестовые хуки — только в `DevFlags`. Не добавляй статические «ForTest»-флаги в игровые классы.

## Стиль

- Приватные поля `m_Name`, статические приватные `s_Name`, всё публичное и константы — `PascalCase`.
- Идентификаторы и комментарии — английские; всё, что видит игрок, — русское.
- `<summary>` у каждого публичного типа: что это и зачем (не пересказ кода).
- Магические числа баланса — именованные константы или поля префаба с `[Tooltip]`.
- Никаких `GetComponent` в `Update` по всем объектам — держи ссылки или используй виртуальные свойства базового класса (`Detector.IsChasing`).

## Чек-лист: новый робот или датчик

1. Значение в `RobotKind` (в конец перечисления) → `RobotInfo.Name/Senses` → строка в `RobotRoster` (вес, с какого уровня), `CanUseStairs`, `IsDevice`.
2. Подвижный на навмеше: только префаб — `MobileRobot(...)` в `AssetBuilder.Actors.cs` с настройкой `RobotSenses`/`RobotTraits`. Новый канал чувств — в `RobotController.Sense()` + формула в `SensorMath` + тест.
   Своё движение: класс `: SecurityDevice`, датчики в `TickServer()` через `Notice/Decay/Trip`, визуал в `TickLocal()`; префаб — `Device<T>(...)`.
3. Модель — в `Tools/Blender/robots.py` (части `Body`/`Accent*`/`Eye*`; `AccentLeg*`, `AccentWheel*`, `AccentRotor*`, `AccentHead` анимируются автоматически).
4. `FORCE=1 Tools/dev.sh assets`, `Tools/dev.sh tests` (`PrefabIntegrityTests` проверит ссылки и вид), сценарий `gallery` со скриншотом.

## Чек-лист: кусок уровня

1. Префаб в `AssetBuilder.Level.cs`, поле в `LevelArt`, постановка в `LevelBuilder`.
2. Если это данные планировки — поле в `LevelLayout`, генерация в `LevelGenerator` (детерминированно от `rng`), тест в `LevelGeneratorTests`.
3. Проходимость — `LevelNavMeshTests`; внешний вид — сценарии `floors`/`rooms`.

## Чек-лист: элемент интерфейса

1. Элемент-префаб в `AssetBuilder.UI.cs` (`BuildUiElements`) и/или место на экране (`BuildHud`/`BuildInventoryScreen`/`BuildMenu`).
2. Поле-ссылка в презентере; логика выбора — в чистом классе (`HudRules.cs`) с тестом.
3. Разметка под 1280×720, масштабируется по высоте окна (`CanvasScaler`). Проверка — сценарии `ui` / `menu` со скриншотами.
