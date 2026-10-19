# Релиз в Steam Workshop: 1.1.11

Обновлено 2026-10-17. Версия 1.1.11 (патчи к 1.1.0: заброшенный сервер, Combat Extended, стрелки у наёмников, квесты и частота событий, упакованная капсула, вид серверов сбоку, тактика и сила наёмников, брейнданс-очки со шлемами, Переход (полоса, питание, концовка), окно «Сеть найдена», выброс из обесточенной капсулы, курьер в укрытии и охотники за ним, блокировка энергосети, раненый курьер и драка в укрытии, консоли связи из других модов; картинки описания по-прежнему из тега `v1.1.0`) в `About.xml`, `Source/Netrun.csproj`, `SourceVAE/NetrunVAE.csproj`; поддержка RimWorld 1.6 и 1.7. Прежний чек-лист (состояние на 2026-10-08) в git-истории этого файла.

## 1. Готово
- `About/About.xml`: имя, автор Raul Moreau, packageId `GafarovMaxim.HackingExpansionNetrun`, версии 1.6 и 1.7, зависимости HE, Harmony, VEF; описание обновлено под 1.1.0.
- `About/Preview.png`: превью v3 (640x360). `About/PublishedFileId.txt` (3819916266) привязывает загрузку к странице мода; без него RimWorld создаёт новую страницу. Превью для страницы Workshop: 9 штук 1920x1080 в `docs/steam/previews/` (`en_*.png`, `ru_*.png`), генератор `tools/gen_steam_previews.py`.
- `CHANGELOG.md`: разделы [1.1.11]–[1.1.0] - 2026-10-19.
- `README.md`, `README_RU.md`: версии 1.6 и 1.7, экран навигации, карта с линзами, защищённые хабы, тренировочный нырок, «Переход», наёмники Black Ledger.
- Описание Workshop: `docs/steam/description_{en,ru}.txt` (заголовки текстом) и `description_{en,ru}_images.txt` (заголовки картинками), все меньше 8000 знаков. Добавлен раздел «Переход», метка «1.6 / 1.7» на баннере.
- Описание из картинок (как у Vanilla Expanded): `docs/steam/description_{en,ru}_cards.txt`, 11 карточек `img/{en,ru}_card_*.png` (`tools/gen_steam_cards.py`). Основной вариант для загрузки.
- Картинки описания: ссылки на GitHub по тегу `v1.1.0` (`https://raw.githubusercontent.com/GafarovMaxim/RimWorld-Netrun/v1.1.0/docs/steam/img/...`). Репозиторий публичный. Imgur не нужен.
- Код: все Harmony-патчи в try/catch, коды ErrorOnce уникальны; информационные Log.Message только в dev-режиме.
- CI: zip только из About, Assemblies, Defs, Languages, Optional, Patches, Sounds, Textures, README.md, LICENSE; собирает обе DLL сам (VAE-часть тоже). По тегу `v*` прикрепляет zip к GitHub Release.
- Исходный арт вынесен из репозитория в `G:\Modding\RimWorld\Hacking Expansion Netrun\Reserve`.

## 2. Порядок публикации (делает Maxim)
1. Закрыть игру, собрать: `dotnet build Source/Netrun.csproj -c Release` (DLL в репозитории обновится; в zip CI её соберёт сам).
2. Проверить в игре новую игру и старое сохранение: нет красных строк от мода в `Player.log`.
3. Поставить тег на последний коммит `dev` и отправить его: `git tag v1.1.0`, `git push origin v1.1.0`. CI сделает Release с `HackingExpansionNetrun.zip`, а ссылки на картинки описания начнут работать.
4. Скачать zip из Release и распаковать в отдельную папку в `Mods` (например `Mods\HackingExpansionNetrun`). Загружать в Workshop только её: в папке `_Dev` лежат `.git`, `Source`, `SourceVAE`, `tools`, `docs`, `.github`, `CLAUDE.md`, они не должны попасть в Steam. Пока обе папки в `Mods`, у них один packageId: на время загрузки вынести `_Dev` из `Mods` или выключить её в списке модов.
5. RimWorld → Моды → выбрать чистую папку → «Загрузить в Steam Workshop». Видимость сначала «только по ссылке».
6. На странице Workshop: вставить `docs/steam/description_en_cards.txt` (основной язык), русский `description_ru_cards.txt` добавить как язык описания; 9 превью из `docs/steam/previews/en_*.png`; теги Workshop.
7. Проверить страницу по ссылке, потом открыть для всех.

## 3. Теги Workshop
Mod, 1.6, 1.7, Cyberpunk, Hacking, Events, Factions, Quests, UI, Translation: Russian.

## 4. Подписи к скриншотам (EN / RU)
1. The network terminal: the navigation screen with the Heat ring / Сетевой терминал: экран навигации и кольцо Heat
2. The cyberspace map with the danger lens / Карта киберпространства с линзой опасности
3. A dive: forks, code gates and Black ICE / Нырок: развилки, кодовые врата и чёрный лёд
4. The Club: fixers, corporations, crews and the top-100 / Клуб: фиксеры, корпорации, команды и топ-100
5. The server room: thirteen AI personalities / Серверная: тринадцать личностей ИИ
6. The Studio: braindance recordings and chips / Студия: записи и чипы брейнданса
7. Hub defense: firewalls and ICE tiers / Защита узла: брандмауэры и уровни льда
8. The Cyberpedia / Киберпедия

## 5. Не должно попасть в Workshop
`docs/`, `tools/`, `Source/`, `SourceVAE/`, `.git`, `.github`, `build.bat`, `CLAUDE.md`, `SECURITY.md`, `Test.txt`. В zip CI их нет; перед загрузкой посмотреть содержимое папки, размер около 8 МБ.

## 6. Риски
- `docs/` в репозитории нужна для ссылок на картинки описания: они читаются из тега `v1.1.0`. Удалять `docs` только в копии для загрузки, не коммитить удаление.
- Ветка `main` отстаёт от `dev`; релиз идёт по тегу, `main` не трогаем без решения Maxim.
- Сборка идёт против ссылок 1.6, игра 1.7: перед публикацией проверить в 1.7 (лог чистый).
