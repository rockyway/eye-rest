# Microsoft Store Submission - Ready-to-Paste Content

## MSIX Package

- **File**: `dist/EyeRest.msix` (88 MB, unsigned - Store will sign)
- **Upload to**: Partner Center > Packages

---

## Properties

| Field | Value |
|---|---|
| Category | Health & fitness |
| Subcategory | — |
| Privacy policy URL | `https://eyerest.net/privacy` |
| Website | `https://eyerest.net` |
| Support contact | `https://eyerest.net/#support` |

---

## Age Rating

Answer "No" to all IARC questionnaire items — the app has:
- No violence, no fear content
- No sexual content
- No gambling, no drugs/alcohol references
- No user-generated content
- No social features or chat
- No in-app purchases (donation is external website link)
- No location data collection

Expected rating: **PEGI 3 / ESRB Everyone**

---

## Store Listing (en-us)

### Product name

```
Eye-Rest
```

### Short description (max 100 chars)

```
Automated eye rest and break reminders for healthier screen time.
```

### Description (max 10,000 chars)

```
Protect your vision. One reminder at a time.

Eye-Rest runs quietly in your system tray, guiding you through the 20-20-20 rule and regular breaks — so hours of screen work don't take a toll on your eyes.

SMART DUAL TIMERS
- 20-20-20 Rule: Every 20 minutes, a gentle popup reminds you to look at something 20 feet away for 20 seconds.
- Break Timer: Every 55 minutes, take a proper 5-minute break to stand, stretch, and move around.
- Pre-warnings appear 15-30 seconds before each reminder so you're never caught off-guard.

INTELLIGENT AUTOMATION
- Auto-pauses when you step away from your desk and resumes the moment you return.
- Detects active meetings in Teams, Zoom, WebEx, Google Meet, and Skype — no interruptions during calls.
- Hourly reminders if you manually pause, plus a safety auto-resume after 8 hours.

NON-BLOCKING BY DESIGN
- Click any overlay to dismiss instantly. Nothing locks you out of your work.
- Delay options (1 min / 5 min) when a full break isn't convenient.

ANALYTICS DASHBOARD
- Track your break compliance over 90 days with visual charts and weekly trends.
- Health score, completion rate, and streak tracking.
- Export data as CSV, JSON, or HTML report.

PRIVACY-FIRST
- All data stays local on your machine. Zero telemetry, zero cloud sync, zero accounts.
- No internet connection required for core functionality.

BEAUTIFUL INTERFACE
- Light and dark themes with glass-card aesthetic and animated mesh gradients.
- Multi-monitor support — overlays appear on all connected displays.
- Lives quietly in your system tray with smart status icons.

FREE & OPEN SOURCE
- Free to use with no limits. Pay what you want to support development.
- No subscriptions, no accounts, no ads.

System requirements: Windows 10 version 2004 or later, x64, 4 GB RAM, 100 MB disk space. Self-contained — no .NET runtime installation needed.
```

### What's new in this version

```
Initial release on the Microsoft Store.
```

### Search terms (max 7, each max 30 chars)

```
eye rest
break reminder
20-20-20 rule
eye strain
screen break
eye health
work break timer
```

### Copyright

```
© 2026 TTT Software
```

### Additional license terms

```
MIT License — https://github.com/rockyway/eye-rest
```

### Developed by

```
TTT Softwares
```

---

## Screenshots

Upload from `frontend/public/` — all are 1800x1400px (meets the 1366x768 minimum).

Recommended order and captions:

| # | File | Caption |
|---|---|---|
| 1 | `screenshot-light.png` | Main window — dual timer display with live countdown |
| 2 | `screenshot-dark.png` | Dark theme — glass-card aesthetic |
| 3 | `screenshot-light-2.png` | Break reminder with delay and skip options |
| 4 | `screenshot-dark-2.png` | Analytics dashboard — 90-day compliance tracking |
| 5 | `screenshot-light-3.png` | Settings and system tray integration |
| 6 | `screenshot-dark-3.png` | Multi-monitor overlay support |

> Upload at least 4 screenshots. You can upload up to 10.

---

## Store Icon

Already configured in the MSIX manifest. The Store uses the tile assets from the package:
- `Square150x150Logo` for the listing tile
- `StoreLogo` for the Store search results

---

## System Requirements

Partner Center lets you declare these:

| Requirement | Minimum | Recommended |
|---|---|---|
| OS | Windows 10 version 2004 | Windows 11 |
| Architecture | x64 | x64 |
| Memory | 4 GB | 8 GB |
| Disk space | 100 MB | 150 MB |

---

## Submission Checklist

1. [ ] **Packages** — Upload `dist/EyeRest.msix`
2. [ ] **Properties** — Set category, privacy URL, website, support contact
3. [ ] **Age rating** — Complete IARC questionnaire (all "No")
4. [ ] **Store listing** — Paste name, descriptions, search terms, copyright
5. [ ] **Screenshots** — Upload 4-6 from `frontend/public/`
6. [ ] **Pricing** — Set to "Free"
7. [ ] **Publish options** — "As soon as possible" or schedule a date
8. [ ] **Submit** — Review and submit for certification (typically 1-3 business days)
