# Промпты для генерации арта (план v3)

Файл для Maxim: отдать нейросети-генератору картинок. Промпты на английском, пояснения на русском. Создан 2026-10-28 по `docs/ART.md` (правила стиля, размеры, раздел 4 «Арт для плана баланса v3»).

## Как пользоваться
1. Выбрать ассет ниже. Взять **общий стиль** его манеры (Г, В или А), приклеить **промпт ассета** после него, **негативный промпт** в поле negative (если генератор его поддерживает; если нет, дописать в конец как «Avoid: …»).
2. Генерировать квадрат 1024×1024 (или больше), прозрачный фон. Если генератор не умеет прозрачность: сплошной фон **#FF00FF** (пурпурный), его вырезаем при обработке.
3. Делать 4 варианта, выбрать один, проверить по чек-листу `docs/ART.md` раздел 2.
4. Обработка (раздел «После генерации» в конце файла).

Для серии (например, 6 символов Шифра или 4 ловца) генерировать **одним листом** («a set of N icons in a row, same style, evenly spaced»), потом резать: так стиль совпадает.

## Общий стиль

### Манера Г: интерфейс терминала (ноды, ступени нырка, состояния, мини-игры, Киберпедия)
Пояснение: неоновые линии на прозрачном фоне, живут только внутри окон мода. Белые и малонасыщенные части код перекрашивает в цвет схемы терминала (`NetrunTint`), поэтому основу рисовать **белой**, а цветом только смысловые акценты (красный для опасности, фиолетовый для Мнемозины).
```
STYLE G (terminal UI): game UI icon for a cyberpunk netrunning terminal, one centered symbol, thin neon line art
with a soft glow, 2-3 px strokes at 128 px, mostly white lines (they are recolored in game) with at most one accent
color, octagonal or circuit-like geometry, flat, no perspective, readable at 32 pixels, occupies about half of the
canvas, transparent background, consistent icon set, minimal detail
```
Negative для Г:
```
text, letters, numbers, watermark, frame around the whole icon, background plate, photo, 3d render, gradient
background, drop shadow, multiple objects, clutter, thick fill, realistic, human face (unless asked)
```

### Манера В: вещи и постройки (предметы 128 px, постройки 2x2 256 px)
Пояснение: как предметы HE и Netrun: приглушённый корпус, толстый тёмный контур, один светящийся акцент бирюзой #3FD8E8 (опасное красным #E04050), ракурс RimWorld сверху под углом.
```
STYLE V (item/building): RimWorld mod game asset, flat cel-shaded vector style, thick dark outline (4-5 px at 128),
muted palette: olive grey #6E715E, steel #8A9099, dark grey #3C3F44, ONE glowing accent in cyan #3FD8E8, simple
light from the top left with one highlight, slightly top-down 3/4 view as in RimWorld, single centered object,
transparent background, no shadow on the ground, consistent with Hacking Expansion items
```
Negative для В:
```
text, logo, brand, watermark, photo, realistic render, ground shadow, scene, background, multiple objects, neon
everywhere, bright saturated colors, perspective distortion, people
```

### Манера А: ванильный глиф (знаки фракций)
Пояснение: только белый и серый, игра красит в цвет фракции.
```
STYLE A (faction glyph): RimWorld faction icon, only white and grey (base #F0F0F0, shadows #B8B8B8 to #8C8C8C),
solid black outline 4-5 px at 128, rounded corners, soft volume lit from the top left, one recognizable symbol
filling 45-55% of the canvas, transparent background, no glow
```
Negative для А:
```
color, hue, neon, glow, text, background, frame, gradient sky, photo, 3d
```

## Ассеты

Колонки: **файл** (имя в `Textures/`), **размер в игре**, **манера**, **промпт ассета**, **заметки**. Негативный промпт: из манеры (плюс дополнения, если указаны).

### 1. Нырок: фигуры и ступени (`Textures/UI/Netrun/Cyberspace/`)
| Файл | Размер | Манера | Промпт ассета | Заметки |
|---|---|---|---|---|
| `Dive_Patrol` | 128 | Г | `a sentinel program: a hovering diamond-shaped drone with one scanning eye and a short sweeping cone of light, accent color amber yellow` | читается как «сторож», не как оружие |
| `Dive_Catcher_Hound` | 128 | Г | `a predatory program shaped like a lean hound made of glitching lines, head low, running forward, accent color violet #A070E0` | серия ловцов: один лист из 4 |
| `Dive_Catcher_Web` | 128 | Г | `a spider web made of circuit traces with a small glowing node in the center, accent violet #A070E0` | |
| `Dive_Catcher_Mirror` | 128 | Г | `two identical humanoid silhouettes facing each other, one solid and one made of broken mirror shards, accent violet #A070E0` | лицо без деталей |
| `Dive_Catcher_Whisper` | 128 | Г | `a data folder icon wrapped in thin violet smoke tendrils that curl like a beckoning hand, accent violet #A070E0` | |
| `Dive_Rival` | 128 | Г | `a runner token: a hooded figure silhouette inside a hexagon, empty circle on the chest for an emblem, accent red #E5533D` | эмблема накладывается кодом в круг |
| `Dive_Runner_Token` | 64 | Г | `a round token frame with a thin double ring and four small notches, empty center, white lines only` | в центр код ставит портрет |
| `Dive_Flatline` | 128 | Г | `a heartbeat line that goes flat, crossed by a small lightning bolt, accent red #E5533D` | |
| `Dive_Core` | 128 | Г | `a processor core: a square chip with radiating circuit lines on four sides and a bright center, white lines` | можно взять `Node_CorpCore` |
| `Dive_Safe` | 128 | Г | `a small vault door seen from the front with a round dial, white lines` | |
| `Dive_Safe_Locked` | 128 | Г | `the same vault door with a padlock in front and a red outline, accent red #E5533D` | генерировать вместе с `Dive_Safe` |
| `Dive_Backdoor` | 128 | Г | `a doorway drawn with a dashed outline and an arrow leaving through it, white lines` | |
| `Dive_DoubleLock` | 128 | Г | `two keyholes side by side linked by one line, white lines` | |
| `Dive_PressureGate` | 128 | Г | `a sliding gate held open by a lever, with pressure arrows pushing it closed, white lines` | |
| `Dive_TwinICE` | 128 | Г | `a two-headed serpent made of jagged ice crystals, accent red #E04050` | опасное: красный акцент |
| `Dive_ICE_White` | 128 | Г | `an ice crystal shield with an eye symbol, pure white lines` | серия классов льда, один лист |
| `Dive_ICE_Grey` | 128 | Г | `an ice crystal shield with a gear being cracked, grey lines with mid grey accent` | серый: не перекрашивается кодом, если насыщенность низкая; проверить в игре |
| `Dive_DigDeeper` | 128 | Г | `a downward arrow drilling into stacked layers, white lines` | |

### 2. Состояния и шапка нырка
| Файл | Размер | Манера | Промпт ассета | Заметки |
|---|---|---|---|---|
| `Alarm_Eye` | 64 | Г | `a stylized open eye inside an octagon, white lines only` | один файл, цвет (зелёный / жёлтый / красный) ставит код |
| `Icon_Noise` | 32 | Г | `three short sound wave arcs, white lines` | очень простой, читается в 32 px |
| `Icon_GhostRun` | 128 | Г | `a faint ghost silhouette walking through a wall made of code lines, white lines` | можно взять `Program_Stealth` |
| `Icon_DeckMemory` | 32 | Г | `a memory chip with four small blocks, white lines` | |
| `Hit_TakeIt` | 128 | Г | `a figure bracing against a lightning strike while holding a data box, accent red #E5533D` | серия окна удара, один лист из 3 |
| `Hit_Decoy` | 128 | Г | `a figure stepping aside while a hollow copy of itself takes a lightning strike, white lines with cyan accent` | |
| `Hit_Drop` | 128 | Г | `an open hand dropping data cubes that scatter, white lines` | |
| `Program_Burned` | 64 | Г | `a crack overlay with small flames along it, transparent everywhere else, accent orange` | накладывается поверх иконки программы |

### 3. Меню ядра
| Файл | Размер | Манера | Промпт ассета | Заметки |
|---|---|---|---|---|
| `Menu_Read` | 64 | Г | `an open file with lines of text flowing into a downward arrow, white lines` | серия из 4, один лист |
| `Menu_Locate` | 64 | Г | `a radar sweep with two small blips, white lines` | |
| `Menu_Control` | 64 | Г | `a toggle lever switching between two positions with a small signal wave, white lines` | |
| `Menu_Erase` | 64 | Г | `lines of code being wiped by a horizontal bar, half of the lines fading out, white lines` | |

### 4. Карта сети (`Textures/UI/Netrun/Cyberspace/`)
| Файл | Размер | Манера | Промпт ассета | Заметки |
|---|---|---|---|---|
| `Map_Patrol` | 64 | Г | `a small patrol drone seen from above with a scanning arc, accent amber` | рядом с `Dive_Patrol` по стилю |
| `Map_RaidNode` | 64 | Г | `a crosshair over a small fortress silhouette, accent red #E5533D` | можно взять `Node_Counterhack` |
| `Map_Bunker` | 64 | Г | `a heavy bunker hatch half open with light coming out, accent copper #C87F45` | медь = Старая сеть |
| `Map_Breach` | 64 | Г | `a cracked wall with a jagged opening, accent copper #C87F45` | |
| `Map_WildAI` | 64 | Г | `an abstract creature made of floating data shards around a single eye, accent copper #C87F45` | можно взять `Event_WildAI` |
| `Map_Grave` | 64 | Г | `a server rack shaped like a tombstone with a faint glow, accent copper` | |
| `Map_TransitionArchive` | 64 | Г | `a sealed archive cylinder with a spiral symbol, accent violet #A070E0` | связь с концовкой |
| `Map_Camp` | 32 | Г | `a small tent made of three lines with a dot of light, white lines` | |

### 5. Мини-игры (`Textures/UI/Netrun/Minigames/`, новая папка)
| Файл | Размер | Манера | Промпт ассета | Заметки |
|---|---|---|---|---|
| `MG_Cipher_Sym1..6` | 64 | Г | `a set of 6 distinct abstract glyphs for a code puzzle: triangle with dot, double bar, open circle, zigzag, square with diagonal, crescent; equal size, white lines, in one row` | символы должны различаться формой, не цветом (дальтоники) |
| `MG_Bearing_Source` | 64 | Г | `a signal source: a dot with three concentric ripples, accent cyan` | клетки сетки рисует код |
| `MG_Hold_Shield` | 64 | Г | `a shield made of hexagon cells, white lines` | серия из 3 каналов; можно взять `Program_Shield` |
| `MG_Hold_Divert` | 64 | Г | `a curved arrow deflecting a straight bolt, white lines` | |
| `MG_Hold_Cool` | 64 | Г | `a snowflake over a small heat gauge, white lines` | |
| `MG_Hold_Intent_Strike` / `_Double` / `_Overheat` | 32 | Г | `three tiny intent icons in a row: one bolt, two bolts, a thermometer at max; accent red` | |
| `MG_Spoof_Card` | 128 | Г | `an empty ID card frame with a chip in the corner and two empty label slots at the bottom, white lines` | рамка, иконки карт кладутся внутрь |
| `MG_Spoof_Plan`, `_Pass`, `_ForeignVoice`, `_DeptPass`, `_Window`, `_Admin`, `_FakeLog` | 64 | Г | `a set of 7 icons in one row: blueprint scroll; keycard; speech wave coming from a mask; badge with a department stripe; open window with a clock; terminal with a crown; document with a forged stamp; white lines` | имена в порядке промпта |
| `MG_Spoof_Guard` | 128 | Г | `a security AI face made of horizontal scan lines, calm and neutral, no mouth, white lines` | |

### 6. Предметы (`Textures/Things/Item/`, манера В, 128 px)
| Файл | Промпт ассета | Заметки |
|---|---|---|
| `NR_WrongDrive` | `an old rugged data drive with a cracked casing, strange glyphs scratched on it, the cyan light leaking through the cracks flickers in an unnatural pattern` | Anomaly-предмет; акцент можно сделать болезненно-бирюзовым |
| `NR_CultRelic` | `a small reliquary box holding a burned-out processor chip, wrapped in thin copper wire and prayer ribbons, cyan glow from the chip` | можно взять `NR_AICoreModule` перекраской |

### 7. Знаки и портреты
| Файл | Размер | Манера | Промпт ассета | Заметки |
|---|---|---|---|---|
| `Faction_Mnemosyne` | 128 | А | `a sigil: an open eye whose iris is a spiral of many small circles, enclosed by a broken ring` | для одержимой команды (белый, красится игрой) |
| `UI_Mnemosyne_Sigil` | 128 | Г | `the same sigil as neon line art, accent violet #A070E0` | ловцы, письма |
| `AI_Mnemosyne` | как портреты `AI_*` | Г | `portrait of a calm woman's face made of thousands of tiny data points, eyes closed, parts of the face dissolving into lines that drift away, violet and white, dark transparent background` | главный арт сюжета; можно отдельно попросить Maxim |
| `Header_Minigames`, `Header_Group`, `Header_Heists`, `Header_BeyondWall` | как `Header_Nodes` | Г | `a wide banner illustration in neon line art: (minigames) four small puzzle symbols; (group) three runner silhouettes linked by lines; (heists) a vault door with a blueprint behind it; (beyond the wall) a cracked wall with violet light behind it` | 4 отдельных генерации, широкий формат как у готовых заголовков |

### 8. Иконки «Что нового»
Берутся из ассетов выше (тот же файл): Группа = `Dive_Runner_Token` с тремя жетонами, Шифр = `MG_Cipher_Sym1`, Удержание = `MG_Hold_Shield`, Пеленг = `MG_Bearing_Source`, Подмена = `MG_Spoof_Card`, Старая сеть = `Map_Breach`, Большое дело = `Header_Heists` (кадр), Классы ICE = `Dive_ICE_White`, Меню ядра = `Dive_Core`, Память деки = `Icon_DeckMemory`. Отдельной генерации не нужно.

## После генерации
1. **Оригинал** (как сгенерирован, полный размер) положить в `G:\Modding\RimWorld\Hacking Expansion Netrun\Reserve\Hacking Expansion Netrun Art\v3\<папка как в Textures>\<имя>_src.png`. В репозиторий оригиналы не класть.
2. **Фон:** если генератор не дал прозрачность, убрать пурпурный #FF00FF (допуск ~10%), края без ореола (сгладить альфу на 1 px). Проверить, что нет полупрозрачного «тумана» по краям (для манеры Г свечение допустимо, но не плашкой).
3. **Кадрирование:** объект по центру, занимает ~45–55% холста (манеры А и В) или ~50% (Г); поля одинаковые.
4. **Уменьшение:** до игрового размера (128 / 64 / 32, постройки 256) в два шага (сначала 50%, потом до цели), фильтр Lanczos. Линии в 32 px не должны слипаться: если слиплись, упростить исходник, не размывать.
5. **Цвет:** манера Г: основа белая или почти белая (код перекрасит); проверить, что смысловой акцент (красный, фиолетовый, медь) насыщенный, иначе код его тоже перекрасит. Манера А: насыщенность 0 во всех пикселях.
6. **Проверка:** чек-лист `docs/ART.md` раздел 2 (манера, контур, свечение, читаемость в 32 px, рядом с эталоном на одном листе).
7. **Игровая копия** в `Textures/...` по пути из `docs/ART.md` раздел 4, PNG 8 бит на канал с альфой, без метаданных.
8. Сказать сессии Claude (задача арта A1–A6 в `docs/DEV_PLAN.md`), какие файлы готовы: она подключит их вместо временных иконок.
