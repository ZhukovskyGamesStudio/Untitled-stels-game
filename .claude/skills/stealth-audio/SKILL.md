---
name: stealth-audio
description: "Звук USG: AudioDirector и фасад Sfx, звук роботов (моторы, голос, шаги по касанию ступни, глушение через перекрытия), звуки работы руками, звуки людей (газ, заложник, прыжки), войс-чат (StealthVoice.dll, VoiceCapture/VoicePlayback/VoiceHub, рация). Используй перед правкой звуков, музыки или голоса."
---

# stealth-audio

Подробности подсистемы, вынесенные из CLAUDE.md. Общие правила кода — скилл `stealth-code`, проверка — `stealth-verify`.

## Звук роботов

**Звук роботов**: у каждого вида свой мотор (`AssetBuilder.MotorFor`) и высота голоса (`AudioRules.VoicePitch`); шаги шагающих — по
касанию ступни (`ProceduralLegs.FootDown` → `AudioDirector.OnFootDown`: охранник, ищейка, паук — свои клипы); всё, что звучит через
перекрытия, глохнет (`AudioDirector.Muffle` → `AudioRules.FloorFactor`, `AudioMix.ThroughFloor`; петли `LoopSound` тоже).
Шагов нет у сидящего в машине и на лестнице. Короткие звуки предзагружаются со сценой (`AssetBuilder.Audio`: иначе плеер не создавал FMOD-звук). Громкость из настроек применяется сразу (вес кроссфейда отдельно от громкости), фон исследования тихий (`AudioMix.ExploreMusic`),
двери — `AudioMix.Doors`.

## Звуки работы руками

**Звуки работы руками** — пока держится E: стук клавиш при взломе, тики отмычки у замка, трещотка отвёртки у решётки (`PlayerController.WorkSounds`, клипы `work_*` из `generate_sounds.py`); звучат, только пока прогресс удержания растёт (`PlayerController.m_WorkWas`: прогресс сохраняется, когда E отпущена, и виден при наведении — это не работа); снятый замок двери щёлкает на каждом пире (`AutoDoor`); навесной замок распахнутой двери (её открыл робот) прячется, а не висит в проёме (`AutoDoor.ShowLock`).

## Звуки людей

**Звуки людей** — `PeopleSounds`: в газе сначала тяжёлое дыхание, потом смех всё громче и чаще (`PlayerController.GasShare`, реплицирует `PlayerNet`,
клипы `gas_breath`/`gas_laugh_0..2` из `generate_sounds.py`); заложник окликает игрока, который сам подошёл и видит клетку (глазами тела, не через камеру терминала) (реплики `Voices/hostage_*` из
`generate_voices.py`); прыжки и приземления — `AirState` (подъём на мебель и дрожь на краю — не прыжок).

## Войс-чат

- Войс-чат — сделан, свой стек ([Docs/VoiceChat.md](Docs/VoiceChat.md), там же исследование и дорожная карта, в т.ч. покупка Dissonance):
  плагин `Plugins/x86_64/StealthVoice.dll` (Opus + RNNoise от Xiph, `Tools/Voice/build_native.sh`), чистые правила `Scripts/Audio/Voice/*`
  (`VoiceGate`, `VoiceGain`, `VoiceJitter`, `VoiceAcoustics`, `VoiceNoise`, `VoicePacket`), микрофон — `VoiceCapture` на префабе AudioDirector,
  голос в мире — `VoicePlayback` (ребёнок `Voice` префаба игрока: плоский клип × голос в `OnAudioFilterRead`, потом Low-pass/Echo/Reverb),
  маршрут — `VoiceHub` (сервер делает шум `NoiseSystem.EmitServer` по сырому уровню и пересылает), сеть — `Net/VoiceNet` (named messages, Unreliable);
  настройки — `Data/Config/Audio` → `Voice`, выбор игрока — `VoicePrefs` (вторая колонка страницы настроек), кнопка — `GameAction.PushToTalk` (CapsLock).
