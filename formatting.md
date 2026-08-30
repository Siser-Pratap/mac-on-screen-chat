# Formatting, standing rules, and stopping a reply

> Four things, planned as six phases:
>
> 1. Every reply comes back properly formatted — short paragraphs, one blank
>    line between them, no walls of text.
> 2. `/command {…}` is a **standing rule**: it is saved and applied to every
>    future reply, not just the next one.
> 3. No reply ever contains `**` — enforced twice: asked for in the prompt, and
>    stripped from the stream so it is guaranteed.
> 4. A stop button that cancels a reply mid-generation.

---

## Phase 1 — `ResponseStyle` (the foundation)

New file `Sources/MacOnScreenChat/ResponseStyle.swift`, three pieces:

- **`ResponseStyle.contract`** — a formatting contract prepended to *every*
  system prompt, whatever the skill: plain text, short paragraphs, exactly one
  blank line between them, `- ` for list items, no `**`, no headings, no
  preamble. It ends by yielding to the skill: if a skill specifies an exact
  output shape (Dating reply's three labeled lines), that shape wins.
- **`AsteriskStripper`** — a streaming-safe filter that deletes runs of two or
  more `*`. Chunks arrive split at arbitrary points, so a trailing run is held
  back until we know how long it is. A lone `*` survives (`2*3`, glob patterns).
- **`ResponseStyle.normalized(_:)` / `paragraphs(of:)`** — trims trailing
  spaces per line, collapses runs of blank lines to one, and splits text into
  paragraph blocks for rendering.

## Phase 2 — Standing rules

- `Rule` record (`id`, `text`, `sortOrder`) + migration `v5.createRule`, with
  CRUD on `AppDatabase`. Rules outlive relaunches and "New chat".
- `RuleStore` — the observable UI-facing store, and `promptBlock`, which renders
  the rules into the system prompt as *my* highest-priority standing rules.
- `ChatRole.note` — a third message kind for "Rule saved" acknowledgements.
  Notes render as a centered pill and are **never sent to the model**.

## Phase 3 — Wiring `ChatViewModel`

`send` now assembles the system prompt in precedence order:

```
ResponseStyle.contract  →  skill prompt  →  standing rules  →  WW:{…} directive
        (general)          (output shape)     (mine, persistent)   (this reply only)
```

- `/command {…}` is parsed out of the input, saved as a rule, and never sent as
  message text. If the input was *only* commands, nothing is sent to the model —
  it just acknowledges with a note.
- Every streamed chunk goes through `AsteriskStripper` before it reaches the
  transcript, so what is displayed, persisted, and copied is already clean.

## Phase 4 — Stop button

- `ChatViewModel.stop()` cancels the stream task. The `AsyncStream`'s
  `onTermination` already cancels the underlying `URLSession` task, so the
  network request dies with it.
- While streaming, the send button becomes a stop button (`⌘.`). Whatever text
  arrived before the stop is kept and persisted; if nothing arrived, the empty
  bubble is removed.
- `Esc` is left alone — it hides the panel.

## Phase 5 — Rendering

`MessageBubble` renders paragraph blocks in a `VStack` with real spacing between
them, plus modest `lineSpacing` inside a block, so the division between
paragraphs is visible rather than just present in the text.

## Phase 6 — Verify

`--selftest` gains headless coverage of the stripper, `/command` extraction, and
the rules round-trip. `--markers "<text>"` dry-runs a real input and prints what
would be saved, what steers this reply, and what reaches the model. Then
`./build-app.sh` and relaunch.

```sh
.build/debug/MacOnScreenChat --selftest
.build/debug/MacOnScreenChat --markers 'summarize this /command {no emojis} WW:{one line}'
```

---

## Using it

```
/command {always answer in under 5 lines}     → saved, applies from now on
WW:{one savage line, no labels}               → this reply only
```

Manage saved rules from the skill menu → **Rules…** (edit, delete, clear all).
