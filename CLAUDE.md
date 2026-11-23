# CLAUDE.md — USG (untitled stels game)

Кооперативный стелс-рогалик (прототип кор-лупа). Дизайн-документ: https://claude.ai/artifact/GdaGu25vZ1ezVUmZ8KWFap.
Обзор для людей, управление и таблица роботов — [README.md](README.md). Здесь — только то, что нужно в любой задаче;
подробности подсистем — в скиллах (таблица ниже), дизайн генератора — [Docs/HouseGenerator.md](Docs/HouseGenerator.md).

- Unity **6000.6.0f1**, URP Forward+, новый Input System (через `Controls`, не `Keyboard.current`).
- Сеть: Netcode for GameObjects 2.13, транспорт Steam (Steamworks.NET, AppID 480) или UnityTransport UDP 7777.
- UI: uGUI (legacy `Text`, `LegacyRuntime.ttf`). Тексты для игрока — только ключами из `Resources/Strings/*.csv` (`key,ru,en`), см. `stealth-ui`.
- Навмеш: com.unity.ai.navigation, запекается в рантайме; зона `Stairs` — только роботам с ногами, `Ladder` — только мулу.

## Ветки

Claude работает в `george`, своих веток не создаёт; в `main` — только по явной просьбе. В ветки коллеги (Саши) не коммитить
и не пушить, сливать их в `george` — по просьбе. Как коммитить — скилл `stealth-commit`.

## Скиллы — читай перед работой в подсистеме

| Скилл | Когда |
| --- | --- |
| `stealth-code` | любая правка C#: слои, сеть, именование, чек-листы (новый робот, датчик, кусок уровня, UI) |
| `stealth-verify` | проверка: тесты, пересборка ассетов, билд, автопилот, скриншоты |
| `stealth-commit` | коммит, пуш, слияние |
| `stealth-robots` | роботы, датчики, восприятие, свет для стелса, тревога, взлом, подкрепление |
| `stealth-level` | генератор дома, планировки локаций, мебель, лестницы, лазы, город, коммуникации |
| `stealth-house-features` | особенности дома, задания, двери, замки, сейфы, терминалы, всё по E |
| `stealth-sewer-car` | люки, подвал, канализация, машина, шредер и топливо, поездка между домами |
| `stealth-items` | инвентарь, классы, инструменты, крафт, предмет в руке |
| `stealth-ui` | HUD, меню, тексты и языки, клавиши, карта, пинги, метки |
| `stealth-styles` | визуальные стили, архитектура стиля, материалы, рендер |
| `stealth-humans` | люди-игроки: модели, гардероб, скелет, анимации |
| `stealth-audio` | звуки, музыка, войс-чат |
| `stealth-net` | репликация, RPC, сессия, переподключение |
| `blender-modeling` | любая модель в `Tools/Blender` |

**Новые подробности пиши в скилл своей подсистемы**, а не сюда. В CLAUDE.md — только то, что касается всех задач.

## Команды

Unity в batchmode не запустится, если проект открыт в редакторе — тогда всё через его MCP-мост (порт 6400).

```bash
Tools/dev.sh cycle                  # один запуск Unity: недостающие ассеты + все EditMode-тесты (~1 мин) + development-билд
NOASSETS=1 NOTESTS=1 Tools/dev.sh cycle   # пропустить шаги (NOPLAYER=1 — без билда, FULL=1 — полный билд)
Tools/dev.sh tests                  # компиляция + тесты; редактор открыт — идут в нём; FILTER=HousePlanTests — только эти
Tools/dev.sh assets                 # дособрать недостающие ассеты; FORCE=1 — все; REBUILD_UI=1 — UI; REBUILD=RobotGuard,Safe — эти префабы
Tools/dev.sh player                 # релизный билд Builds/Windows/USG.exe
PLANS="7:3,42:5" Tools/dev.sh plans # картинки планов домов в Logs/plans/
SEED=8 LEVEL=5 Tools/dev.sh run gallery   # автопилот: лог [AUTOTEST] + скриншоты Builds/shots/<сценарий> (SEEDS=7,42 — по окну на сид)
# Редактор открыт:
python Tools/unity_mcp.py refresh_unity '{"mode":"force","scope":"all","compile":"request"}'   # компиляция; ошибки — read_console
Tools/unity_test.sh Stealth.Tests.HousePlanTests                                               # тесты по классам
python Tools/unity_mcp.py execute_menu_item '{"menu_path":"Stealth/Build Windows Player"}'     # билд и другие пункты меню Stealth
python Tools/comfy.py "brass key icon" --count 4 --out Logs/comfy/key.png   # картинки локальным ComfyUI (--start, --cutout, --size 256x256)
Tools/unity_test.sh Stealth.Tests.SpaceAudit.WriteReport   # аудит пространства → Logs/space_audit.txt
```

Логи: `Logs/tests.log`, `Logs/build_assets.log`, `Logs/build_player.log`, `Builds/host.log`, `Builds/client*.log`.
Сценарии автопилота — `Scripts/Game/DevAutomation*.cs`; тестовые хуки — `DevFlags` (в обычной игре не используются).

## Структура `Assets/_Game`

```
Scripts/<Audio|Config|Enemies|Game|Items|Level|Player|UI|Vehicles>  игра (asmdef Stealth.Game — без Netcode и Steam)
Scripts/Net/          сетевой слой (Stealth.Net): NGO, Steam, сетевые двойники
Editor/AssetBuilder*  генератор ассетов (Stealth.Editor)      Tests/EditMode/  тесты (Stealth.Tests.EditMode)
Data/Config/          настройки: Player, Run, Level, Items, Perception, Audio      Resources/GameConfig.asset — корень настроек
Data/Styles/          визуальные стили и StyleKit                                 Data/  LevelArt, ItemVisuals, NetworkPrefabs, SoundBank
Prefabs/<Level|Props|Robots|Sensors|Items|Player|Vehicles|UI|Network|FX|Audio|StyleKit>   (конфиг робота/датчика — рядом с префабом)
Art/<Models|Materials|Audio|Textures|Sprites|Shaders|External>   модели — FBX из Tools/Blender, звуки — из Tools/Audio, CC0 — CREDITS.md
Resources/Strings/*.csv   все тексты      Scenes/Game.unity — единственная сцена
```

## Архитектура

1. **Чистые правила** — обычные C#-классы без `MonoBehaviour`, сети и физики (`RobotBrain`, `LevelGenerator`, `RunState`, `HousePlan`, `Furnisher`, `*Rules`, `*Math`…).
   Здесь живут решения и тесты; время и случайность — параметрами.
2. **Компоненты** — тонкие `MonoBehaviour`: связывают правила с вводом, физикой, сценой.
3. **Презентеры UI и звук** — `GameHUD`, `InventoryUI`, `GameMenu`, `AudioDirector`: только показывают элементы, заранее собранные в префабах.
4. **Сеть** — сборка `Stealth.Net`, единственное место с Netcode/Steam (проверяет `NetworkBoundaryTests`). У игрового компонента
   есть сетевой двойник `…Net` (PlayerNet, GameManagerNet, DetectorNet…), запросы идут через link-интерфейсы (`IPlayerLink`, `IGameLink`…),
   без сети — через `Offline*Link`. Движение игрока — owner-authoritative, остальное — сервер. Подробно — `stealth-net`.

Роботы и датчики — наследники `Detector` (`RobotController` на навмеше, `SecurityDevice` для устройств); логику от движка отвязывают
`IIntruder`, `IHackable`, `IRobotBody` (фейки в `Tests/EditMode/Fakes.cs`). Подробно — `stealth-robots`.

Цикл забега: дом (`RunPhase.Playing`) → канализация и машина (`Sewer`) → следующий дом или `Failed`. Добыча — топливо машины, склад — ящик мула.
Уровень каждый пир строит сам из общего сида (`LevelGenerator.Generate` → `ActiveLevel.Load`, только из префабов `LevelArt`); навмеш, лут и роботы — только на сервере.

**Рандом — только по сиду.** Сид забега (`GameManager.RunSeed`) → сиды домов (`LevelRandom.LevelSeed`) → отдельный поток на каждую цель
(`RandomChannel`) и объект (`LevelRandom.Next`). `UnityEngine.Random` в игровом коде запрещён (`LevelRandomTests`). Новый бросок — новый
канал или поток, чтобы не сдвигать остальные; соль — именованная константа в `Salt`, одна на поток.

## Настройки

Всё, что крутят при балансе, — в ScriptableObject-конфигах, не в коде.

| Ассет | Что внутри |
| --- | --- |
| `Data/Config/Player` | движение, прыжок, шум шагов, газ, оживление, взлом, команда (карта, пинги) |
| `Data/Config/Run` | топливо и цена поездки, штрафы, тревоги, подкрепление, машина |
| `Data/Config/Level` | размер здания, типы комнат, темнота, проёмы, охрана и ростер, лут, особенности |
| `Data/Config/Items` | цвета лута, сейфы, задания, крафт |
| `Data/Config/Perception` | зрение, свет, слух, вибрации роботов |
| `Data/Config/Audio` | громкости групп, дальность, музыка, войс-чат |
| `Prefabs/Robots|Sensors/<Префаб>Config` | числа каждого робота и устройства (в префабах чисел нет — `TuningTests`) |

- Чистые правила получают группу настроек параметром (`RunSettings`, `BuildingSettings`…), без параметра — значения по умолчанию из кода (их видят тесты).
- Компоненты берут настройки через `Tuning` (`Tuning.Level.Security`…).
- Сборщик создаёт конфиги один раз и **никогда не перезаписывает** (даже `FORCE=1`); новое поле в старом ассете получает значение по умолчанию.
- У хоста и клиентов должен быть один билд: уровень детерминирован от конфигов.

## Ассеты из кода

`Editor/AssetBuilder*.cs` создаёт материалы, спрайты, все префабы, `LevelArt`, список сетевых префабов и сцену.
Меню **Stealth → Build Missing Assets** дособирает отсутствующее (ручные правки сохраняются), **Rebuild All Assets** (`FORCE=1`) перезаписывает всё;
пути не меняются — GUID и сетевые хэши стабильны. Поменял структуру префаба в коде — пересобери и закоммить префабы вместе с кодом.
Сцена в режиме «только недостающее» не пересобирается: новый объект сцены появится после `FORCE=1`.
Модели — `Tools/Blender/*.py` (перед экспортом `meshfix.fix_objects`; после — откати неизменённые FBX:
`git diff --name-only Assets/_Game/Art/Models | xargs git checkout --`), звуки — `python Tools/Audio/generate_sounds.py`, иконки — `python Tools/Art/generate_icons.py`.

## Тесты

Весь набор EditMode — около минуты. Дома для тестов общие (`Tests/EditMode/TestHouses.cs`): каждый генерируется раз за прогон, наборы
`Corpus`, `Offices`, `Kinds`, `RoomsTest`, `First(вид, n)`; свой цикл по сидам — только если общих мало, дом, который тест меняет, — свой `LevelGenerator.Generate`.

## Правила кода (подробно — `stealth-code`)

- В рантайме **не собирать объекты кодом** (`new GameObject` + `AddComponent`, `CreatePrimitive`, процедурные текстуры): только `Instantiate` префабов из полей/`LevelArt`.
- Один `MonoBehaviour` — один файл с тем же именем.
- Логику, которую можно проверить без движка, — в чистый класс с тестом.
- Число для баланса — в группу настроек (с `[Tooltip]`), константы — только геометрия и то, что не тюнится.
- Сервер решает, клиенты показывают; в игровом коде нет `NetworkVariable`/`Rpc`/`NetworkManager`.
- `m_Name`, `s_Name`, публичное и константы — `PascalCase`; комментарии и идентификаторы — по-английски, тексты для игрока — ключами (ru + en).
- Текстов на экране — минимум: клавиши — в стопке подсказок, интерактивность — иконкой прицела.
- Найденный баг — чинить и, если можно, закрыть тестом.

## Точки стыковки для коллеги

- Войс-чат — свой стек ([Docs/VoiceChat.md](Docs/VoiceChat.md), скилл `stealth-audio`).
- Предметы: `ItemCatalog` (форма + цвет), расстановка — `LootPlanner`, в мире — `WorldItem`, инвентарь — `PlayerInventory`.
- Мета-прогрессия: итог забега `GameManager.RunTotal`, конец — `GameManager.Phase == RunPhase.Failed`.
- Новый робот, датчик или сетевой объект — чек-листы в `stealth-code`.
