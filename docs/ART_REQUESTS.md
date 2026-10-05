# Hacking Expansion: Netrun — список артов

Документ для генерации графики. Промпты на английском: так нейросети понимают точнее. Обновлено 2026-10-05 (план Фаз 2–3).

## Общий стиль (вставлять в каждый промпт)

Предметы и постройки:
```
RimWorld mod game asset, flat cel-shaded vector style, thick black outline,
muted military palette (olive green, gunmetal grey, dark charcoal),
glowing cyan accents, subtle cyberpunk tech, slightly top-down 3/4 view
as in RimWorld, centered, single object, transparent background, no text,
no shadow on ground, consistent with existing assets
```

Иконки интерфейса (как уже готовые Gizmo_*, Tab_*, Heat_*, Fixer_*):
```
game UI icon, bold simple silhouette, thick outline, neon accent on dark,
readable at 64 pixels, transparent background, no text
```

Технические требования: PNG с прозрачным фоном, без текста и тени. Предметы и постройки можно в 1024x1024, иконки 128x128. Уменьшу сам.

## Нужно сейчас (после теста терминала, 2026-10-06), по приоритету

Подключено 2026-10-06: портрет Архивариуса, Icon_Pinned (64), значки внешних нод (Node_Market/Archive/CorpVault/AbandonedServer/DarkExchange, Node_NeutralZone — скрытая нода), Program_* на кнопках программ, Action_* на кнопках нырка, Icon_Cyberspace_V2 — нода Шлюз на карте. Ещё нет: новых спрайтов построек сопроцессора и стойки (стоят Capsule_Coprocessor и AIServer_Minimal), иконок домашних нод (Node_Core/Club/Board/Archive/Server/Studio/Gateway — стоят иконки старых вкладок). Не используются: Icon_Pinned_Dim_Brass, Icon_Pinned_Diag_Dim, Server_Upgrade_Quantum.

| Файл | Что это | Размер | Идея |
|---|---|---|---|
| `NR_NetrunCoprocessor.png` | Сопроцессор Netrun: постройка 1x1 рядом с кибер-капсулой, внутри живёт ИИ | 256x256, вписать в клетку с небольшим запасом, вид сверху-спереди как у сопроцессоров Hacking Expansion | корпус в стиле HE (Optimization/Jammer coprocessor), но с прозрачным отсеком, где светится ядро ИИ; фиолетовый акцент |
| `NR_AIServerRack.png` | Серверная стойка: постройка 1x1 рядом с ИИ-сервером, +1 слот | 256x256, тот же ракурс и палитра, что у `AIServer` | узкая стойка с рядами модулей и индикаторов, в центре золотистый модуль ядра ИИ; должна выглядеть частью сервера |
| `Icon_Pinned.png` | Значок закреплённого фиксера (Клуб) | 64x64 | неоновая булавка, золото, читается на тёмном фоне (сейчас ванильная) |
| `AI_Archivist.png` + `_64` | Портрет Архивариуса | 128 и 64 | раскрытая книга или стеллаж из светящихся строк, янтарный (сейчас заглушка) |
| `Node_Core`, `Node_Club`, `Node_Board`, `Node_Archive`, `Node_Server`, `Node_Studio`, `Node_Gateway` | Иконки нод на карте терминала | 128x128 | один набор в неоновых линиях, как на макете 1-core-map (сейчас стоят иконки старых вкладок) |
| `Ext_Market`, `Ext_Archive`, `Ext_Corp`, `Ext_Abandoned`, `Ext_DarkExchange` | Значки типов внешних нод в кругах Шлюза | 64x64 | простой символ в центре круга, как на макете 5 (шестигранник с точкой и т.п.) |
| `Prog_Sword`, `Prog_Shield`, `Prog_Decoy`, `Prog_Stealth`, `Prog_Killer` | Иконки программ нырка | 64x64 | меч, щит, двойник-силуэт, глаз с чертой, череп или молния |

## Промпт: иконки домашних нод терминала (7 шт.)

Файлы: `Node_Core.png`, `Node_Club.png`, `Node_Board.png`, `Node_Archive.png`, `Node_Server.png`, `Node_Studio.png`, `Node_Gateway.png`, 128x128, прозрачный фон. Рисуются внутри квадратной плитки ноды на тёмном фоне, рамку плитки красит код, поэтому сама иконка без рамки.

Общая часть (вставлять перед каждой):
```
game UI icon for a cyberpunk network terminal, one centered symbol, thin neon line art
with soft glow, 2-3 px strokes, cyan and white lines on transparent background,
minimal detail, readable at 64 pixels, same style as a set of 7 icons, no frame,
no text, no background plate, no shadow
```

| Файл | Нода | Описание для промпта |
|---|---|---|
| `Node_Core` | Ядро (обзор следа) | `a stylized network core: a glowing hexagon chip with a pulse line (heartbeat) across it, small nodes on the edges` |
| `Node_Club` | Клуб (фиксеры, контакты) | `a contact card with a hooded figure silhouette and a small speech bubble, two connected dots like a phone link` |
| `Node_Board` | Доска (контракты) | `a document with a checkmark and a pin at the top corner, like a job posting on a board` |
| `Node_Archive` | Архив (журнал следа) | `a terminal window with stacked log lines and a small clock symbol` |
| `Node_Server` | Серверная (ИИ) | `a server rack with three slots, a small robot head glowing in the middle slot` |
| `Node_Studio` | Студия (брейнданс) | `VR braindance goggles with a sound wave or film frame passing through them` |
| `Node_Gateway` | Шлюз (нырок) | `a portal ring with a downward arrow diving into a grid plane` |

Цвет линий можно оставить единым (циан): код окрашивает рамку ноды в её цвет. Положи в папку арта, подключу сам.

## Уже есть (Art/Approved)
AIServer, AIServer_Minimal, BraindanceChip, BraindanceGoggles, Fixer_Corp / ShadowBroker / Street, Gizmo_NetTerminal / CallFixer / NetrunDive, Heat_1…5, Tab_Overview / Fixers / Contracts / AI / Braindance / Log (+ варианты), OpenCyberspace (+ Node), Preview.

## Фаза 2: ИИ и зависимости (нужно в первую очередь)

| Файл | Что это | Идея |
|---|---|---|
| `AIFragment.png` | Осколок ИИ, предмет | кристалл или шард данных, внутри пульсирует голубой узор, будто что-то думает |
| `AICoreModule.png` | Модуль ядра ИИ: очень редкое улучшение сервера (+1 слот, максимум 3) | плата или модуль-картридж с сияющим ядром, дороже и "древнее" на вид, золотистые акценты |
| `Gizmo_AwakenAI.png` | Кнопка "Пробудить ИИ" | глаз или искра, загорающаяся внутри шарда |
| `Gizmo_Isolate.png` | Кнопка "Изоляция" (air-gap) | разорванный кабель или замок на сетевом штекере |

Портреты личностей ИИ, 13 файлов, иконки 128x128. Абстрактные цифровые лица или символы, не люди. Каждой личности свой цвет акцента.

| Файл | Личность | Идея |
|---|---|---|
| `AI_DataHunter.png` | Data Hunter | цифровая ищейка, сканирующий луч, зелёный |
| `AI_Ghost.png` | Ghost | полупрозрачная маска, серо-голубой |
| `AI_Muse.png` | Muse | светящаяся фигура со звёздами, фиолетовый |
| `AI_Predator.png` | Predator | хищный цифровой зверь, красные глаза |
| `AI_Warden.png` | Warden (Страж) | щит с глазом, синий |
| `AI_Oracle.png` | Oracle (Оракул) | всевидящий глаз в круге, белый или золотой |
| `AI_Forge.png` | Forge (Кузнец) | шестерня и молот из линий, оранжевый |
| `AI_Siren.png` | Siren (Сирена) | лицо с волнами звука, бирюзовый |
| `AI_Trickster.png` | Trickster (Трикстер) | маска-шут, разделённая на два цвета |
| `AI_Echo.png` | Echo | размытый пиксельный силуэт человека, "цифровой призрак", бледный |
| `AI_Doc.png` | Doc (Врач) | цифровой крест или кардиограмма вокруг силуэта мозга, мятно-зелёный |
| `AI_Archivist.png` (нет, стоит заглушка) | Archivist (Архивариус) | раскрытая книга или стеллаж из светящихся строк, янтарный |
| `AI_Seed.png` | Seed (Зерно) | светящееся семя или росток из линий кода, нейтральный белый |

## Фаза 3: брейнданс и атаки

| Файл | Что это | Идея |
|---|---|---|
| `BraindanceChip_Black.png` | Чёрный брейнданс-чип | тот же чип, что BraindanceChip, но чёрный корпус, красный экран, трещина или череп вместо мозга |
| `BraindanceGoggles_south.png`, `_north.png`, `_east.png` | Очки на голове пешки (3 вида) | только очки, без головы, на уровне глаз пешки RimWorld; north — виден ремешок сзади, east — профиль. Лучше рисовать поверх контура ванильного головного убора |
| `Gizmo_WatchBD.png` | Кнопка "Смотреть BD" | очки с волной |
| `Gizmo_RecordBD.png` | Кнопка "Записать BD" | красная точка записи и мозг |

## Интерфейс

| Файл | Что это | Идея |
|---|---|---|
| `Icon_Pinned.png` | Значок закреплённого фиксера (рядом с именем во вкладке «Фиксеры») | булавка или канцелярская кнопка в неоновом стиле, 64x64, читается на тёмном фоне. Пока стоит ванильная булавка |

## Для Steam
| Файл | Что это | Размер |
|---|---|---|
| `ModIcon.png` | Иконка мода в списке модов | около 128x128 |

## Куда класть
Присылай в тред или клади в `Art/Approved`. Разложу по папкам, уменьшу и подключу сам.
