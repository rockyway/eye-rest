# 001 — Product Backlog

Collected feature requests, user feedback, and improvement ideas. Items are prioritized and scheduled in separate planning documents.

**Legend:**  
`[U]` = User request · `[D]` = Developer/AI suggestion · `[F]` = User feedback  
Priority: `P1` Critical · `P2` High · `P3` Medium · `P4` Low  
Status: `New` · `Planned` · `In Progress` · `Done`

---

## UX / Main UI

| ID | Source | Priority | Status | Description |
|----|--------|----------|--------|-------------|
| BL-001 | [U] | P2 | Done | **Quick Break button** — Add a small "Quick Break" button in the main UI so users can trigger a break on demand without waiting for the scheduled break timer. Clicking it immediately shows the break popup for the same duration as a regular scheduled break. |

---

## Timer & Scheduling

_No items yet._

---

## Notifications & Popups

| ID | Source | Priority | Status | Description |
|----|--------|----------|--------|-------------|
| BL-002 | [U] | P3 | New | **Per-popup audio + custom audio sources** — Refactor audio playback so each popup type has its own audio configuration. Four configurable audio channels total: (1) Eye-rest popup **start**, (2) Eye-rest popup **end** (optional), (3) Break popup **start**, (4) Break popup **end** (optional). Ship a bundled default sound effect for every channel (generate the four default audio assets and include them with the app). For each channel, the user can: (a) keep the default sound effect, (b) pick a local audio file, or (c) provide a URL (e.g., a YouTube gym-music playlist) that opens in the default browser when the popup fires, in place of local audio. Add a single global on/off toggle that mutes/un-mutes all sound effects. |
| BL-003 | [U] | P3 | New | **Stable popup size across Break → Break-Complete transition** — When the Break popup transitions to the Break-Complete state, the popup window currently expands because the "Break Complete – Continue when ready" message is longer than the running-break message. Pin the popup size so it stays identical between the active Break popup and the Break-Complete popup (no auto-sizing to content). Likely a `SizeToContent="Manual"` + fixed `Width`/`Height` on the root window, plus letting the inner text wrap or truncate rather than push the container. |
| BL-004 | [U] | P3 | New | **Tabular/mono digits for ticking time displays** — All numeric time displays that tick every second (countdown minutes, countdown seconds, elapsed/extended time, etc. across all popups) currently use a proportional font, so the width jumps as digits change (e.g., `1` → `2`, `17` → `18`). Use tabular-figure digits — either via OpenType `FontFeatures="+tnum"` on the existing typeface or by switching the digit `TextBlock`s to a monospaced font — so each digit occupies the same horizontal advance and the time display doesn't visibly shift each tick. Apply consistently across Eye-rest popup, Break popup, Break-Complete popup, and any tray/tooltip time displays. |

---

## Settings & Configuration

_No items yet._

---

## Platform / Infrastructure

_No items yet._

---

## Notes

- Add new items with the next sequential `BL-NNN` ID.
- When an item is moved to planning, update its Status to `Planned` and link the plan doc.
- Do not delete `Done` items — archive them at the bottom for historical reference.
