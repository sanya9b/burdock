# Реєстр джерел контенту

Єдине місце, де фіксуються джерела даних бази слів, їхні ліцензії, рядки цитування, дати завантаження й контрольні суми. Команда `lint --licenses` порівнює вміст `content/sources/` з цим реєстром і блокує збірку, якщо знайдено файл без запису або з забороненою ліцензією. Правила: [08-content-pipeline, розд. 3.1](../../docs/specs/08-content-pipeline.md); рішення: [журнал, Q37](../../docs/decisions/2026-09-25-interview.md).

## Джерела, що використовуються

| ID джерела | Назва / версія | Що беремо | Ліцензія | URL | Рядок цитування / атрибуції | Дата завантаження | SHA-256 файлу |
|---|---|---|---|---|---|---|---|
| `cefrj-1.6` | CEFR-J Wordlist Version 1.6 (оновлено 2020-03-24), Tono Laboratory, TUFS | Леми, частини мови, рівні A1-B2 (7 801 запис) | Умови CEFR-J: дослідницьке й комерційне використання з належним цитуванням; примітка про «витрати після окремої консультації» для комерційного використання — потрібне письмове підтвердження (відкрите питання 7) | https://www.cefr-j.org/download_eng.html | `The CEFR-J Wordlist Version 1.6. Compiled by Yukio Tono, Tokyo University of Foreign Studies. Retrieved from https://www.cefr-j.org/download_eng.html on DD/MM/YYYY.` | `YYYY-MM-DD` (заповнити) | `(заповнити)` |
| `oewn-2025` | Open English WordNet, 2025 edition (WN-LMF XML) | Синсети, глоси, usage-домени | CC BY 4.0; зберігається повідомлення Princeton WordNet (файл `WNDB_License.txt` дистрибутива) | https://github.com/globalwordnet/english-wordnet/releases/tag/2025-edition | `This product includes data from Open English WordNet 2025 (https://github.com/globalwordnet/english-wordnet), licensed under CC BY 4.0, derived from Princeton WordNet. WordNet 3.1 Copyright 2011 by Princeton University. All rights reserved.` + повний текст WNDB-повідомлення в ATTRIBUTIONS.md | `YYYY-MM-DD` (заповнити) | `(заповнити)` — файл не комітиться, завантажується скриптом `oewn/download.sh` |
| `cmudict-0.7b` | CMU Pronouncing Dictionary (cmusphinx/cmudict, `cmudict.dict`) | ARPAbet-транскрипція → IPA GA | BSD-2-Clause | https://github.com/cmusphinx/cmudict/blob/master/LICENSE | `Copyright (C) 1993-2015 Carnegie Mellon University. All rights reserved.` + повний текст умов і disclaimer у ATTRIBUTIONS.md | `YYYY-MM-DD` (заповнити) | `(заповнити)` |
| `claude-sonnet-5`, `claude-opus-5-5` | Anthropic API (генерація і рецензування) | Переклади, приклади, класифікація тем, RP-транскрипція, оцінки якості | Anthropic Commercial Terms: права на Outputs передаються замовнику; обов'язок повідомляти користувачів, що фактичні твердження можуть бути хибними | https://www.anthropic.com/legal/commercial-terms | Повідомлення в застосунку: «Переклади та приклади створено з допомогою моделей Claude (Anthropic) і відредаговано редакторами «Реп'яха». Попри перевірку, окремі формулювання можуть містити помилки.» | — (версія моделі фіксується в `provenance.model_id`) | — |
| `owner` | Власні списки власника (`content/own-lists/`) | Лексеми, переклади, приклади, теми | Власний контент | — | — | дата коміту | — |

## Джерела лише для внутрішнього використання (не відвантажуються)

| Джерело | Ліцензія | Дозволене використання | Заборонено |
|---|---|---|---|
| wordfreq (дані) | CC BY-SA 4.0 на дані (код Apache-2.0) — https://github.com/rspeer/wordfreq | Лише як внутрішній критерій черговості рецензування у таблиці `internal_ordering`; результати не потрапляють у майстер-таблиці й пакети | Будь-яке поле пакета, похідне від цих даних; коміт дампів у репозиторій |
| SUBTLEX-US | CC BY-NC-SA 4.0 — https://osf.io/download/eb2cq/ | Не використовується навіть внутрішньо (NonCommercial) | Усе |
| Wiktionary (сайт) | CC BY-SA 4.0 + GFDL | Довідник рецензента без копіювання тексту (див. `content/review/REVIEWER_GUIDE.md`) | Копіювання перекладів, прикладів, глос, транскрипцій; імпорт дампів Kaikki |

## Кандидати на джерело частотності (рішення не ухвалене, див. 08-content-pipeline, питання 7.2, і відкрите питання 10 журналу рішень)

| Кандидат | URL | Що показала попередня перевірка 27.09.2026 | Статус |
|---|---|---|---|
| Google Books Ngram (Version 3, 2020) | https://storage.googleapis.com/books/ngrams/books/datasetsv3.html | Сторінка наборів даних заявляє ліцензію Creative Commons Attribution 3.0 Unported | Потребує підтвердження власником і фіксації рядка атрибуції; великий обсяг даних — у репозиторій потрапляє лише похідний файл смуг для ~10k лем у `derived/` |
| hermitdave/FrequencyWords | https://github.com/hermitdave/FrequencyWords | README заявляє MIT для коду і CC BY-SA 4.0 для даних (похідні від OpenSubtitles) | Не підходить для відвантаження; можливе лише внутрішнє використання як у wordfreq |
| Leipzig Corpora Collection | https://wortschatz.uni-leipzig.de/en/download | Умови не перевірені | Не використовувати до перевірки |

До ухвалення рішення поле `frequency_band` у пакетах = `null`.

## Заборонені джерела даних

| Джерело | Причина |
|---|---|
| Wiktionary / витяг Kaikki | CC BY-SA 4.0 + GFDL: каскад ShareAlike на всю базу (https://kaikki.org/dictionary/rawdata.html) |
| Octanove Vocabulary Profile (C1/C2) | CC BY-SA 4.0 |
| Kelly list | CC BY-NC-SA 2.0 (https://ssharoff.github.io/kelly/) |
| VESUM / dict_uk | CC BY-NC-SA 4.0 (https://github.com/brown-uk/dict_uk) |
| Oxford 3000 / 5000 | Усі права застережено, торгова марка; GitHub-копії успадковують ризик (https://www.oxfordlearnersdictionaries.com/legal-notice) |
| Cambridge English Vocabulary Profile | Умови забороняють програмні продукти на основі даних EVP (https://web.archive.org/web/20230603152537/https://languageresearch.cambridge.org/wordlists/terms-of-use) |
| COCA / wordfrequency.info | Платна ліцензія, обмежені смуги, «відбитки» у даних (https://www.wordfrequency.info/license.asp) |
| SUBTLEX-US напряму | CC BY-NC-SA 4.0 (https://osf.io/download/eb2cq/) |
| Скани паперових словників | Немає ліцензії |
| FreeDict | Немає пари eng-ukr (https://freedict.org/freedict-database.json) |
| Tatoeba | Не заборонене, але **не використовується у v1** (T9): CC BY 2.0 FR вимагає атрибуції на кожне речення, що неможливо на віджетах; увімкнення лише через ADR |
| Forvo, Apple System Voices, платні TTS | Аудіо не входить у v1 (T7); Apple SLA забороняє поширення системних голосів |

## Як додати джерело

1. Переконатися, що ліцензія дозвільна (CC BY, BSD, MIT, Apache, public domain, власні дані) і не має ShareAlike/NonCommercial/NoDerivatives.
2. Додати рядок у таблицю вище з ID, URL, рядком атрибуції, датою та SHA-256.
3. Додати запис у таблиці `source`/`license` через міграцію пайплайну; оновити `attribution_template`.
4. Якщо файл > 10 МБ — скрипт завантаження + SHA-256 замість коміту.
5. Запустити `burdock-content lint --licenses` і `attributions render`; закомітити оновлений `ATTRIBUTIONS.md`.
