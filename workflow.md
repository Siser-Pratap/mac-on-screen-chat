# Workflow — Mac On-Screen Chat Assistant

> A floating macOS assistant that lives above every other app. Pop it open with a
> hotkey, paste in a message someone sent you, and get a ready-to-send reply
> (e.g. a warm LinkedIn-style connection message) from a library of prompt
> "skills."

---

## 1. Product vision

**What it is:** A small, always-on-top chat window — think Spotlight / Raycast,
but a conversational AI panel. It never gets buried behind other windows, opens
instantly via a global hotkey, and ships with a set of pre-built "skills"
(prompt templates) tuned for outreach, replies, and message analysis.

**Core use cases (the "special prompts"):**
1. **Connection / outreach writer** — paste someone's message or profile blurb;
   get a personalized connection request or DM that references what they said.
2. **Reply analyzer** — paste an incoming message; the app extracts intent,
   tone, and asks, then drafts 2–3 reply options (warm / concise / formal).
3. **Tone rewriter** — paste your own draft; get it rewritten in a chosen tone.
4. **Quick chat** — a plain conversational fallback for anything else.

**Design principles:**
- *Always reachable* — floats over fullscreen apps, survives Spaces switches.
- *Zero-friction* — global hotkey to summon/dismiss; auto-focus the input.
- *Skill-driven* — adding a new "special prompt" = adding one config entry.
- *Private by default* — API key in Keychain; conversations stored locally.

---

## 2. Tech stack — DECIDED: Native SwiftUI + AppKit `NSPanel`

**Chosen stack: Native SwiftUI + AppKit `NSPanel`.**

Why: truest "always on top of everything" behavior (including over fullscreen
apps), tiny footprint (~5MB), and global hotkey + Keychain are first-class on
macOS. UI in SwiftUI, the floating window via an AppKit `NSPanel` bridged in.

Considered and set aside: Tauri (React+Rust) and Electron — both viable for a
web-first workflow, but neither matches native for the overlay feel, and Electron
is heavy (~150MB). Revisit only if Swift becomes a blocker.

**Tooling (per installed toolchain):** This machine has **Swift 6.3 + Command
Line Tools only — no full Xcode**. So the project is a **Swift Package Manager
executable**, built/run with `swift build` / `swift run`. The app is constructed
programmatically (AppKit `NSApplication` + `NSPanel`, SwiftUI content via
`NSHostingView`). No `.xcodeproj` is required for development; we can generate
one later (or install Xcode) when it's time to code-sign for distribution.

Dependency strategy: keep Phases 0–1 **dependency-free** (Carbon hotkey, no SPM
deps) so it builds offline. Add [`GRDB`](https://github.com/groue/GRDB.swift)
for SQLite in Phase 3/5, and a markdown renderer when the transcript needs it.

---

## 3. Architecture overview

```
┌────────────────────────────────────────────┐
│  Floating Panel (always-on-top, hotkey)     │
│  ┌──────────────────────────────────────┐   │
│  │  Skill picker  ▸ [Connection] [Reply]│   │
│  │  Chat transcript                      │   │
│  │  Input box  ───────────────  [Send]   │   │
│  └──────────────────────────────────────┘   │
└───────────────┬────────────────────────────┘
                │
        ┌───────▼────────┐
        │  App core      │  prompt assembly, history, streaming
        └───────┬────────┘
        ┌───────▼────────┐     ┌──────────────┐
        │  LLM client    │────▶│  Claude API  │
        └───────┬────────┘     └──────────────┘
        ┌───────▼────────┐
        │ Local storage  │  Keychain (API key) + SQLite via GRDB (history + skills)
        └────────────────┘
```

**Components:**
- **Floating panel** — overlay window + global hotkey + input/transcript UI.
- **Skill registry** — each skill = `{ id, name, systemPrompt, inputHint }`.
- **LLM client** — calls Claude API, streams tokens back to the UI.
- **Storage** — Keychain for the API key; local **SQLite (GRDB)** for history + skills.

**LLM backend:** DEFERRED — provider decision postponed to Phase 3.
- Build the chat shell against a small `LLMClient` protocol (one method:
  `stream(messages, systemPrompt) -> AsyncStream<String>`), so the UI and skills
  are written without committing to a provider yet.
- Start with a **mock/echo client** so Phases 1–2 run with no network/key.
- Leading candidate when we wire it up: Claude API (`claude-sonnet-4-6` default,
  `claude-opus-4-8` toggle, streamed). *(When that day comes, consult the
  `claude-api` skill for the exact request shape, headers, and streaming format.)*

---

## 4. Build phases & steps

### Phase 0 — Setup
- [ ] Confirm tech stack (Section 2).
- [ ] Scaffold a **SwiftPM executable** macOS app (no SPM deps yet).
- [ ] Add a Swift `.gitignore`; verify it builds (`swift build`) and launches
      (`swift run`).
- [ ] (LLM key not needed yet — Phases 1–2 use the mock client.)

### Phase 1 — Floating window (the hard/unique part)
- [ ] Create a borderless, always-on-top panel.
  - Swift: `NSPanel` with `level = .floating` (or `.statusBar`),
    `collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]`,
    `styleMask = [.nonactivatingPanel, .titled, .fullSizeContentView]`.
  - Tauri/Electron: `alwaysOnTop: true`, `setVisibleOnAllWorkspaces`,
    frameless, transparent if desired.
- [ ] Register the **global hotkey ⌘⇧Space** to show/hide & focus input.
  - Use Carbon `RegisterEventHotKey` — system-wide, no Accessibility permission.
  - (Rebinding UI deferred; can swap in `KeyboardShortcuts` later.)
- [ ] Make it draggable, remember last position/size, and dismiss on `Esc`.
- [ ] **Checkpoint:** window floats over a fullscreen app and toggles via hotkey.

### Phase 2 — Chat UI
- [ ] Transcript view (user vs assistant bubbles, autoscroll, markdown render).
- [ ] Multiline input with Send (⌘↵) and a busy/streaming indicator.
- [ ] Skill picker (segmented control or dropdown) above the input.
- [ ] Empty state showing available skills + example usage.

### Phase 3 — LLM integration
- [ ] Define the `LLMClient` protocol + a mock/echo client (used in Phases 1–2).
- [ ] **Decide the provider** (Claude API is the leading candidate) and implement
      the real client behind the same protocol.
- [ ] Implement streaming responses into the transcript.
- [ ] Store the API key in **Keychain** (never plaintext); add a settings field.
- [ ] Handle errors gracefully (no key, rate limit, network) with inline notices.
- [ ] **Checkpoint:** plain chat works end-to-end.

### Phase 4 — Skills (the "special prompts")
- [ ] Define the skill schema and a registry; seed the 4 starter skills into
      SQLite on first launch (editable thereafter).
- [ ] Ship the starter skills:
  - **Connection writer** — sys prompt: analyze their message/profile, find a
    genuine hook, write a short, non-salesy connection note; output 1 message.
  - **Reply analyzer** — sys prompt: summarize intent/tone/asks, then draft
    3 labeled reply options (warm / concise / formal).
  - **Tone rewriter** — sys prompt: rewrite the draft in the selected tone,
    preserve meaning, return only the rewrite.
  - **Plain chat** — no template; empty system prompt, normal conversation.
    This is the default selection when the panel opens.
- [ ] Selecting a skill swaps the system prompt + updates the input placeholder.
- [ ] (Optional) Per-skill parameters (target tone, length, language).
- [ ] **Checkpoint:** paste a message → pick "Connection writer" → get a draft.

### Phase 5 — Quality-of-life
- [ ] **Copy reply** button + auto-copy last assistant message.
- [ ] Persist conversation history in SQLite; "New chat" to reset; (optional)
      list/search past conversations.
- [ ] Menu-bar icon (status item) to show/hide and quit.
- [ ] Launch at login (optional).
- [ ] Model toggle (Sonnet ↔ Opus) and a token/cost awareness note.

### Phase 6 — Polish & ship
- [ ] App icon, name, and window styling.
- [ ] Light/dark mode.
- [ ] Write README with setup + hotkey docs.
- [ ] **(Deferred — "signed later")** Code-sign & notarize with an Apple
      Developer account, then package a `.dmg` for sharing. Skip until the app
      is proven and you want to hand it to someone else.

---

## 5. Skill schema (reference)

```jsonc
{
  "id": "connection-writer",
  "name": "Connection writer",
  "inputHint": "Paste their message or profile blurb…",
  "model": "claude-sonnet-4-6",
  "systemPrompt": "You write short, genuine LinkedIn-style connection messages. Analyze the pasted text for a specific hook (shared interest, recent post, role). Output ONE message under 300 characters, warm and non-salesy, no emojis unless the source uses them.",
  "params": { "tone": "warm", "maxChars": 300 }
}
```

Adding a new "special prompt" later = append one object like this. No code change.

---

## 6. Decisions — all settled ✅

- ✅ **Tech stack** — Native SwiftUI + AppKit `NSPanel`.
- ✅ **LLM** — deferred; build behind an `LLMClient` protocol, mock first,
  pick provider (likely Claude API) in Phase 3.
- ✅ **Global hotkey** — **⌘⇧Space**, via Carbon `RegisterEventHotKey`
  (system-wide, needs **no** Accessibility permission). Rebinding can come later.
- ✅ **Distribution** — **local now, signed later**. Run unsigned from Xcode
  while building; add code-sign + notarize + `.dmg` as a final polish step.
- ✅ **Starter skills** — **all four**: Connection writer, Reply analyzer,
  Tone rewriter, and Plain chat (the no-template fallback).
- ✅ **Storage** — **local SQLite** for conversation history; skills as editable
  config seeded into the DB on first launch.

---

## 7. Suggested milestones

- **M1:** Floating window + hotkey (Phase 1) — the riskiest piece, done first.
- **M2:** Working chat with Claude (Phases 2–3).
- **M3:** The 3 starter skills (Phase 4) — the actual product value.
- **M4:** QoL + packaging (Phases 5–6).
