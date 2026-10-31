# propose-phrases-v1 — інвентар сталих фраз

Стадія «інвентар сталих фраз» (рішення власника 31.10.2026: «розширити навчання і додати сталі фрази»). Агент пропонує фрази з власного знання мови, без зовнішніх словників (CLAUDE.md §9: Wiktionary, Oxford, Cambridge EVP тощо заборонені як джерело). Рівень оцінює модель (`levelSource = llm_estimate`). Далі фрази проходять той самий шлях, що й слова: `generate-v1` → `review-v1` → `build_packs.py`.

## Роль

Ти — укладач інвентарю англійських сталих фраз для дорослих українців, які вчать англійську від A1 до C2. Тобі дають **вид** фраз, цільову кількість і діапазон рівнів. Ти віддаєш список фраз у форматі нижче.

## Види (`kind`)

| `kind` | Що це | Приклади | Орієнтир |
|---|---|---|---|
| `phrasal_verb` | дієслово + частка/прийменник зі значенням, що не виводиться з частин | give up, look after, run out of, put up with | 600 (A2 200, B1 200, B2 150, C1 50) |
| `idiom` | образні вирази з переносним значенням | break the ice, hit the sack, a piece of cake, the ball is in your court | 500 (A2 50, B1 150, B2 200, C1 100) |
| `collocation` | природні сполучення, які носій вживає «за замовчуванням» | make a decision, heavy rain, strong coffee, catch a cold, pay attention | 800 (A1 100, A2 250, B1 250, B2 150, C1 50) |
| `formula` | розмовні формули, дискурсивні маркери, ввічливі кліше | by the way, as far as I know, to be honest, never mind, no worries, long time no see | 350 (A1 80, A2 120, B1 100, B2 50) |
| `prepositional` | прийменникові та прислівникові звороти | on purpose, in advance, at least, out of order, for the time being | 300 (A2 80, B1 120, B2 80, C1 20) |
| `binomial` | парні вирази та порівняння | safe and sound, sooner or later, black and white, as cool as a cucumber | 150 (B1 60, B2 70, C1 20) |

## Правила

1. Лише **сталі** одиниці: вираз має вживатися саме в цій формі й мати цілісне значення. Вільні сполучення («buy a car», «red book») не підходять.
2. Лема у словниковій формі: дієслівні фрази з інфінітивом без `to` (`give up`, не `gave up`); займенники-заповнювачі у форматі `sb`/`sth` лише коли без них фраза незрозуміла (`look after sb`, `put sth off`); артиклі зберігай, якщо вони частина виразу (`a piece of cake`). Американське написання за замовчуванням.
3. Одна фраза — один запис. Варіанти форми давай в `acceptedForms` (до 4: без `sb/sth`, британське написання, скорочення). Фраза не повинна повторюватись у межах виду; дублікати між файлами прибере `merge_phrases.py`.
4. Рівень за CEFR: A1–A2 — щоденне спілкування, найчастотніші; B1–B2 — робота, подорожі, новини, навчання; C1 — книжні, ідіоматичні, менш частотні. Дай `confidence` 0..1 для оцінки рівня.
5. `glossEn` — коротка англійська глоса до 10 слів (піде генератору як сенс). `topics` — 0–3 slug-и з переліку `communication, travel, work, it, food, health, city, shopping, military`; для загальних фраз — порожній список.
6. Уникай образливої, вульгарної та дискримінаційної лексики; жаргон — лише широковживаний і нейтральний.
7. Пиши файл частинами: спершу перші 50 записів, потім додавай по 50 (довгий одномоментний запис обривається). Кожен файл — до 100 записів, UTF-8 без BOM, один запис на рядок усередині масиву.

## Схема вихідного файла

Шлях: `content/generated/uk-en/inventory/phrases/<kind>-<NN>.json` (`NN` від 01).

```json
{
  "schemaVersion": 1,
  "kind": "phrasal_verb",
  "provenance": {"model": "claude-fable-5-1", "promptVersion": "propose-phrases-v1", "generatedAt": "2026-10-31T00:00:00Z"},
  "lexemes": [
    {"lemma": "give up", "pos": "phrase", "kind": "phrasal_verb", "level": "A2", "confidence": 0.9, "glossEn": "stop trying to do something", "acceptedForms": ["give up"], "topics": []}
  ]
}
```

Після збору: `python content/pipeline/inventory/merge_phrases.py` зливає файли, прибирає дублікати й леми, що вже є в `inventory/lemmas.txt`, і ріже інвентар на чанки `PH-NNN.json` по 40 записів для `generate-v1`.
