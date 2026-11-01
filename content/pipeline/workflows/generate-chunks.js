export const meta = {
  name: 'burdock-words-generate',
  description: 'Generate, review and fix a group of word inventory chunks with the generate-v1 / review-v1 prompts (chunks come from args)',
  phases: [{ title: 'Generate' }, { title: 'Review' }, { title: 'Fix' }],
}
const CHUNKS = args && args.chunks ? args.chunks : []
if (!CHUNKS.length) throw new Error('args.chunks is required')
const ROOT = 'D:/WORK/SOURCE_CODE/Burdock'
const GEN_SCHEMA = { type: 'object', properties: { chunk: { type: 'string' }, entries: { type: 'number' }, errors: { type: 'number' }, note: { type: 'string' } }, required: ['chunk', 'entries', 'errors'] }
const REV_SCHEMA = { type: 'object', properties: { chunk: { type: 'string' }, items: { type: 'number' }, flagged: { type: 'number' }, errors: { type: 'number' } }, required: ['chunk', 'items', 'flagged', 'errors'] }
const FIX_SCHEMA = { type: 'object', properties: { chunk: { type: 'string' }, regenerated: { type: 'number' }, errors: { type: 'number' }, note: { type: 'string' } }, required: ['chunk', 'regenerated', 'errors'] }

const generatePrompt = (chunk) => `You generate one chunk of the Ukrainian–English learning dictionary of «Реп'ях» (repo ${ROOT}). The chunk contains single WORDS whose level was estimated by a model; each input lexeme carries glossEn (the intended sense) and topics hints.

1. Read the whole prompt file content/pipeline/prompts/generate-v1.md and the input chunk content/generated/uk-en/inventory/${chunk}.json (up to 40 lexemes with id, lemma, pos, level, acceptedForms, glossEn, topics).
2. Write content/generated/uk-en/chunks/${chunk}.json following the output schema of generate-v1 exactly: header {"schemaVersion":1,"chunk":"${chunk}","level":"<level from the input>","provenance":{"model":"claude-code/claude-fable-5-1","promptVersion":"generate-v1","generatedAt":"2026-11-01T00:00:00+00:00","status":"draft"},"entries":[...]}. One entry per input lexeme, same order, same id/lemma/pos/level. Translate the sense given by glossEn; the input topics are hints, keep only those that are really typical. All Ukrainian text with the apostrophe U+2019 (’), never the ASCII one; no letters ы э ъ ё.
3. Write INCREMENTALLY, the file must be valid JSON after every step: first Write the header plus the first 5 entries, then use Edit to append 5 entries at a time (insert before the closing "]"), until every input lexeme is present. Never produce the whole file in one go and never create scratch files.
4. Finish with: python content/pipeline/packs/check_chunk.py ${chunk} (run from the repo root). It must print {"chunk": "${chunk}", "errors": 0, ...}. Fix every ERROR it reports with Edit and run it again; WARN lines are acceptable but fix them when cheap.
5. Return JSON {chunk, entries, errors, note} with the final counts and a one-line note.`

const reviewPrompt = (chunk) => `You review one generated chunk of the Ukrainian–English learning dictionary of «Реп'ях» (repo ${ROOT}). The chunk contains single WORDS with model-estimated levels.

1. Read content/pipeline/prompts/review-v1.md, the generated chunk content/generated/uk-en/chunks/${chunk}.json and the inventory content/generated/uk-en/inventory/${chunk}.json (its glossEn is the intended sense of each word; judge accuracy against it).
2. Write content/generated/uk-en/reviews/${chunk}.review.json following the review schema exactly: header {"schemaVersion":1,"chunk":"${chunk}","reviewer":{"model":"claude-code/claude-fable-5-1","promptVersion":"review-v1","reviewedAt":"2026-11-01T00:00:00+00:00"},"items":[...]}; one item per entry, same order and ids, integer scores 1–5 for accuracy, naturalness, levelFit, safety, grammarNotes, topics; autoReject / needsRegeneration booleans per the rules; issues with reasonCode from the allowed list and a short Ukrainian suggestion; primaryAlternative or null.
3. Write INCREMENTALLY: first Write the header plus the first 10 items, then Edit to append 10 at a time (valid JSON after every step). Do not modify the chunk file. No scratch files.
4. Finish with: python content/pipeline/packs/check_chunk.py ${chunk} --review (repo root). It must report errors 0 for the review part; fix what it reports.
5. Return JSON {chunk, items, flagged, errors} where flagged = items with needsRegeneration or autoReject.`

const fixPrompt = (chunk, flagged) => `You fix the flagged entries of one generated chunk of the «Реп'ях» dictionary (repo ${ROOT}); the chunk contains single WORDS. The review flagged ${flagged} entries.

1. Read content/pipeline/prompts/generate-v1.md, the review content/generated/uk-en/reviews/${chunk}.review.json, the chunk content/generated/uk-en/chunks/${chunk}.json and the inventory content/generated/uk-en/inventory/${chunk}.json (glossEn = intended sense).
2. For every review item with needsRegeneration or autoReject true: rewrite that entry in the chunk file with Edit (replace just that entry's JSON object), keeping id, lemma, pos and level, addressing every issue and the primaryAlternative when the reviewer is right, and add the field "regenerated": 1 to the entry. Keep the order and leave other entries untouched. Edit one entry at a time; the file must stay valid JSON. No scratch files.
3. Finish with: python content/pipeline/packs/check_chunk.py ${chunk} (repo root). It must print errors 0; fix what it reports.
4. Return JSON {chunk, regenerated, errors, note}.`

const results = await pipeline(
  CHUNKS,
  (chunk) => agent(generatePrompt(chunk), { label: `generate:${chunk}`, phase: 'Generate', schema: GEN_SCHEMA, agentType: 'general-purpose', effort: 'medium' }),
  async (gen, chunk) => {
    if (!gen || gen.errors > 0) log(`${chunk}: generation left ${gen ? gen.errors : 'n/a'} errors; reviewing anyway`)
    const rev = await agent(reviewPrompt(chunk), { label: `review:${chunk}`, phase: 'Review', schema: REV_SCHEMA, agentType: 'general-purpose', effort: 'medium' })
    return { gen, rev }
  },
  async (prev, chunk) => {
    const flagged = prev && prev.rev ? prev.rev.flagged : 0
    if (!flagged) return { chunk, gen: prev && prev.gen, rev: prev && prev.rev, fix: { chunk, regenerated: 0, errors: 0, note: 'nothing flagged' } }
    const fix = await agent(fixPrompt(chunk, flagged), { label: `fix:${chunk}`, phase: 'Fix', schema: FIX_SCHEMA, agentType: 'general-purpose', effort: 'medium' })
    return { chunk, gen: prev.gen, rev: prev.rev, fix }
  }
)
const summary = results.filter(Boolean).map(r => ({ chunk: r.chunk, entries: r.gen ? r.gen.entries : null, genErrors: r.gen ? r.gen.errors : null, flagged: r.rev ? r.rev.flagged : null, regenerated: r.fix ? r.fix.regenerated : null, fixErrors: r.fix ? r.fix.errors : null }))
log(`done ${summary.length}/${CHUNKS.length} chunks`)
return summary
