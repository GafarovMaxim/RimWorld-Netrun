# Звуки Netrun

Звуки синтезированы скриптом `tools/gen_sounds.py` (numpy и ffmpeg): ни одного чужого сэмпла, лицензий нет. Облачная сессия их не слышит, поэтому громкости и характер нужно проверить на слух; всё меняется параметрами в скрипте и в `Defs/Sounds.xml`.

Файлы: `Sounds/Netrun/{UI,Dive,AI,Ambient}/*.ogg` (около 1.6 МБ), определения: `Defs/Sounds.xml`. Пересборка: `python3 tools/gen_sounds.py` (опция `--wav ПАПКА` оставит wav). Фильтр по имени: `python3 tools/gen_sounds.py glitch`.

## Что подключить в коде (сессия на ПК)

Одиночные: `NR_UI_Click.PlayOneShotOnCamera()`. Эмбиенты: `NR_Amb_*.TrySpawnSustainer(SoundInfo.OnCamera(MaintenanceType.PerFrame))` и `sustainer.Maintain()` каждый кадр, пока окно открыто; закрыть: `sustainer.End()`. Эмбиент меняется по экрану: терминал и Core, Архив, Доска, Клуб, Студия, Серверная: `NR_Amb_Terminal`; Шлюз: `NR_Amb_Cyberspace`; нырок: `NR_Amb_Dive`. При смене экрана плавно гасить старый и поднимать новый (End() у sustainer затухает сам).
- `NR_Amb_Server` назначить как `<soundAmbient>` постройки `NR_AIServer` (в `<building>`), только когда питание есть.
- Реже: клик и hover не чаще раза в 80 мс, `NR_UI_Type` на каждый напечатанный символ в эффекте печати, но не более 20 раз в секунду.
- Настройки мода: «Звуки терминала» (0–100%, по умолчанию 60%, выключатель) умножает громкость; отдельно «Эмбиент терминала». Глитч-звук играть только при включённых анимациях.
- Если RimWorld ругнётся в логе на поле SoundDef, поправить по ванильному примеру (`Sounds/` ванили и `SoundDef` в Core/Defs/SoundDefs).

## Список

| SoundDef | Когда | Громкость |
|---|---|---|
| `NR_UI_Click` | button press | 50% |
| `NR_UI_Hover` | hover over a button or node (rate-limit: not more than once per 80 ms) | 30% |
| `NR_UI_Type` | one key tick while text is being typed in | 38% |
| `NR_UI_Confirm` | confirm: contract accepted, deal made, scrub done | 55% |
| `NR_UI_Deny` | refusal: unavailable action, failed deal | 55% |
| `NR_UI_Tab` | switch of a tab or a screen | 50% |
| `NR_UI_Open` | the terminal window opens | 55% |
| `NR_UI_Close` | the terminal window closes | 50% |
| `NR_UI_Glitch` | short glitch (bottom bar on screen change, node frames); only with animations on | 45% |
| `NR_UI_Notify` | new Archive entry, letter from the net | 50% |
| `NR_UI_Alert` | Heat reached Target or Red zone, counter-hack, raid warning | 50% |
| `NR_UI_HeatUp` | Heat level went up | 55% |
| `NR_UI_HeatDown` | Heat level went down, scrub done | 50% |
| `NR_UI_Scan` | node search, Scan action in a dive | 50% |
| `NR_UI_Download` | Download action, cyberdata received | 45% |
| `NR_UI_Hack` | Hack action of a dive passes a gate | 50% |
| `NR_Dive_Start` | a dive starts | 60% |
| `NR_Dive_Success` | clean exit from a dive | 60% |
| `NR_Dive_Fail` | dive failed or aborted | 60% |
| `NR_Dive_ICE` | hit from ICE or Black ICE | 65% |
| `NR_AI_Awaken` | an AI wakes up | 60% |
| `NR_AI_Whisper` | AI events with a message, wild AI in memory | 40% |
| `NR_AI_Request` | an AI asks for something | 50% |
| `NR_AI_Fulfilled` | request fulfilled, trust grows | 55% |
| `NR_AI_Angry` | AI is hurt, request failed, escape | 55% |
| `NR_AI_Feed` | feeding an AI | 50% |
| `NR_AI_Chime` | small AI event, service done | 45% |
| `NR_AI_Echo` | Echo AI (a dead colonist) speaks | 45% |
| `NR_Amb_Terminal` (цикл) | terminal open: low hum, fan, faint data pings | 28% |
| `NR_Amb_Cyberspace` (цикл) | Gateway (cyberspace map): airy pad, sparse pings | 28% |
| `NR_Amb_Dive` (цикл) | inside a dive: tense drone, rhythmic throb, riser | 30% |
| `NR_Amb_Server` (цикл, на постройке) | гул ИИ-сервера, слышно рядом | 22% |

## Что дальше

- Проверка на слух: Maxim отмечает, какие звуки резкие, тихие, лишние. Исправлю параметрами и перегенерирую.
- Можно добавить звуки: события киберпространства (шторм, контрвзлом), записи брейнданса, звонок фиксера, каждому архетипу ИИ свой голос.
