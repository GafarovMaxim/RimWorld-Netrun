# Описание мода для Steam Workshop

Всё генерирует `python tools/gen_steam.py` (тексты EN/RU, баннер и 11 заголовков разделов в стиле терминала, шрифт Tektur). Лимит описания в Steam — 8000 символов, сейчас около 5700.

## Два варианта
- `description_en.txt`, `description_ru.txt` — заголовки текстом (`[h1]`). Можно вставлять сразу.
- `description_en_images.txt`, `description_ru_images.txt` — заголовки картинками, как у Vanilla Expanded. Нужно один раз залить картинки.

## Как сделать вариант с картинками
1. Залить `img/en_*.png` (или `img/ru_*.png`) на Imgur или другой хостинг, который отдаёт прямые ссылки (`https://i.imgur.com/....png`).
2. Создать `docs/steam/urls.json` с прямыми ссылками:
   ```json
   {"en_banner": "https://i.imgur.com/xxxx.png", "en_heat": "https://i.imgur.com/yyyy.png"}
   ```
   Ключи — имена файлов без `.png`: `banner`, `heat`, `terminal`, `cyberspace`, `fixers`, `corps`, `ais`, `braindance`, `defense`, `colonists`, `requirements`, `credits` с префиксом `en_` или `ru_`.
3. Запустить `python tools/gen_steam.py` ещё раз: ссылки встанут вместо `{{en_heat}}` и других меток.
4. Вставить текст в описание мода в Steam.

Английский вариант — основное описание. Русский можно выложить отдельным языком в Steam (у описания есть языки) или в обсуждения мода.
