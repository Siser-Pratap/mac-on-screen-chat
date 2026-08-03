# Visibility — Hide the panel from screen sharing

> Goal: the floating chat panel is **visible to me on my physical display**, but
> **invisible to anyone I screen-share with** — Google Meet, Zoom, Teams,
> QuickTime, OBS, macOS `screencapture`, and any other screen-recording tool.

---

## 1. The mechanism: `NSWindow.sharingType`

macOS has a built-in, first-class way to exclude a window from screen capture:
the window's [`sharingType`](https://developer.apple.com/documentation/appkit/nswindow/sharingtype)
property (`NSWindow.SharingType`).

| Value | Meaning |
|-------|---------|
| `.readOnly` | **Default.** Other processes may read (capture) the window's contents. |
| `.readWrite` | Others may read and write the contents. |
| `.none` | **The window is excluded from capture entirely.** |

Setting `sharingType = .none` tells the **window server** never to hand this
window's pixels to another process. It still composites and draws normally on the
local display, so you see it — but any screen-capture pipeline gets a hole
(or the content behind it) where the panel is.

This is exactly what password managers (1Password), banking apps, and DRM video
use. It is the supported, App-Store-safe API — no private symbols, no
entitlements needed.

### Why this covers "all" screen-share apps

Zoom, Google Meet (browser `getDisplayMedia`), Teams, QuickTime, OBS, and the
`screencapture` CLI all ultimately obtain screen pixels through the macOS window
server / **ScreenCaptureKit** / CoreGraphics capture path. `sharingType = .none`
is honored at that layer, so it applies uniformly regardless of which app is
doing the sharing. There is no per-app work to do.

---

## 2. The change

**One property, set once, in [FloatingPanel.swift](Sources/MacOnScreenChat/FloatingPanel.swift).**

In `FloatingPanel.init`, alongside the other window configuration (near the
`level`/`collectionBehavior` lines), add:

```swift
// Exclude this window from ALL screen capture / screen sharing.
// The window server never hands our pixels to another process, so the panel
// is invisible in Zoom / Meet / Teams / QuickTime / OBS / screencapture,
// while staying fully visible on the local physical display.
sharingType = .none
```

That's the entire functional change. `sharingType` is inherited from `NSWindow`,
so it's available on our `NSPanel` subclass directly.

### Notes on placement
- Set it in `init` so it applies the moment the panel exists and can never be
  captured — even for the first frame.
- It persists across show/hide, Spaces switches, and fullscreen, so nothing in
  [PanelController.swift](Sources/MacOnScreenChat/PanelController.swift) needs to
  change.

---

## 3. Optional: make it a toggle (recommended follow-up, not required)

You may sometimes *want* to share the panel (e.g. demoing the app). If so, expose
a switch instead of hard-coding `.none`:

- Store a `Bool` in `UserDefaults` (e.g. `hideFromScreenShare`, default `true`).
- Add a checkbox item in [MenuBarController.swift](Sources/MacOnScreenChat/MenuBarController.swift):
  **"Hide from screen sharing ✓"**.
- On toggle, set `panel.sharingType = hidden ? .none : .readOnly`.

Changing `sharingType` at runtime takes effect immediately — no window recreate
needed. Ship the safe default (`.none`) first; add the toggle only if you find
you need to share it.

---

## 4. Limitations — read before trusting it

`sharingType = .none` defeats **software** capture. It does **not** defeat:

1. **A physical camera / phone pointed at the screen.** Nothing in software can
   stop that.
2. **Hardware capture / a second physical monitor mirror** at the display-link
   level (rare, e.g. an HDMI capture card mirroring the whole signal). The window
   server exclusion happens before that in most setups, but a full hardware
   mirror of the display output can still show it.
3. **Accessibility / screen-reader tools** that read the view hierarchy rather
   than pixels (not a screen-share concern).

For the stated use case — normal Zoom/Meet/Teams screen sharing — items 1–3 are
not in scope, so `.none` fully solves it.

---

## 5. How to verify

Do a real end-to-end check (don't trust it blind):

1. Build and launch the app; summon the panel with the hotkey.
2. **Quick local test — `screencapture`:**
   ```sh
   screencapture -x ~/Desktop/capture-test.png
   ```
   Open the PNG. The panel should be **absent** (you'll see whatever is behind
   it), while it's still plainly visible on your live screen.
3. **QuickTime:** New Screen Recording → record a few seconds with the panel
   open → play back. Panel should not appear.
4. **The real thing:** Start a Google Meet or Zoom call (a second device or a
   throwaway account works), share your **entire screen**, and confirm the panel
   is missing from the shared view while visible to you.
5. Test both "share entire screen" and, where offered, "share a window" — the
   panel should never be listed or shown.

---

## 6. Summary / checklist

- [ ] Add `sharingType = .none` in `FloatingPanel.init`
      ([FloatingPanel.swift](Sources/MacOnScreenChat/FloatingPanel.swift)).
- [ ] Build (`./build-app.sh` or `swift build`) and launch.
- [ ] Verify with `screencapture -x`, QuickTime, and a live Meet/Zoom share.
- [ ] *(Optional)* Add a menu-bar toggle backed by `UserDefaults` if you ever
      want to share it deliberately.

**Bottom line:** a single line — `sharingType = .none` — makes the panel yours
alone on screen shares, with no downside to how you use it locally.
