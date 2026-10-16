# Описание мода для Steam Workshop

Всё генерирует `python tools/gen_steam.py` (тексты EN/RU, баннер и 11 заголовков разделов в стиле терминала, шрифт Tektur). Лимит описания в Steam — 8000 символов, сейчас 6600–7700.

## Два варианта
- `description_en.txt`, `description_ru.txt` — заголовки текстом (`[h1]`). Можно вставлять сразу.
- `description_en_images.txt`, `description_ru_images.txt` — заголовки картинками, как у Vanilla Expanded. Нужно один раз залить картинки.

## Ссылки на картинки
По умолчанию скрипт ставит в `_images.txt` ссылки на файлы в GitHub по тегу релиза: `https://raw.githubusercontent.com/GafarovMaxim/RimWorld-Netrun/v<modVersion>/docs/steam/img/<имя>.png` (версия берётся из `About/About.xml`). Ссылки начинают работать, когда тег отправлен на GitHub. Другой хостинг: положить прямые ссылки в `docs/steam/urls.json` (`{"en_banner": "https://...", ...}`, ключи — имена файлов без `.png`) и запустить скрипт ещё раз.

Английский вариант — основное описание. Русский можно выложить отдельным языком в Steam (у описания есть языки) или в обсуждения мода.

## Превью 1920x1080
`python tools/gen_steam_previews.py --lang ru|en [--only terminal,cyberspace,club]` собирает превью в `docs/steam/previews/` из скриншотов в папке Preview (вне репозитория) и мокапов из `docs/mockups`, `docs/ui-mockups`.

## Описание из картинок (как у Vanilla Expanded)
`python tools/gen_steam_cards.py` рисует 11 карточек `img/en_card_*.png` (заголовок раздела, скриншот, пункты) и `description_en_cards.txt`: баннер, вступление, цепочка карточек, требования и благодарности текстом (ссылки кликабельны). Ссылки на картинки берутся по тегу версии из About.xml.
