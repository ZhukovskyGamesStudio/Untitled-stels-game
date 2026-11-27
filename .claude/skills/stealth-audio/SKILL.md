---
name: stealth-audio
description: "Звук USG: откуда звуки (CC0-записи, recorded.py, generate_sounds.py), AudioDirector и фасад Sfx, звук роботов (моторы, голос, шаги по касанию ступни, глушение через перекрытия), звуки работы руками, мебели и терминалов, шаги по поверхностям, лестница, звуки людей (газ, заложник, прыжки), войс-чат (StealthVoice.dll, VoiceCapture/VoicePlayback/VoiceHub, рация). Используй перед правкой звуков, музыки или голоса."
---

# stealth-audio

Подробности подсистемы, вынесенные из CLAUDE.md. Общие правила кода — скилл `stealth-code`, проверка — `stealth-verify`.

## Откуда звуки

Звуки — из CC0-записей, реалистично: `python -I Tools/Audio/cc0_sources.py` (скачать наборы OpenGameArt в `Tools/Audio/cache`),
`Tools/Audio/decode_cc0.py` (в Blender: его `aud` читает ogg/mp3/flac), `python Tools/Audio/import_cc0_sounds.py` (рецепты клипов — `recorded.py`,
мотор и гудок машины; список — `Art/Audio/CREDITS.md`; `--recorded` — только рецепты); остальное (музыка, интерфейс, часть роботов) —
`python Tools/Audio/generate_sounds.py` (детерминированно, с сидом; имена из `recorded.REPLACED` не пишет), послушать всё — `python Tools/Audio/sound_board.py`,
голос роботов — `python Tools/Audio/generate_voices.py` (Windows, ru-RU голос). Наборы не из CC0 (`cc0_sources.LICENCES`, сейчас — настоящий енот, CC-BY) в CREDITS.md подписаны автором и лицензией. Скачать только нужные наборы — `cc0_sources.py <набор> …`; нарезать только свои рецепты — `import_cc0_sounds.py --only Mule/` (CREDITS.md сливается, остальные наборы не нужны). Звуки Линуса — `stealth-robots`.

## Звук роботов

**Звук роботов**: у каждого вида свой мотор (`AssetBuilder.MotorFor`) и высота голоса (`AudioRules.VoicePitch`); шаги шагающих — по
касанию ступни (`ProceduralLegs.FootDown` → `AudioDirector.OnFootDown`: охранник, ищейка, паук — свои клипы); всё, что звучит через
перекрытия, глохнет (`AudioDirector.Muffle` → `AudioRules.FloorFactor`, `AudioMix.ThroughFloor`; петли `LoopSound` тоже).
Шагов нет у сидящего в машине и на лестнице. Короткие звуки предзагружаются со сценой (`AssetBuilder.Audio`: иначе плеер не создавал FMOD-звук). Громкость из настроек применяется сразу (вес кроссфейда отдельно от громкости), фон исследования тихий (`AudioMix.ExploreMusic`),
двери — `AudioMix.Doors`.

## Звуки работы руками

**Звуки работы руками** — пока держится E: стук клавиш при взломе, тики отмычки у замка, трещотка отвёртки у решётки (`PlayerController.WorkSounds`, клипы `work_*` — наборами, `SoundBank.Any`; рецепты — `recorded.py`); звучат, только пока прогресс удержания растёт (`PlayerController.m_WorkWas`: прогресс сохраняется, когда E отпущена, и виден при наведении — это не работа); снятый замок двери щёлкает на каждом пире (`AutoDoor`); навесной замок распахнутой двери (её открыл робот) прячется, а не висит в проёме (`AutoDoor.ShowLock`).
**Звуки мебели** — `Storage.Sound` (дерево / сталь — `StorageMath.Steel`), сундук обыскан, решётка снята, выключатели — по реплицированному состоянию на каждом пире; то, что знает только сервер (верстак, лечение, кофе, глушилка), — `GameManager.SoundServer(GameSound)` → `IGameLink.BroadcastSound`. Клипы работы руками — наборами (`SoundBank.Any`).
**Работа за терминалом кончается звуком** (на каждом пире, по реплицированному состоянию, не при первом взгляде): терминал двери склада — «доступ открыт» у терминала и засовы у самой двери (`SecurityTerminal.Sound`, `terminal_granted`), терминал взлома — подтверждение на каждый потраченный заряд (`terminal_hack_done`), пульт хранилища — защёлка (`console_solved`), последний из трёх — три засова у двери сейфа (`vault_unlocked`, `VaultConsole`).

## Шаги и лестница

**Шаги по поверхностям** — `StepSurfaces` (бетон, плитка, дерево, мягкое — линолеум и резина, ковёр, металл, мокрое — канализация): луч вниз, на полу дома — отделка комнаты (`StyleDecor.FloorMaterial`), на лестнице — её конструкция, в коробах — металл; по четыре клипа на поверхность, все одинаковой громкости; роботам поверхность безразлична (`FootstepNoise`).
**Лестница звучит по телу**: ботинок по перекладине — в момент, когда ступня лазающего на неё встала, мягкий хлопок ладони — когда рука сомкнулась на своей (`HumanMath.ClimbContacts` по фазе дубля Climb: ступня — на 0,25 и 0,75 цикла, рука напротив — на 0,31 и 0,81; вниз — те же моменты, дубль идёт назад; фаза — `HumanRig.ClimbPhase`: та самая скорость, с которой дубль играет аниматор, по кадрам, `PlayerController.ClimbPhase`; не по метрам — сглаживание скорости уводило звук от ноги на треть цикла); два набора клипов `SoundBank.LadderFoot/LadderHand` (`ladder_foot_0..3`, `ladder_hand_0..3` из `recorded.py`), громкости — `AudioMix.LadderFeet/LadderHands/LadderDown`; на месте — тишина; роботы по-прежнему слышат шум на каждой перекладине (`AudioRules.Rungs`, `NoiseSettings.LadderRadius`, владелец шлёт шум из `UpdateBodyPose`).

## Звуки людей

**Звуки людей** — `PeopleSounds`: в газе сначала тяжёлое дыхание, потом кашель всё громче и чаще (`PlayerController.GasShare`, реплицирует `PlayerNet`,
клипы `gas_breath`/`gas_cough_0..2`; высота голоса — по отделу, `PeopleSounds.VoicePitch`); заложник окликает игрока, который сам подошёл и видит клетку (глазами тела, не через камеру терминала) (реплики `Voices/hostage_*` из
`generate_voices.py`); прыжки и приземления — `AirState` (подъём на мебель и дрожь на краю — не прыжок).

## Войс-чат

- Войс-чат — сделан, свой стек ([Docs/VoiceChat.md](Docs/VoiceChat.md), там же исследование и дорожная карта, в т.ч. покупка Dissonance):
  плагин `Plugins/x86_64/StealthVoice.dll` (Opus + RNNoise от Xiph, `Tools/Voice/build_native.sh`), чистые правила `Scripts/Audio/Voice/*`
  (`VoiceGate`, `VoiceGain`, `VoiceJitter`, `VoiceAcoustics`, `VoiceNoise`, `VoicePacket`), микрофон — `VoiceCapture` на префабе AudioDirector,
  голос в мире — `VoicePlayback` (ребёнок `Voice` префаба игрока: плоский клип × голос в `OnAudioFilterRead`, потом Low-pass/Echo/Reverb),
  маршрут — `VoiceHub` (сервер делает шум `NoiseSystem.EmitServer` по сырому уровню и пересылает), сеть — `Net/VoiceNet` (named messages, Unreliable);
  настройки — `Data/Config/Audio` → `Voice`, выбор игрока — `VoicePrefs` (вторая колонка страницы настроек), кнопка — `GameAction.PushToTalk` (CapsLock).
- Ретранслятор (`LureRelay`) играет настоящие слова игроков: пока в доме есть ретранслятор (`VoiceHub.RememberServer`), сервер
  держит в памяти последние секунды речи каждого (`VoiceMemory`, кадры как пришли; очищается в начале дома), на зов берёт 0.8–2.5 с
  речи игрока дальше `GameManager.LureNear` (ping — от его имени), шлёт всем одним надёжным сообщением `stealth.voice.lure`
  (`VoicePacket.WriteLure`), каждый пир играет его в `VoicePlayback` ретранслятора (без владельца, + high-pass и distortion — «динамик»).
  Никто не говорил — старый синтетический `LureCall`. Числа — `RelayProfile` (SnippetMin/Max, RememberSeconds, ForgetAfter).
