# Black ICE: карта интеграции с Hacking Expansion

5 октября 2026. Основано на чтении исходников Hacking Expansion (workshop 3573344880, namespace USH_HE, папка Source, 50 файлов) сессией на компьютере Maxim. Только чтение, ничего не менялось. Сам код я не открывал, выдержки ниже переданы этой сессией.

## Как идея нашего мода ложится на контент Hacking Expansion

| Идея Black ICE | За что цепляемся | Как | Сложность |
| --- | --- | --- | --- |
| Уровни Heat и награда за взлом | Ванильный `CompHackable.ProcessHacked(Pawn, bool)` | Наш Harmony-postfix. Срабатывает один раз по завершении взлома и для пешки, и для капсулы. Сам Hacking Expansion цепляется туда же | Низкая |
| Бонусы работы из капсулы | Тот же postfix + `pawn.TryGetHoldingCyberpod(out pod)` | Если хакер в капсуле, добавляем Cyberdata, уменьшаем прирост Heat и риск Netsick | Низкая |
| Heat снижает скрытность | Стат `HackingStealth` | Хедифф «след» с отрицательным offset по стату, чем выше Heat, тем сильнее. ICE-исходы срабатывают по MTB от скрытности, значит частота растёт | Низкая |
| Adaptive ICE и риск Красной зоны | Публичные `HackingOutcomeDef` и `HackingOutcomeWorker` (проверено по репозиторию) | Свои исходы (ICE-отдача, нейрошум, утечка следа) объявляем своими `HackingOutcomeDef` со своим `workerClass` и добавляем в списки `hackingOutcomes` ванильных Hackset-ов через `PatchOperationAdd`. Условия («только при Heat 75+», «если тип цели повторяется») пишем в `CanApplyOnPawn`. Для частоты на повторяющихся целях: Harmony-prefix на публичный `CompDataSourceProtected.Hack(float, Pawn)` задаёт контекст, а `StatPart` на `HackingStealth` его читает | Средняя |
| Cyberdata вместо BI_Cyberdata | `USH_Cyberdata`, `USH_AdvancedCyberdata` | Наш мод их не создаёт, а читает и тратит. Фиксеры берут именно их | Низкая |
| Свой вид данных (если понадобится) | `RippableExtension` на ThingDef | `CyberUtils.AllCyberDataDefs` собирает все ThingDef с этим расширением, поэтому Hacking Expansion сам начнёт вытягивать наш вид из целей | Низкая |
| Проверка «есть нейроинтерфейс» | `CyberUtils.IsHacker()` | Проверка по hediffClass, наследующему `Hediff_Cyberlink` | Низкая |
| ИИ-пассажир | Мозг пешки с кибер-линком | Наш хедифф на Brain, НЕ наследующий `Hediff_Cyberlink`. Иначе он сам станет считаться кибер-линком. Условие установки: `IsHacker()` | Средняя |
| Просмотр BD | Капсула и кибер-линк | Пункт «Смотреть BD» в `Building_Cyberpod.GetFloatMenuOptions`; хедифф зрителя | Средняя |
| Сетевой терминал (консоль связи) | Ванильный `Building_CommsConsole` | `PatchOperationAdd` добавляет свой компонент с кнопкой. Hacking Expansion консоль не упоминает ни в XML, ни в коде | Низкая |
| Сетевой терминал (капсула) | `Building_Cyberpod.GetGizmos` (public override) | Postfix добавляет кнопку | Низкая |
| Начало и конец сессии, Netsick | `Building_Cyberpod.TryAcceptThing`, `EjectContents` | Postfix: считаем время в капсуле, растим хедифф | Средняя |
| Последствия срыва «Сетевая ломка» | `USH_CyberspaceNausea`, `USH_CyberspaceComa`, `USH_CyberspaceLockOut` | Накладываем готовые хедиффы при неудачном нырке | Низкая |
| Сервер для ИИ | Свой объект `BI_AIServer` (решение Maxim, графику даёт он) | Отдельная запитанная постройка, `USH_DataServer` не трогаем. Исследование ставим после `USH_CybertechMachinery`. Мы ничего не патчим у Hacking Expansion, конфликтов нет | Низкая |
| Усиление капсулы | Сопроцессоры Optimization, Jammer, Vitality (`CompAffectedByFacilities`) | Наш собственный сопроцессор или множитель к стату | Средняя |
| Исследования | `USH_BasicCybertech`, `USH_AdvancedCybertech` | Наши исследования ставим после них (`prerequisites`) | Низкая |

## Что важно знать про Hacking Expansion

- **Кибер-линк:** `USH_Cyberlink` даёт HackingSpeed +0.1, `USH_AdvancedCyberlink` даёт HackingSpeed +0.2 и HackingStealth +25 (заменяет обычный). Ставится без операции, пешка сама использует предмет. Крафт на TableMachining: обычный 10 `USH_Cyberdata` + 6 ComponentIndustrial + 50 Steel (исследование `USH_BasicCybertech`), продвинутый 4 `USH_AdvancedCyberdata` + 6 ComponentSpacer + 50 Steel (`USH_AdvancedCybertech`).
- **Только «хакер» может лечь в капсулу** и взламывать турели и мехов. Хакер = любая пешка с хедиффом, чей класс наследует `Hediff_Cyberlink`.
- **Cyberdata:** `USH_Cyberdata` стоит 50 и даёт 15 очков исследования. Добывается вытягиванием («Rip data») с любой взламываемой цели: данные выпадают каждые `hackWorkAmount` работы (1500), максимум с цели `min(4~8, защита/1500)`. `USH_BrokenExecData` выпадает по завершении взлома цели под ICE. Данные также лежат в луте `USH_DataCenter` и `USH_EncryptedCrate`.
- **Траты данных:** топливо капсулы (1 data = 40 hack points, ёмкость 400), исследования, кибер-линк (10), постройка капсулы (12), апгрейды (2–5). Значит, фиксеры конкурируют с исследованиями и топливом капсулы.
- **Капсула:** класс `USH_HE.Building_Cyberpod : Building_Casket`, нужно 200 Вт энергии, нет поломки, нет оглушения, есть топливо. Скорость взлома = `HackingSpeed × USH_HackingSpeedMultiplier` (у капсулы 0.3), скрытность ×0.6.
- **Black ICE у Hacking Expansion:** это свойство цели, а не события. `USH_BlackICE`: minDefense 30000, вес 0.3. **Капсула такие цели вообще пропускает.** Исходы (выжигание мозга, смертельный некроз) срабатывают по MTB от скрытности.
- **Hackset-ы:** OutdatedFirewall (от 200: тошнота, блокировка, EMP-импульс, разгерметизация, EMP-взрыв, утечка энергии), ICE (от 6000: кома, EMI-вирус, замыкание), CoreCommandICE (от 30000: берсерк, психоз, кибер-некроз), BlackICE (от 30000: выжигание мозга, смертельный некроз).
- **Взламываемые цели:** `USH_DataCenter`, `USH_EncryptedCrate`, `USH_AncientCyberdeck`, `USH_SatelliteBlackBox`, все турели (питание или оглушение EMP, кроме ручных; настройка), все мехи и дроны (настройка). Каждой вещи с ванильным `CompHackable` добавлен `CompDataSourceProtected`. Из ваниля и DLC: Ideology (AncientTerminal, AncientEnemyTerminal, AncientCommsConsole, Spacedrone и другие), Odyssey (AncientBlastDoor, AncientHatch, AncientGravReactor, AncientUplink, MechStabilizer и другие). Значит, ванильных целей для взлома достаточно, и своих взламываемых объектов моду не нужно.

## Ключевые выдержки (переданы сессией, не мной проверены)

```csharp
// Source/CyberUtils.cs
public static bool IsHacker(this Pawn p) => p.HasAnyHediffDef(AllCyberlinkHediffDefs, out _);

// Source/Harmony/PatchCompHackable.cs (так Hacking Expansion цепляется за ваниль)
[HarmonyPatch(typeof(CompHackable), "ProcessHacked")]
static void Postfix(CompHackable __instance, Pawn hacker, bool suppressMessages) { ... }

// Source/Building_Cyberpod.cs
private bool CanBeHacked(CompHackable c, bool autohackCheck = true) {
    ... if (c.parent.TryGetComp(out CompDataSourceProtected p) && p.HacksetDef == USH_DefOf.USH_BlackICE) return false; ...
}
```

## Что меняется в концепции из-за этой проверки

1. **Бонус «меньше шанс Black ICE» в капсуле** формулируем как «реже срабатывают ICE-исходы» (за счёт скрытности). Опционально наш апгрейд разрешает капсуле взламывать BlackICE-цели с риском.
2. **Adaptive ICE** встраиваем в систему Hackset-ов Hacking Expansion, а не пишем свой блок: меняем веса и скрытность.
3. **Название уровня Heat «Black ICE»** заменено на «Красная зона», чтобы не путать с Black ICE как уровнем защиты цели.
4. **Нырок свой не пишем:** он уже есть, мы расширяем его начало и конец и добавляем бонусы.
5. **ИИ-пассажир и BD-зритель** вешаем отдельным хедиффом на мозг, не наследуя `Hediff_Cyberlink`.
6. **Своих взламываемых целей не нужно:** ванильных и DLC-целей достаточно.
7. **Наш текущий код** (хук на `ProcessHacked`, ReflectionUtility) уже цепляется правильно, но патч на `CanHackNow` из прототипа выкидываем: блокировку теперь делает система Hackset-ов.

## Сверка с репозиторием (github.com/BlueEagle421/HackingExpansion, коммит 7b98454 от 5 апреля 2026)

Я клонировал репозиторий и проверил три оставшихся места сам.

- **`USH_DataServer`:** обычное запитанное здание, слот добавляется XML-патчем (см. таблицу выше). Закрыто.
- **Выбор Hackset:** `CyberUtils.GetHacksetDef` выбирает Hackset случайно по весу один раз при появлении цели (`CompDataSourceProtected.PostSpawnSetup`) и сохраняет в сейв. Значит, менять веса для уже существующих целей бесполезно. Это изменило план: наши исходы добавляем в списки исходов Hackset-ов, а не правим выбор Hackset. Закрыто.
- **Когда срабатывает исход:** во время взлома по MTB от стата `HackingStealth` хакера, но не чаще раза за 1000 единиц прогресса и только когда Hackset «активен» (он становится активным после урона по цели и ещё в одном месте, `CompDataSource.cs`, строка 111). В капсуле работают только исходы с `worksWithPod = true`, так что нашим исходам надо явно задавать эту настройку.
- **Выход из капсулы:** `Building_Cyberpod.EjectContents()` публичный override. Его вызывают и обычное пробуждение, и принудительное извлечение, так что подсчёт Netsick на выходе надёжен. Вход: `TryAcceptThing` сразу после успешного приёма запускает автовзлом. Закрыто.
- **Консоль связи:** слова `CommsConsole` в репозитории нет нигде. Закрыто.

## Что ещё проверить до кода

- Что именно активирует Hackset в `CompDataSource.cs` (строка 111), чтобы понимать, сколько взломов реально сопровождается ICE.
- Как Hacking Expansion сохраняет и сбрасывает время в капсуле, чтобы наш Netsick не противоречил его таймерам.
- Сравнение локальной копии из Steam с GitHub: сессия на компьютере Maxim сейчас этим занимается.

## Дополнения (сверка сессии на компьютере Maxim с GitHub, 5 октября 2026)

Сессия сверила Steam-копию мода с GitHub (ветка main, последний коммит 5 апреля 2026, с тех пор изменений нет). Побайтово файлы не сравнивались. В планах автора нет ни ИИ, ни брейнданса, ни фиксеров, ни консоли связи, так что наши идеи его контент дополняют, а не дублируют. Новое для нас:

- **Зависимости Hacking Expansion:** Harmony и Vanilla Expanded Framework (VEF). Значит, нам можно пользоваться VEF, например его системой способностей (Hacking Expansion использует её для эксов). Способности ИИ можно делать через неё.
- **Расширение через XML:** `HackingOutcomeDef`, Hackset-ы, `RippableExtension` и класс `Hediff_Cyberlink` рассчитаны на дополнение сторонними модами. Апгрейды кибер-линка сделаны по общему шаблону (`Bases.xml`), по нему можно добавить имплант-апгрейд «ИИ-сокет» (ставится только при наличии кибер-линка).
- **Отложенные рейды:** у Hacking Expansion есть готовый механизм `DelayedRaidUtility` («рейд через N часов»), его используют чёрный ящик и древняя кибер-дека. По его образцу запускаем рейд наёмников после «утечки следа».
- **Исследования:** наши проекты и предметы открываем через их `ResearchPrerequisitesExtension` после `USH_BasicCybertech`, `USH_CybertechMachinery`, `USH_AdvancedCybertech`.
- **Экономика двух модов:** фиксеры могут продавать контент Hacking Expansion (апгрейды кибер-линка, ICEBreaker, broken exec data, а редкие фиксеры сам кибер-линк), а контракты можно привязать к его объектам (`USH_DataCenter`, древняя кибер-дека, спутниковый чёрный ящик).
- **Heat и вытягивание данных:** предлагается, чтобы «Rip data» оставлял больше следа, чем обычный взлом: у игрока появляется выбор между быстрой добычей и скрытностью.
- **Netsick:** кроме времени в капсуле растёт и от дистанционных взломов через кибер-линк (`JobDriver_RemoteHack`).
- **Фрагмент ИИ:** выпадает с шансом при завершении взлома целей под CoreCommand ICE или Black ICE, по аналогии с broken exec data.
- **Риск обновлений:** в первую очередь могут сломаться патчи капсулы. Держим их в try/catch и проверяем после каждого обновления Hacking Expansion.
