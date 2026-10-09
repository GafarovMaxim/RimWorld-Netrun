# Тексты B21a и B22: пустые состояния, уведомления и подписи терминала

Задание из `docs/TASKS_2026-10-09.md` (B21, B22, C17). Облако пишет, Плотва вставляет и оформляет (B21b).
**Как вставлять:** все ключи уже существуют, нужно только заменить значения в `Languages/English/Keyed` и `Languages/Russian (Русский)/Keyed`. Новых ключей нет, `check_keys.py` не затронут. Плейсхолдеры `{0}` не менять.

**Принципы.** Короткий статусный тон терминала, без шуток и без воды; смысл остаётся тем же, что у старого текста. Сообщение «ИИ нет» объясняет, что делать дальше. Оформление (B21b): строка состояния в рамке с меткой кода слева (`[NO AI]`), глиф `◇` для «пусто», `▲` для «нельзя сейчас». Метку кода Плотва рисует отдельным элементом, в строку она не входит.

## 1. Пустые состояния («нет ИИ» и подобные)

| Ключ | Было (EN) | Стало (EN) | Стало (RU) |
|---|---|---|---|
| `NR_AINone` | No AIs yet. Find an AI fragment… | **No AI online.** Find an AI fragment (behind strong ICE or at a fence), build the AI server, then press "Awake AI" in the server room or on the server's "AI room" gizmo. | **ИИ не в сети.** Найдите осколок ИИ (за сильным ICE или у скупщика), постройте ИИ-сервер и нажмите «Пробудить ИИ» в серверной или в гизмо «Комната ИИ» сервера. |
| `NR_AwakenNoFragment` | No AI fragment in the colony. | No fragment to wake. The colony holds no AI fragment. | Будить нечего. В колонии нет осколков ИИ. |
| `NR_AwakenNoServer` | Needs a powered AI server with a free slot. | No host. Needs a powered AI server with a free slot. | Нет хоста. Нужен запитанный ИИ-сервер со свободным слотом. |
| `NR_AwakenNoConsole` | Needs a powered network console. | No uplink. Needs a powered network console. | Нет канала. Нужна запитанная сетевая консоль. |
| `NR_AwakenNoWorker` | No colonist can do intellectual work. | No operator. Nobody here can do intellectual work. | Нет оператора. Никто в колонии не способен к интеллектуальному труду. |
| `NR_AIIsolatedNoService` | Isolated AIs do not work. | Cut off from the net: an isolated AI does no work. | Отрезан от сети: изолированный ИИ не работает. |
| `NR_ContractNoFragment` | No AI fragment in storage. | Nothing to deliver: no AI fragment in storage. | Сдавать нечего: на складе нет осколка ИИ. |
| `NR_TerminalNoPower` | No power. | Terminal dark. No power. | Терминал обесточен. Нет питания. |
| `NR_GatewayPodNone` | No cyberpod | No pod linked | Капсула не подключена |
| `NR_GatewayNoPod` | needs a colonist with a cyberlink in a powered cyberpod | needs a runner: a colonist with a cyberlink in a powered cyberpod | нужен раннер: колонист с кибер-линком в запитанной капсуле |
| `NR_GatewayNoSlots` | no free slots | all slots taken | все слоты заняты |
| `NR_HeatScreenNoFixer` | Scrub at a fixer: no fixer is answering right now | Scrub: no fixer picks up. Try later. | Сброс: ни один фиксер не отвечает. Позже. |

## 2. Другие пустые состояния и уведомления

| Ключ | Было (EN) | Стало (EN) | Стало (RU) |
|---|---|---|---|
| `NR_ClubNoCorps` | No corporations play in the net yet. | The net is quiet. No corporation has made a move yet. | В сети тихо. Ни одна корпорация пока не пошевелилась. |
| `NR_CoreNoAlerts` | Nothing urgent. | All clear. | Всё чисто. |
| `NR_CoreNoServer` | no server | server offline | сервер не в сети |
| `NR_HeatScreenNothingRaised` | Nothing this week. | No heat raised this week. | За неделю Heat не растёт. |
| `NR_ArchiveEmpty` | Nothing matches. | No records match. | Записей нет. |
| `NR_ArchiveNoReports` | No dives yet. | No dives on record. | Нырки не записаны. |
| `NR_GatewayNoReports` | No dives yet. | No dives on record. | Нырки не записаны. |
| `NR_ArchiveNoRaids` | No raids or attacks yet. | No raids or attacks on record. | Рейды и атаки не записаны. |
| `NR_StudioNoRecordings` | No raw recordings. Colonists wearing braindance goggles record strong moments on their own. | The reel is empty. Colonists in braindance goggles record strong moments on their own. | Плёнка пуста. Колонисты в брейнданс-очках сами записывают сильные моменты. |
| `NR_StudioNoChips` | No chips in storage. | No chips in storage. | На складе нет чипов. |
| `NR_StudioNoneHooked` | Nobody is hooked on braindance. | Nobody is hooked. | Никто не подсел. |
| `NR_GatewayPickNode` | Pick a node. | Pick a node to read it. | Выберите ноду, чтобы прочесть её. |
| `NR_ReportNothing` | Nothing brought back. | Came back empty-handed. | Вернулись с пустыми руками. |
| `NR_DiveHudHaulEmpty` | Nothing yet. | Haul: empty. | Добыча: пусто. |
| `NR_NewsEffectNone` | Nothing special. | No active effects. | Особых эффектов нет. |
| `NR_HelpNoResults` | Nothing found. | No such entry. | Такой записи нет. |
| `NR_DiveNoFurther` | nowhere further to go | dead end | тупик |
| `NR_DiveNothingToScan` | nothing hidden ahead | nothing hidden ahead | впереди ничего не скрыто |
| `NR_DiveNothingToHack` | nothing to hack ahead | no hackable target ahead | впереди нет целей для взлома |
| `NR_DiveNothingToDownload` | nothing to download here | no data worth pulling here | качать здесь нечего |

## 3. Подписи терминала (B22)

| Ключ | Было (EN) | Стало (EN) | Стало (RU) |
|---|---|---|---|
| `NR_TerminalTitleMap` | NETRUN // NETWORK CORE | NETRUN // NETWORK CORE | NETRUN // ЯДРО СЕТИ (без изменений) |
| `NR_TerminalTitleDive` | NETRUN // DIVE: {0} | NETRUN // IN THE NET: {0} | NETRUN // В СЕТИ: {0} |
| `NR_MenuTitle` | NETRUN // MENU | NETRUN // SYSTEM | NETRUN // СИСТЕМА |
| `NR_NodeLockedSub` | locked | no access | нет доступа |
| `NR_NodeLockedMark` | LOCKED | NO ACCESS | НЕТ ДОСТУПА |

**Замечание Плотве.** Заголовок экрана в пункте «В сети» не заменяет «НЫРОК» в справке и Киберпедии (там термин остаётся). Меню-подпись «СИСТЕМА» выбрана вместо «МЕНЮ», чтобы звучало как консоль; если это мешает пониманию новичков (Guide показывает кнопку как «Меню»), оставить MENU / МЕНЮ.

## 4. Что не менял

Тексты с числами и обучающие (`NR_Onboarding*`, `NR_Help*Note`): они уже короткие. Тексты, которые видят только в режиме разработчика. Названия экранов-«разделов» в навигации: их называют в статьях.
