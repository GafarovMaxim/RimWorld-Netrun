# Hacking Expansion: Netrun — список артов

Документ для генерации графики. Промпты на английском: так нейросети понимают точнее.

## Общий стиль (вставлять в каждый промпт)

```
RimWorld mod game asset, flat cel-shaded vector style, thick black outline,
muted military palette (olive green, gunmetal grey, dark charcoal),
glowing cyan accents, subtle cyberpunk tech, slightly top-down 3/4 view
as in RimWorld, centered, single object, transparent background, no text,
no shadow on ground, consistent with existing assets
```

Ориентир по стилю: уже готовые AIServer.png, BraindanceGoggles.png, BrainDanceFile.png.

Технические требования ко всем файлам:
- PNG с прозрачным фоном.
- Можно генерировать в 1024x1024: уменьшу до нужного размера сам.
- Объект по центру, вокруг небольшой отступ (примерно 5–10% от края).
- Без текста, подписей, водяных знаков и тени на полу.

## Готово

| Файл | Что это |
|---|---|
| AIServer.png | ИИ-сервер, постройка 2x2 |
| BraindanceGoggles.png | BD-очки, иконка предмета |
| BrainDanceFile.png | BD-чип, иконка предмета |
| AIServer_Minimal.png | запасной вариант сервера |

## Нужно сделать

### A. Обязательно (нужно для работы систем)

**A1. BD-очки на пешке: 3 файла**
- Файлы: `BraindanceGoggles_south.png`, `BraindanceGoggles_north.png`, `BraindanceGoggles_east.png`. West игра отражает сама.
- Что: те же очки, что на BraindanceGoggles.png, как они выглядят надетыми на голову пешки: спереди, сзади (виден ремешок) и сбоку.
- Важно: нарисовать только очки, без головы. Располагать их там, где у пешки RimWorld глаза. Лучше всего взять за шаблон любую ванильную текстуру головного убора и рисовать поверх её контура.
- Промпт:
  `VR braindance goggles worn on head, [front view | back view showing strap | side view], only the goggles without the head, positioned at eye level of a RimWorld pawn head`

**A2. ИИ-фрагмент: 1 файл**
- Файл: `AIFragment.png`
- Что: небольшой кристалл или шард данных, внутри которого "что-то живое" (пульсирующий узор, глаз или ядро).
- Промпт:
  `small data shard containing a living artificial intelligence, crystalline core with pulsing cyan neural pattern, compact tech casing`

**A3. Имплант "ИИ-сокет": 1 файл**
- Файл: `AISocket.png`
- Что: мозговой имплант-разъём, который вставляется в кибер-линк. Стиль как у апгрейдов кибер-линка из Hacking Expansion.
- Промпт:
  `small neural implant socket module, cybernetic brain connector chip with pins, cyan indicator light, medical tech item`

**A4. Сопроцессор капсулы: 1 файл (постройка 1x1)**
- Файл: `NetrunCoprocessor.png`
- Что: небольшая серверная стойка или блок, который подключается к кибер-капсуле. Вид сверху и чуть спереди, как у построек RimWorld.
- Промпт:
  `small 1x1 computer coprocessor unit building, compact rack with cooling vents and status lights, top-down 3/4 RimWorld building view`

**A5. Виды BD-чипов: 4 файла**
- Файлы: `BrainDanceFile_Calm.png`, `BrainDanceFile_Skill.png`, `BrainDanceFile_Thrill.png`, `BrainDanceFile_Black.png`
- Что: перекраски готового BrainDanceFile.png, меняется только цвет экрана и значка:
  - Calm: мягкий голубой, значок волна;
  - Skill: зелёный, значок книга или шестерёнка;
  - Thrill: оранжевый, значок молния;
  - Black: тёмно-красный на чёрном, значок череп или трещина.
- Если удобнее, можно не делать: тогда я раскрашу один чип прямо в игре.

### B. Иконки кнопок (gizmo)

Квадрат, крупный узнаваемый символ, читается в маленьком размере (64 px). Фон прозрачный или тёмная плашка в стиле игры.

| Файл | Кнопка | Идея символа |
|---|---|---|
| `Gizmo_NetTerminal.png` | Сетевой терминал | монитор с сеткой или узлами сети |
| `Gizmo_WatchBD.png` | Смотреть BD | очки с волной |
| `Gizmo_RecordBD.png` | Записать BD | красная точка записи и мозг |
| `Gizmo_Dive.png` | Нырок в сеть | человек, погружающийся в сетку |

Промпт:
`game UI icon, [symbol], bold simple silhouette, cyan glow on dark background, readable at 64 pixels`

### C. Интерфейс терминала (желательно)

**C1. Значки уровней Heat: 5 файлов**
- `Heat_Shadow.png` (Тень): силуэт в тени или закрытый глаз, серо-голубой;
- `Heat_Noticed.png` (Замечен): полуоткрытый глаз, жёлтый;
- `Heat_Tracked.png` (Отслеживается): глаз со следящими линиями или радар, оранжевый;
- `Heat_Target.png` (Цель): прицел, красно-оранжевый;
- `Heat_RedZone.png` (Красная зона): горящий прицел или череп в красном круге, ярко-красный.

**C2. Портреты ИИ: 5 файлов** (абстрактные цифровые лица или символы, не люди)
- `AI_DataHunter.png`: ищейка из данных, сканирующий луч;
- `AI_Ghost.png`: полупрозрачная маска;
- `AI_Muse.png`: светящаяся фигура со звёздами или нотами;
- `AI_Predator.png`: хищный цифровой зверь, красные глаза;
- `AI_Echo.png`: размытый силуэт человека из пикселей, "цифровой призрак".

Промпт:
`abstract digital AI persona portrait, [description], glowing wireframe on dark background, cyberpunk`

**C3. Силуэты фиксеров: 3 файла**
- `Fixer_1.png`, `Fixer_2.png`, `Fixer_3.png`: тёмные силуэты по плечи (капюшон, шляпа, кибер-имплант на лице), без деталей лица.

### D. Для Steam и списка модов

| Файл | Что | Размер |
|---|---|---|
| `Preview.png` | обложка мода: название "Hacking Expansion: Netrun", нетраннер в капсуле, сеть, ICE | около 640x360 (16:9) |
| `ModIcon.png` | иконка мода: стилизованный символ сети или нетраннера | около 128x128 |

Здесь текст на обложке разрешён. Точные размеры для RimWorld 1.6 перепроверю перед публикацией.

## Куда класть
Присылай в тред. Разложу по папкам, уменьшу и подключу сам.
