---
name: stealth-commit
description: How to commit and push in the «Айтишники: вылазка» repo — the three-branch rule (main, george, sasha), Russian commit message format with a tag and area, what to stage (prefabs and .meta with code, never Builds/Logs/Library), pre-commit checks and when pushing to main is allowed. Use whenever committing, pushing or merging in this project.
---

# Как коммитить

## Ветки

- В репозитории ровно три ветки: `main`, `george`, `sasha`. Новые ветки (feature/…, fix/…) **не создавать**.
- Claude работает в `george`. `sasha` — ветка коллеги, в неё не коммитить и не пушить.
- В `main` — только по явной просьбе пользователя (merge `george` → `main`, без force-push).
- Перед работой: `git status` чистый, `git pull --ff-only` своей ветки.

## Перед коммитом

1. `Tools/dev.sh tests` — всё зелёное. Если менялись ассеты или геймплей — ещё сборка и сценарий (скилл `stealth-verify`).
2. Код, который меняет структуру префабов, коммитится **вместе** с пересобранными префабами (`FORCE=1 Tools/dev.sh assets`).
3. Каждому новому файлу и папке в `Assets/` — свой `.meta`. Перемещать ассеты только `git mv` вместе с `.meta` (сохраняет GUID и ссылки).
4. Не коммитить: `Builds/`, `Logs/`, `Library/`, `Temp/`, `UserSettings/`, `*.csproj`, `*.slnx`, скриншоты автотестов.

## Сообщение

Одна строка-заголовок по-русски, потом при необходимости пустая строка и подробности:

```
<Тег> (<область>): <что сделано, через запятые> — <зачем / результат>
```

- Тег — короткий номер шага серии работ: `R20`, `A3`, `Итерация 4`; для разовой правки — `Фикс`, `Тесты`, `Доки`.
- Область: `управление`, `интерфейс`, `уровни`, `роботы`, `архитектура`, `ассеты`, `сеть`, `тесты`, `сборка`, `документация`.
- Пиши, что изменилось для игры или разработчика, а не «обновил файлы».
- В конце — строка соавторства, которую задаёт система (сейчас `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`).

Пример:

```
A2 (архитектура): общий базовый класс Detector для роботов и датчиков, RobotBrain отдельно от NavMeshAgent — логика роботов покрыта тестами

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

Многострочное сообщение передавай через heredoc или файл (`git commit -F file`), чтобы не ломались кавычки.

## После

- `git push origin george`. Проверить `git status` — рабочее дерево чистое, ветка не опережает origin.
- Крупную серию изменений (несколько коммитов) — кратко описать пользователю: что сделано, что проверено, что нет.
