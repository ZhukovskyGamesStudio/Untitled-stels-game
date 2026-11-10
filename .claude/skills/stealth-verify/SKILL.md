---
name: stealth-verify
description: How to verify a change in the «Айтишники: вылазка» Unity project without the editor — EditMode tests, rebuilding generated assets, the Windows player build, autopilot scenarios with [AUTOTEST] logs and screenshots, two-instance network runs, and the usual pitfalls. Use after any code or asset change, before committing, and when asked to "check", "test", "screenshot" or "build".
---

# Как проверять изменения

Проверка — это код **и** глаза: тесты + прогон билда с автопилотом + просмотр скриншотов.
Все команды — из корня репозитория в Git Bash. Unity должна быть закрыта (иначе batchmode падает: проект занят).

## 1. Тесты

```bash
Tools/dev.sh tests
```
Внизу вывода — `tests: total=… passed=… failed=…` и упавшие с сообщениями; выше — ошибки компиляции `error CS…`.
Новое правило → новый тест в `Assets/_Game/Tests/EditMode` (фейки — `Fakes.cs`). `PrefabIntegrityTests` проверяют, что в префабах нет пустых ссылок и Missing Script.

## 2. Ассеты (если менялись префабы, материалы, UI, сцена или их сборка)

```bash
FORCE=1 Tools/dev.sh assets   # всё из кода заново
Tools/dev.sh assets           # только недостающее
```
Успех — строка `[Builder] Assets built`. После — снова `Tools/dev.sh tests`.

## 3. Билд и автопилот

```bash
Tools/dev.sh player                          # → [Builder] Player build: Succeeded
SEED=42 Tools/dev.sh run ui                  # один хост
Tools/dev.sh run full 1                      # хост + клиент по сети (UDP)
SEED=8 LEVEL=5 Tools/dev.sh run gallery      # все роботы; HOSTILE=1 — с проверкой реакции; KINDS=Guard,Drone — выборочно
```
Переменные: `SEED`, `LEVEL`, `PEACEFUL=1`, `HOSTILE=1`, `KINDS`, `RESW`/`RESH` (разрешение).
Вывод: строки `[AUTOTEST <сценарий>]` с измерениями, сводка исключений из логов, список скриншотов в `Builds/shots/<сценарий>/`.

Какой сценарий смотреть:
| Изменение | Сценарии |
| --- | --- |
| движение, камера | `move` |
| HUD, инвентарь, склад у люка, подсказки | `ui` (лог `stash:`), `menu` |
| звук | `ui` (лог `audio: music …, one-shots …`); на слух — только руками в билде |
| здание, лестницы, лазы, двери | `floors`, `rooms`, `vent`, `perf` |
| роботы и датчики | `gallery` (+`HOSTILE=1`), `hack`, `radio`, `lasers`, `hunter` |
| сеть, общий цикл, склад по сети | `full 1` (у клиента `stash: in -> …; back -> …`), `coop 1` |

## 4. Смотреть скриншоты

Открывай PNG (Read) и проверяй глазами: элемент на месте, текст не обрезан, нет розовых материалов, робот виден и отличим,
подсказка соответствует ситуации. Числа из `[AUTOTEST]` сверяй с ожиданием (например, `climb: … reached=True`).

## Подводные камни

- Нет `tests.xml` / `Scripts have compiler errors` — читай `error CS` в выводе или `Logs/tests.log`.
- Unity MCP может не подключаться (редактор открыт на другом проекте) — используй batchmode-команды выше.
- Сценарий завис — у `run` нет таймаута; запускай через `timeout 300 Tools/dev.sh run …`.
- Скриншот «не туда» не всегда баг игры: сначала проверь, куда автопилот поставил камеру (`DevAutomation*.cs`).
- Мебель/окна: после правки раскладки гоняй тесты — `FurnitureDoesNotClip` пишет пары «что x во что» с координатами.
- Steam не проверить на одном аккаунте: для сетевых проверок — локальный UDP (`run … 1`).
