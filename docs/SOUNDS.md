# Звуки Netrun: 5 звуковых пакетов

Звуки синтезированы скриптом `tools/gen_sounds.py` (numpy и ffmpeg): ни одного чужого сэмпла, лицензий нет. Облачная сессия их не слышит, поэтому характер пакетов оценивает Maxim на слух; всё меняется параметрами `PACKS` и функциями `amb_*` в скрипте, потом `python3 tools/gen_sounds.py [Пакет] [часть имени]`.

**v3 (2026-10-07, по отзыву «пакеты похожи, эмбиенты не слышно, нужен киберпанк»).** У каждого пакета теперь свой голос и свои мотивы (не просто смена тембра): Clean стеклянное FM-электропиано, Minimal одиночные синусные пипы, Neon пилообразные щипки с гейт-эхо и малыми арпеджио, RetroDeck 8-бит (пульс 25%, bitcrush, ступенчатые свипы, реле), Deep колокола и смычковые дроны с длинным хвостом. Эмбиенты переписаны: суперсо-пады с плавающим фильтром, суб-бас, далёкие сирены, эхо-колокола, у Neon синтвейв-бас и арпеджио, у RetroDeck чип-бас и модемные трели. Уровень эмбиентов около -20 дБ RMS, громкость SoundDef 55/55/60/45%. Шум по-прежнему почти нулевой.

## Пакеты (выбор в настройках мода)

| Id | Название (ru) | Характер |
|---|---|---|
| `Clean` | Чистый | Неоновая ночь: тёплые аналоговые пады, стеклянные электропианные сигналы, редкая сирена вдали. |
| `Minimal` | Минимализм | Холодная комната: короткие чистые пипы, тихий дрон и далёкий колокол, всё очень скупо. |
| `Neon` | Неон | Синтвейв: суперсо-пады, бас-арпеджио, пилообразные щипки и гейт-эхо. |
| `RetroDeck` | Ретро-дека | Кибердека 80-х: чип-звуки в 8 бит, реле, модемные трели, сонар. |
| `Deep` | Глубина | Тяжёлая бездна: смычковые дроны, огромные колокола, гонги и длинное эхо. |

Файлы: `Sounds/Netrun/<Пакет>/{UI,Dive,AI,Ambient}/...ogg`, определения: `Defs/Sounds.xml` (160 SoundDef). Имя определения: `NR_<Пакет>_<Папка>_<Имя>`, например `NR_Neon_UI_Click`, `NR_RetroDeck_Ambient_Terminal`.

Память: файлы моно (одиночные 32 кГц, эмбиенты 22.05 кГц). RimWorld загружает аудио мода при старте, поэтому все пять пакетов вместе занимают около 26 МБ декодированной памяти (около 5 МБ на пакет). Если это много, оставим меньше пакетов.

## Что сделать в коде (сессия на ПК)

**Сделано 2026-10-07:** `Source/Terminal/NetrunSound.cs`, группа «Звук» в настройках. Серверный гул идёт не через `soundAmbient` (в 1.6 такого поля нет), а позиционным sustainer-ом, пока есть питание. Ниже исходный план.

1. Настройка мода «Звуковой пакет»: выпадающий список из Clean, Minimal, Neon, RetroDeck, Deep и пункта «Выкл.»; по умолчанию Clean. Рядом ползунки «Громкость звуков интерфейса» и «Громкость эмбиента» (0–100%, по умолчанию 60%). Названия и описания пакетов в Keyed (en и ru, таблица выше).
2. Класс `NetrunSound`: `Play(string id)` ищет `DefDatabase<SoundDef>.GetNamedSilentFail("NR_" + пакет + "_" + id)` (id вида `UI_Click`, `Dive_Success`, `AI_Awaken`) и играет `PlayOneShotOnCamera()`; кэшировать найденные def в словаре, сбрасывать при смене пакета. `Ambient(string id)` то же для циклов через `TrySpawnSustainer(SoundInfo.OnCamera(MaintenanceType.PerFrame))`, `Maintain()` каждый кадр, `End()` при закрытии окна или смене экрана; при смене пакета перезапустить.
3. Громкость: множить через `SoundInfo.volumeFactor` (поле `SoundInfo`) на настройку; 0 = не играть.
4. `NR_<Пакет>_Ambient_Server` назначать постройке `NR_AIServer` в `<building><soundAmbient>`; пакеты переключаются на лету неудобно, поэтому для сервера достаточно значения Clean, а выбранный пакет применять к нему только после перезагрузки (или оставить серверный гул общим). Решение на усмотрение сессии на ПК.
5. Привязка по событиям (одинаково для всех пакетов): клик и hover не чаще раза в 80 мс, `UI_Type` на каждый напечатанный символ (не более 20 раз в секунду), `UI_Glitch` только при включённых анимациях; эмбиент: терминал и разделы Core, Архив, Доска, Клуб, Студия, Серверная это `Ambient_Terminal`, Шлюз это `Ambient_Cyberspace`, нырок это `Ambient_Dive` с плавной сменой.
6. Если игра ругнётся в логе на поля SoundDef, поправить по ванильному SoundDef из Core.

## Одиночные звуки (в каждом пакете)

| Id | Когда | Громкость |
|---|---|---|
| `UI_Click` | button press | 50% |
| `UI_Hover` | hover over a button or node (not more often than once per 80 ms) | 30% |
| `UI_Type` | one key tick while text is typed in | 38% |
| `UI_Confirm` | confirm: contract accepted, deal made, scrub done | 55% |
| `UI_Deny` | refusal: unavailable action, failed deal | 55% |
| `UI_Tab` | switch of a tab or a screen | 50% |
| `UI_Open` | the terminal window opens | 55% |
| `UI_Close` | the terminal window closes | 50% |
| `UI_Glitch` | short glitch (bottom bar on screen change, node frames); only with animations on | 45% |
| `UI_Notify` | new Archive entry, letter from the net | 50% |
| `UI_Alert` | Heat reached Target or Red zone, counter-hack, raid warning | 50% |
| `UI_HeatUp` | Heat level went up | 55% |
| `UI_HeatDown` | Heat level went down, scrub done | 50% |
| `UI_Scan` | node search, Scan action in a dive | 50% |
| `UI_Download` | Download action, cyberdata received | 45% |
| `UI_Hack` | Hack action of a dive passes a gate | 50% |
| `Dive_Start` | a dive starts | 60% |
| `Dive_Success` | clean exit from a dive | 60% |
| `Dive_Fail` | dive failed or aborted | 60% |
| `Dive_ICE` | hit from ICE or Black ICE | 65% |
| `AI_Awaken` | an AI wakes up | 60% |
| `AI_Whisper` | AI events with a message, wild AI in memory | 40% |
| `AI_Request` | an AI asks for something | 50% |
| `AI_Fulfilled` | request fulfilled, trust grows | 55% |
| `AI_Angry` | AI is hurt, request failed, escape | 55% |
| `AI_Feed` | feeding an AI | 50% |
| `AI_Chime` | small AI event, service done | 45% |
| `AI_Echo` | Echo AI (a dead colonist) speaks | 45% |

## Эмбиент-циклы (в каждом пакете)

| Id | Когда | Громкость |
|---|---|---|
| `Ambient_Terminal` | terminal open (Core, Archive, Board, Club, Studio, server room) | 28% |
| `Ambient_Cyberspace` | Gateway (cyberspace map) | 28% |
| `Ambient_Dive` | inside a dive | 30% |
| `Ambient_Server` | AI server building (Building.soundAmbient, positional) | 22% |

## Правки 2026-10-07 (по просьбе Maxim)
- Neon, `UI/Close`: вместо падающего пилообразного свипа теперь «выключение»: четыре плавно падающих щипка и мягкий саб-удар.
- Clean и Deep: из эмбиентов убраны колокольчики (Clean: случайные в терминале, мелодия в киберпространстве теперь глухой щипок без звона, та же партия; Deep: все колокола). Остальное не менялось. Засев эмбиентов теперь детерминированный (crc32 вместо hash).
- Эмбиент должен играть один непрерывный на всё окно терминала (код: сессия на ПК), основой берётся Ambient/Cyberspace.

## Звуки действий хака (2026-10-07 поздно вечером)
В каждом пакете `Dive/Move` (шаг по ноде, тихо, питч ±3%), `Dive/Back` (тот же шаг задом наперёд и ниже), `Dive/HackFail` (сорванный взлом: диссонанс и цифровой хрип), `Dive/Exit` (отключение: падающий каскад и сухой щелчок штекера). SoundDef `NR_<Pack>_Dive_Move/Back/HackFail/Exit`. Генератор теперь сам пишет ручные правки сессии на ПК в SoundDef эмбиентов (плавный вход и выход 1.6 с, тихий позиционный гул сервера).
