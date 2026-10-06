# Недостающая графика, волна 5 (2026-10-06)

Стиль — как в docs/ART_REQUESTS.md («Общий стиль»): иконки 128x128 PNG с прозрачным фоном, неоновый контур на тёмном, без текста. Класть в `Textures/UI/Netrun/Cyberspace/` (если не сказано иное). Код уже ищет эти имена; пока файла нет, стоит указанная замена.

## Уже подключено из «Hacking Expansion Netrun Art» в этой волне
Daemon_Blade/Aegis/Wraith/Hound/Lens; Event_HunterICE, Event_Rival, Event_Honeypot, Event_DataStorm (из _VarC1), Event_WildAI (из WildAIDialog), Event_DataCourier, Event_CorpHunt, Event_BlackMarket, Event_DistressSignal (из DistressBeacon), Event_MapHack (из _VarB), Event_BossNode; Node_CorpCore (копия Event_BossNode), Node_Counterhack, Node_Locked (закрытые узлы Core), Icon_Lock (замки Киберпедии), Icon_DiveReport, Icon_GroupFavor, Icon_HackerGroup, Reward_DaemonProgram, Fixer_Service_Reward; Textures/Things/Building/Misc/NR_DistressServer.png.
Не подключены (не нашлось места или спорно): AI_Jester, AI_Shard (1024 px), Dive_Node_RogueAICamp_InvertedA, Server_Upgrade_Core/Quantum, OpenCyberspace(_Node), BD_Chip_Celebrity, Capsule_Coprocessor, AIServer_Minimal. Icon_Pinned в папке арта отличается от того, что лежит в Textures (оставлен старый) — скажи, какой брать.

## Нужно нарисовать
| Файл | Где | Идея | Сейчас |
|---|---|---|---|
| `Icon_Guide.png` (Textures/UI/Netrun/) | кнопка «Гайд» в заголовке терминала | раскрытая книга-терминал с курсором на странице, белый контур (тонируется кодом) | нарисована мной простая книжка 64 px |
| `Dive_LOBBY.png` | шапка ступени «Лобби» в нырке | входной шлюз / дверь с зелёной стрелкой внутрь | Action_Move |
| `Dive_FILES.png` | ступень «Файлы» | папка с потоком байтов | Reward_Valuables |
| `Dive_MEMORY.png` | ступень «Память» | микросхема-ячейка с фиолетовым свечением | иконка осколка ИИ |
| `Dive_EXIT.png` | ступень «Выход / порт» | порт ввода-вывода со стрелкой наружу | Action_Exit |
| `Report_Success.png` | итоги вылазки, статус «Чистый выход» | щит с галочкой, зелёный | Action_Exit |
| `Report_Retreat.png` | статус «Отступление» | стрелка назад через рамку, жёлтый | Action_Move |
| `Report_Fail.png` | статус «Провал» | треснувший экран с черепом, красный | Dive_BLACKICE |
| `Icon_Netsick.png` | итоги вылазки (строка Netsick), нижняя строка терминала | капля/волна сигнала, впивающаяся в голову, бирюзовая | Gizmo_AI_Use |
| `Icon_Xp.png` | итоги вылазки (опыт) | мозг со стрелкой вверх | Action_Hack |
| `Icon_AIRequest.png` | карточка ИИ, строка «Просит» | облачко-запрос с восклицательным знаком, фиолетовый | Icon_DiveReport |
| `Icon_Trace.png` | HUD нырка, шкала «След» | отпечаток пальца из пикселей | — |
| `Icon_Settings.png` (Textures/UI/Netrun/) | заголовок окна настроек мода | шестерёнка-микросхема | — |

## Обновление 2026-10-06 (вечер)
- Подключено из папки арта: Gizmo_AI_* (новые версии), Gizmo_CallFixer, Tab_Overview, Tab_Log (VarC), Gizmo_NetTerminal (VarC), Icon_AIRequest (из Gizmo_AI_Demand_VarH3), Icon_Xp (из Icon_Experience), Icon_Netsick (из Icon_NetSick_10E_GlitchSlices), NR_AIServer (из AIServer, 256 px), NR_DistressServer (из NR_DistressServer_3x3, 384 px, на карте drawSize 2.4 при клетке 1x1), Icon_Pinned (из Icon_Pinned_Diag_Dim).
- Иконки ступеней нырка нарисованы мной как белые линейные заготовки (тонируются цветом ступени): Dive_LOBBY, Dive_FILES, Dive_MEMORY, Dive_EXIT, Dive_HIDDEN (Textures/UI/Netrun/Cyberspace, 64 px). Если захочешь фирменные — те же имена, белый контур на прозрачном.
- Всё ещё нужно: Report_Success / Report_Retreat / Report_Fail (статусы итогов), Icon_Trace, Icon_Settings, фирменный Icon_Guide.
