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
| `AI_Archivist.png` | Archivist (Архивариус) | раскрытая книга или стеллаж из светящихся строк, янтарный |
| `AI_Seed.png` | Seed (Зерно) | светящееся семя или росток из линий кода, нейтральный белый |

## Фаза 3: брейнданс и атаки

| Файл | Что это | Идея |
|---|---|---|
| `BraindanceChip_Black.png` | Чёрный брейнданс-чип | тот же чип, что BraindanceChip, но чёрный корпус, красный экран, трещина или череп вместо мозга |
| `BraindanceGoggles_south.png`, `_north.png`, `_east.png` | Очки на голове пешки (3 вида) | только очки, без головы, на уровне глаз пешки RimWorld; north — виден ремешок сзади, east — профиль. Лучше рисовать поверх контура ванильного головного убора |
| `Gizmo_WatchBD.png` | Кнопка "Смотреть BD" | очки с волной |
| `Gizmo_RecordBD.png` | Кнопка "Записать BD" | красная точка записи и мозг |

## Для Steam
| Файл | Что это | Размер |
|---|---|---|
| `ModIcon.png` | Иконка мода в списке модов | около 128x128 |

## Куда класть
Присылай в тред или клади в `Art/Approved`. Разложу по папкам, уменьшу и подключу сам.
