# Dating Reply — "Keep it engaging & spicy"

> A new **skill** for the Mac On-Screen Chat assistant. Paste the last **4–5
> messages** from a conversation with a girl; get back a few ready-to-send
> replies that keep the thread alive, playful, and (dialed to taste) spicy —
> without sounding like a bot, a pickup line, or a creep.

This plugs into the existing skill architecture (`Skill.defaults` in
[Models.swift](Sources/MacOnScreenChat/Models.swift), seeded into SQLite by
[AppDatabase.swift](Sources/MacOnScreenChat/AppDatabase.swift)). Adding it is
one skill entry + a DB migration — no UI rewrite.

---

## 1. Goal

Given a short slice of a real chat, produce replies that:
- **Match her energy** — mirror her length, tone, and emoji use. Never out-invest her.
- **Move the conversation forward** — every reply gives her something easy to react to.
- **Are choice, not homework** — output a few labeled options at rising heat so I pick.
- **Sound like me texting** — casual, lowercase-ish, short. Not an essay, not a poem.

**Non-goals:** manipulation, negging, mind-games, love-bombing, or anything that
ignores a clear "not interested" signal. Those kill attraction and are just gross.

---

## 2. The reply framework (researched)

Distilled from texting/attraction guides (sources at the bottom). These are the
levers the prompt is built on:

1. **Read her interest first.** Is she investing (long replies, questions, emojis,
   fast) or coasting (one-word, dry, slow)? Everything downstream adapts to this.
   - *High interest* → it's safe to tease harder and escalate.
   - *Low / dry* → re-engage with curiosity or a pattern-break; **do not** escalate
     into a dry thread — that's how you get left on read.
2. **Match & mirror.** Her message length and emoji density set the ceiling for mine.
3. **Push–pull.** Show interest, then playfully pull back or tease something minor.
   Creates a cat-and-mouse spark instead of a flat "that's cool 😊".
4. **Playful teasing + a genuine compliment.** Teasing alone reads mean; compliment
   alone reads try-hard. Mixed = flirty and grounded.
5. **Open-ended hooks.** End on something she can run with. Kill "how was your day"
   → replace with specific, curious, slightly cheeky bait.
6. **Callbacks & inside jokes.** Reference something from earlier in the thread; it
   manufactures "us" and continuity.
7. **Build anticipation.** Hint at the next thing (a plan, a "you'll find out"),
   don't dump everything at once. Mystery > over-explaining.
8. **Escalate gradually.** Start subtle, raise the heat only if she mirrors it.
   Innuendo and suggestion beat anything explicit early. Respect > desire, always.
9. **Emoji as tone.** Text has no voice; a well-placed emoji signals "this is a
   joke / a wink" so teasing lands as flirty, not hostile.

### Output contract
Three labeled options, escalating heat, so I choose based on the room:
- **Playful** — light, funny, low-risk banter. Safe for any interest level.
- **Flirty** — teasing + warmth, clear romantic intent, still classy.
- **Spicy** — bolder, more suggestive/innuendo. *Only lean here if she's clearly
  reciprocating.* If her messages read dry/uninterested, the prompt says so and the
  "Spicy" slot pivots to a re-engagement line instead of forcing heat.

Each option: 1–2 texts max, matches her tone, ends with a hook where natural,
plain text (no markdown), emojis only to match her usage.

---

## 3. Build phases

### Phase 0 — Research & framework ✅
- [x] Scrape texting/flirting/attraction guides for concrete, repeatable techniques.
- [x] Distill into the 9-lever framework + the 3-tier output contract above.

### Phase 1 — Ship the skill (core) ✅
- [x] Add a `Skill` entry (`id: "dating"`, name **Dating reply**) to
      `Skill.defaults` with the engineered system prompt encoding §2.
- [x] Add a DB migration (`v3.seedDatingSkill`) so it also appears for the
      **existing** install (the table is only auto-seeded when empty).
- [x] `swift build` clean; skill shows in the picker; end-to-end reply works.

### Phase 2 — Prompt tuning (iterate on real threads) ✅
- [x] Ran 4 conversation slices (high-interest, dry, medium, she-teases-me) through
      the prompt on both the local model and Gemini.
- [x] **Rewrote the prompt (v2):** the original "WORK THROUGH THIS SILENTLY →
      1/2/3" scaffold made the model narrate its reasoning. v2 leads with hard
      OUTPUT RULES ("start immediately with Playful:", no preamble) and folds the
      analysis into terse write-guidance. Also banned `*emphasis*` asterisks.
- [x] Re-synced the tuned prompt into the existing DB via migration
      `v4.tuneDatingPrompt` (defaults alone don't reach an already-seeded row).
- [x] **Verified behavior** on Gemini 2.5 Flash:
      - High-interest → escalates, mirrors her words + 😏, spicy stays classy.
      - Dry thread → **Spicy correctly pivots to a re-engagement line**, no forced
        heat, no emojis (she used none). The core safety rule works.
      - Medium → escalates on the real hook, no emojis to match her.
- [x] Edit further live via the in-app **Edit "Dating reply"…** sheet (no rebuild).

**Model finding (important):** the local **qwen3:30b narrates its reasoning**
before the answer regardless of `think:false` or `/no_think` — a quirk of that
reasoning model, not the prompt. **Gemini 2.5 Flash follows the format cleanly**
and is the recommended model for this skill. (Gemini 2.5 **Pro** free-tier is what
threw the earlier HTTP 429 — Flash has much higher limits.)

### Phase 3 — Spice dial ✅
- [x] Added a **heat dial** — a segmented control (**Auto · Cool · Warm · Spicy**)
      that appears *only* when "Dating reply" is the selected skill.
      ([DatingHeat.swift](Sources/MacOnScreenChat/DatingHeat.swift),
      [ContentView.swift](Sources/MacOnScreenChat/ContentView.swift).)
- [x] It **calibrates how far the replies escalate** by appending a directive to
      the prompt at send time — no DB / schema change needed. `Auto` (default)
      injects nothing, so the model keeps reading her interest straight from the
      thread (the Phase-2 behavior); `Cool`/`Warm`/`Spicy` override that read.
- [x] Persists across launches via `@AppStorage("datingHeat")`.
- [x] **Verified** on Gemini 2.5 Flash: same warm thread stays restrained on
      `Cool` and escalates (bolder, anticipation-building) on `Spicy`.
- Chose this over `Skill.params` plumbing: the injected-directive approach is
      zero-schema and keeps the loved 3-option output intact.

### Phase 3b — "My style" personalization ✅
- [x] Added an editable **"My texting style"** field (skill menu → *My texting
      style…*, shown only for Dating reply).
      ([DatingStyleEditor.swift](Sources/MacOnScreenChat/DatingStyleEditor.swift).)
- [x] Stored in `@AppStorage("datingStyle")` — separate from the skill prompt, so
      it survives prompt re-tuning migrations. Injected into the prompt at send
      time as a "MY TEXTING STYLE (write all three options in THIS voice…)" block,
      before the heat calibration. Empty = no block (just match her energy).
- [x] **Verified** on Gemini 2.5 Flash: a "dry, deadpan, all-lowercase, one line,
      no emojis" style flipped the default gushy/emoji output into terse deadpan
      one-liners — voice matched the description.

### Inline steering — `WW:{…}`
- [x] Type `WW:{your instruction}` anywhere in the input to steer THIS reply — it's
      injected as a highest-priority instruction that overrides the skill's own
      formatting, and stripped out of the transcript.
      ([ChatViewModel.swift](Sources/MacOnScreenChat/ChatViewModel.swift) —
      `extractDirective`.) Works for **every** skill, not just Dating reply.
      Examples: `WW:{one savage line, no labels}`, `WW:{shorter}`,
      `WW:{make her laugh}`, `WW:{in Hindi}`.
- [x] Verified end-to-end: `WW:{…give me ONE savage one-liner, no labels}` broke
      the default 3-option format and returned a single line; the `WW:{…}` text was
      removed from the sent message.

**Status: dating skill feature-complete** — reads her interest, outputs
Playful/Flirty/Spicy, with a heat dial (Phase 3) and my-voice personalization
(Phase 3b). Best model: Gemini 2.5 Flash.

### Phase 4 — Quality of life (optional)
- [ ] One-tap "regenerate hotter / cooler".
- [ ] Remember the girl's name / running context per thread (multi-conversation
      history is already a deferred item in `workflow.md`).

---

## 4. The skill (reference)

```jsonc
{
  "id": "dating",
  "name": "Dating reply",
  "inputHint": "Paste the last 4–5 messages…",
  "systemPrompt": "…encodes the §2 framework: read interest → match tone → output Playful / Flirty / Spicy, with the dry-thread pivot and no-manipulation guardrails…"
}
```

Adding it = one object in `Skill.defaults` + one migration. Tune the prompt
forever after from inside the app.

---

## 5. Sources
- [The Art of Charm — Playful banter into flirting](https://theartofcharm.com/art-of-dating/a-mindset-that-can-turn-any-playful-banter-into-flirting-with-examples/)
- [Jaunty — How to flirt over text: 12 tips](https://www.jaunty.org/blog/how-to-flirt-over-text/)
- [Boo — Flirty texting tips & examples](https://boo.world/resources/flirting-over-text)
- [Roast Dating — Get a reply every time you flirt over text](https://roast.dating/blog/flirt-over-text)
- [The Art of Charm — Attraction signals: when to escalate](https://theartofcharm.com/art-of-dating/attraction-signals-when-to-escalate/)
- [Text Weapon — Build sexual tension over text](https://textweapon.com/how-to-build-sexual-tension-with-a-girl-over-text/)
- [Beyond Ages — Build sexual tension over text](https://beyondages.com/how-to-build-sexual-tension-over-text/)
