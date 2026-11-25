---
name: stealth-net
description: "Сетевой слой USG подробно: сетевые двойники …Net и Bind/PushState/ApplyReplicated, link-интерфейсы и Offline*Link, IWorldState, фасад Session, кто authoritative, переподключение (GameManager.Reconnect). Используй перед правкой репликации, RPC, сессии, лобби или переподключения."
---

# stealth-net

Подробности подсистемы, вынесенные из CLAUDE.md. Общие правила кода — скилл `stealth-code`, проверка — `stealth-verify`.

## Сеть

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
  и вызывает серверные обработчики игрового компонента (`OnReviveRequest`, `PickupServer`…). Без двойника (тесты, офлайн) запросы идут через `Offline*Link` (`Scripts/Game/OfflineLinks.cs`) прямо в серверные обработчики:
  код, который шлёт, не различает эти случаи (`Link` вместо `m_Link`). Дальность на сервере — одно правило `ServerReach` (запас на отставание позиции).
- Состояние дома для сцены — `IWorldState` (`World.State`, ставит `GameManager.Bind`): открытое по ключам `OpenKeys`, лифт, часы; статических хуков у компонентов нет.
- `Session` — фасад сессии: `IsServer`, `ServerTime`, `Spawn`/`Despawn` (сетевые префабы), `Launcher` (меню: хост, подключение, Steam). Статусы в меню — только наши слова из `net.csv` («Стим недоступен», «Не удалось создать лобби»): технические причины (`SteamManager.Error`, код лобби, причина разрыва) — только в лог.
  Без запущенной сессии этот пир — сам себе сервер, время — локальное, спавн — обычный `Instantiate`.
- Движение игрока — owner-authoritative (`NetworkTransform` Owner), здоровье, лут и роботы — server-authoritative.
- Правила забега (топливо и цена поездки, штрафы, тревоги, охотник, конец уровня) — чистый класс `RunState`.

## Переподключение

кто вышел (`PlayerController.Identity` — Steam id или id компьютера, `ISessionLauncher.LocalPlayerId`), вернётся с рюкзаком туда, где стоял (тот же дом),
иначе — к хосту (`IPlayerLink.PlaceOwner` → `PlayerController.PlaceFromServer`). Выбывший видит рюкзак того, за кем смотрит (`InventoryUI`).
