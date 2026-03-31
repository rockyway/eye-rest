# 003 — EyeRest Marketing & Promotion Plan

> **Date:** 2026-03-30
> **App:** EyeRest — Free 20-20-20 break reminder for Windows & macOS
> **Model:** Freemium (free app, "Buy Me a Coffee" supporter key via LemonSqueezy)
> **Distribution:** GitHub Releases (Velopack auto-update), Microsoft Store (MSIX ready), eyerest.net

---

## App Summary for Marketing Purposes

**Elevator pitch:**
EyeRest automatically reminds knowledge workers to rest their eyes and take physical breaks using the 20-20-20 rule — pausing intelligently during meetings, idle time, and system sleep so reminders never interrupt at the wrong moment.

**Differentiators vs. competitors (Stretchly, Time Out, Awareness):**
- Smart meeting detection (Zoom, Teams, WebEx, Google Meet, Skype)
- Auto-pause on idle / system lock with session reset on long absence
- Local analytics dashboard with health score, compliance rate, CSV/JSON/HTML export
- 9 distinct system tray / menu bar icon states communicating app state at a glance
- Glass-morphism UI with light/dark themes — not a plain OS dialog
- No account, no cloud, no tracking — privacy-first
- Cross-platform: Windows 10/11 + macOS 12+ in one codebase (.NET 8 + Avalonia)
- Completely free; supporter key optional

---

## Current State Assessment

> **Updated 2026-03-30** — all distribution blockers are cleared.

| Channel | Status |
|---------|--------|
| eyerest.net website | **LIVE** |
| Microsoft Store (MSIX) | **APPROVED and live** |
| GitHub Releases | **Live** — Velopack signed installer |
| Windows EXE download | **Code-signed** |
| LemonSqueezy | Supporter checkout set up |
| Social / press / directories | Not started — this is the focus now |

**Remaining gaps (press kit only):**
1. Press kit page at eyerest.net/press (fact sheet + screenshots + demo GIF)
2. Google Search Console setup + sitemap submission
3. Google Alerts for `EyeRest app` to catch mentions

---

## Priority-Ordered Promotion Channels

### Tier 1 — Foundation (Week 1–2, one-time effort, evergreen ROI)

#### 1.1 App Submission Directories

Submit to all of the following. Total effort: ~4–6 hours. Returns ongoing organic discovery indefinitely.

| Directory | URL | Reach | Cost | Notes |
|-----------|-----|-------|------|-------|
| **Softpedia** | softpedia.com/user/submit.shtml | ~10M visits/mo | Free | Creates "100% Clean" badge for landing page |
| **AlternativeTo** | alternativeto.net | ~5M visits/mo | Free | Add under eye-care/break-reminder; appear in Stretchly/Time Out listings as an alternative |
| **MacUpdate** | macupdate.com | ~1M visits/mo | Free | macOS-focused; high-quality audience |
| **FileHorse** | filehorse.com | ~3M visits/mo | Free | Windows + Mac; editorial review (1–2 weeks) |
| **FossHub** | fosshub.com | ~2M visits/mo | Free | Signals clean software to technical users |
| **SourceForge** | sourceforge.net | ~25M visits/mo | Free | Broad reach; accepts freeware |
| **Ghacks** tip | ghacks.net (tips@ghacks.net) | ~2M visits/mo | Free | Regularly reviews freeware on request |

**For each submission, prepare:**
- App name, version, release date
- Short description (150 chars) + long description (500 chars)
- Download URL (GitHub Releases direct link)
- Developer email
- Screenshot set (5 images at 1280×800)

#### 1.2 Microsoft Store Submission

Packaging infrastructure is already complete in `EyeRest.Package/`. All MSIX visual assets are prepared.

**Steps:**
1. Register at partner.microsoft.com ($19 one-time developer account)
2. Upload MSIX bundle
3. Fill Store listing: description, keywords, age rating, privacy policy URL
4. Submit for certification (typically 1–3 business days)

**Why this matters:** The Microsoft Store is pre-installed on every Windows 10/11 machine. Discoverability is built in via Windows Search, Store search, and "New apps" sections.

#### 1.3 Google Search Console + First SEO Blog Post

**Setup:** Submit eyerest.net sitemap to Google Search Console (free). Enables monitoring of organic keyword rankings.

**First blog post target:** "How to Follow the 20-20-20 Rule Automatically (Free Desktop App)"
- ~1,200 words, targets low-competition informational keywords
- Captures users searching for the rule, not the app — converts them to downloaders
- Natural mention of EyeRest as the implementation

**Target keywords (low competition, high intent):**
- `eye rest reminder app windows`
- `20 20 20 rule app free desktop`
- `break reminder app macOS free`
- `reduce eye strain app free`
- `computer vision syndrome app`

**SoftwareApplication schema markup** on the landing page: Tells Google it's an app; can trigger rich snippets showing price (Free), OS, and star rating in search results.

---

### Tier 2 — Launch Week (Week 3–4, concentrated effort, high spike ROI)

#### 2.1 Hacker News — Show HN

**Post title:**
```
Show HN: EyeRest – free 20-20-20 break reminder for Windows and macOS (.NET 8/Avalonia)
```

**Opening comment (write in advance):**
- 150–200 words max
- Lead with the problem, not the solution
- Include one interesting technical detail (e.g., cross-platform NSStatusItem approach, or meeting detection via process scanning)
- Mention it is free, no account required
- Honest framing: "I built this for myself because existing apps were too heavy"

**Timing:** Tuesday or Wednesday, 7–9 AM EST. Post at news.ycombinator.com/submit.

**Realistic outcome:** 50–200 upvotes → 500–10,000 website visits in 24h. Front-page Show HN → press pickup opportunity.

#### 2.2 Product Hunt Launch

**Ideal launch day:** Wednesday of the same week as Show HN (avoid same day — spread traffic peaks).

**Assets to prepare:**
- Thumbnail: 240×240px, immediately communicates "eye + reminder" — no text
- Gallery: 5–8 screenshots in story order: tray icon states → popup overlay → settings panel → analytics dashboard
- Demo video: 60–90 second screen recording. Show the popup appearing, warning countdown, then dismissal.
- Tagline: `Protect your eyes — free automated 20-20-20 reminders for Windows & macOS`
- Founder comment: Write it before posting. Explain why you built it, what makes it different from Time Out and Stretchly, and offer a discount for Product Hunt users (even a "thank you" message works).

**Post at 12:01 AM PST.** Reply to every comment within 4 hours. Ask for feedback/comments — not upvotes.

**Pre-launch:** Share a "coming soon" link in communities 1 week before for notification subscribers.

#### 2.3 Reddit Launch Posts

Post in this order across launch week (not all on the same day):

| Subreddit | Members | Post angle |
|-----------|---------|------------|
| r/MacApps | 172K | Direct showcase: "I built a free eye break reminder for macOS — here's what it looks like" |
| r/productivity | 2M+ | "I automated the 20-20-20 rule with a free app I built — here's why manual reminders don't work" |
| r/eyestrain | 50K | Targeted: reply to complaints + dedicated post on the tool |
| r/RemoteWork | 400K | "Remote worker screen time hit 9h/day for me — this free app auto-enforces eye breaks" |
| r/windows | 2M | Windows-focused post with Start Menu icon / tray behavior screenshots |

**Reddit rule — do this before posting:** Spend 1 week genuinely commenting in r/eyestrain and r/MacApps before any app post. New accounts with only self-promotion posts get removed.

#### 2.4 LinkedIn Personal Story Post

Write a 300-word personal story (not a product announcement):
- "I got a headache every day at 4 PM for 6 months. Then I realized I hadn't looked away from my screen once."
- Include 3 screenshots of the app
- End with: "I built a free tool — EyeRest — for Windows and macOS. Link in comments."
- Tags: `#RemoteWork` `#DigitalWellness` `#Productivity` `#EyeHealth`

**Realistic reach:** 5K–50K impressions organically from a personal account with 500+ connections.

#### 2.5 Indie Hackers Launch Post

Create an account at indiehackers.com. Post "I just launched EyeRest" with:
- Origin story (why you built it)
- Tech stack (.NET 8 + Avalonia — rare and interesting to the IH audience)
- Early download numbers
- What you learned about cross-platform .NET development

Indie Hackers has a dedicated maker community that shares tools they use. High conversion per visitor.

---

### Tier 3 — Press and Influencer Outreach (Week 3–4, highest ceiling, lowest hit rate)

#### 3.1 Tech Blog Pitches

**Target publications and contacts:**

| Publication | Traffic | Contact |
|-------------|---------|---------|
| Lifehacker | ~10M/mo | pitches@lifehacker.com |
| HowToGeek | ~30M/mo | editorial contact / tips@howtogeek.com |
| MakeUseOf | ~20M/mo | contributor tip submission page |
| The Sweet Setup | ~500K/mo | thesweetsetup.com/sweet-setup-submission (macOS focus) |
| Ghacks | ~2M/mo | tips@ghacks.net |
| Neowin | ~5M/mo | Windows software tips section |

**Pitch email template:**
```
Subject: Free eye rest app for Windows/macOS — editorial tip

Hi [Name], I noticed you recently covered [specific article they wrote].

I built EyeRest, a free desktop app that automates the 20-20-20 eye break rule for
Windows 10/11 and macOS 12+. It includes meeting detection (Zoom, Teams), smart
pause on idle, and a local analytics dashboard — no account, no tracking.

Download: [direct link]
Press kit: eyerest.net/press

Happy to provide screenshots, a preview build, or answer questions.
— [Your name]
```

**Send 5 pitches/week. Track in a spreadsheet. Follow up once after 2 weeks.**

#### 3.2 Influencer Outreach

**Tier 1 — Productivity YouTubers (low reply rate but high ceiling):**
- Ali Abdaal (5.9M subs) — covers wellness + productivity tools
- Matt D'Avella (4M subs) — digital minimalism + wellness; strong thematic fit
- Keep Productive / Francesco D'Alessio (300K subs) — reviews productivity apps specifically; higher response rate

**Tier 2 — Smaller reviewers (better reply rate, still meaningful reach):**
- Simpletivity / Scott Friesen (200K subs) — 5-minute productivity tool reviews
- Jeffsu (200K subs) — Mac-focused productivity, app reviews
- Productivit Doctor — digital wellness + productivity

**Tier 3 — Eye health / optometry bloggers (niche but highly targeted):**
- Use FeedSpot's list (bloggers.feedspot.com/eye_health_blogs/) to identify 5 optometry/eye health bloggers with "tools" or "resources" sections
- These writers are underserved for app recommendations and may be enthusiastic about a free, clinically-aligned tool

**Outreach rule:** Reference a specific video/post of theirs in every email. Non-automated emails get responses; templates don't.

#### 3.3 Press Kit (host at eyerest.net/press)

A journalist should be able to write a story without contacting you first.

**Required assets:**
- [ ] Fact sheet (one page PDF): name, version, price, platforms, download links, key features, tech stack
- [ ] Founder bio (50 words)
- [ ] App logo PNG (1024×1024, transparent background)
- [ ] 5–8 annotated screenshots (1280×800 minimum)
- [ ] 15-second demo GIF or MP4
- [ ] 2 pull quotes from the developer
- [ ] "News hook": Digital eye strain affects 65% of adults (Vision Council). Remote work raised average screen time to 8+ hours/day. EyeRest addresses this for free, cross-platform, no account required.

**Free press release distribution:**
- PRLog (prlog.org) — free; indexed by Google News
- OpenPR (openpr.com) — free distribution

---

### Tier 4 — Ongoing / Long-Term (Monthly cadence)

#### 4.1 Twitter/X — Build in Public

Post 3×/week minimum for 6 weeks after launch:
- Screenshots of new features under development
- Interesting implementation stories (meeting detection edge cases, macOS NSStatusItem nuances)
- User-reported bugs and how they were fixed
- Metrics milestones: "1,000 downloads", "first supporter key purchased"

**Hashtags:** `#buildinpublic` `#indiehacker` `#dotnet` `#avalonia` `#productivity` `#eyehealth` `#MacApp` `#WindowsApp`

#### 4.2 Monthly SEO Blog Posts

Target one new keyword cluster per month:
- Month 1: "How to Follow the 20-20-20 Rule Automatically"
- Month 2: "Best Free Break Reminder Apps for Windows 11 (2026)"
- Month 3: "EyeRest vs Stretchly vs Time Out — Honest Comparison"
- Month 4: "How to Reduce Eye Strain Working From Home"
- Month 5: "Computer Vision Syndrome — What It Is and How to Prevent It"

Each post: 1,000–1,500 words, one natural mention of EyeRest, download CTA at the end.

#### 4.3 Community Monitoring

Monitor these for organic mention opportunities (not mass posting):
- r/eyestrain — reply to complaints with a solution
- r/ErgonomicsAdvice — eye strain + ergonomics overlap
- r/HealthyGamer — gamers have the same screen fatigue problem
- r/LongHaulersRecovery / CVS communities — computer vision syndrome sufferers actively seek tools

#### 4.4 Dev.to Technical Article

Write one in-depth technical post on Dev.to:
> "Building a cross-platform system tray app with .NET 8 and Avalonia — what I learned shipping to Windows and macOS"

This generates ongoing organic discovery from developers who are the exact target audience, and creates a lasting backlink to eyerest.net.

---

## Launch Sequence

```
Week 1  ← START HERE (site + store are live)
├── Submit to all 6 app directories (Softpedia, AlternativeTo, MacUpdate, FileHorse, FossHub, SourceForge)
├── Set up Google Search Console + submit sitemap
├── Build press kit at eyerest.net/press
└── Write first SEO blog post (draft)

Week 2
├── Publish first SEO blog post
├── Start Twitter/X "build in public" posting (3× per week)
├── Create Indie Hackers account + warm up (genuine posts)
└── Begin Reddit account warming (comment in r/eyestrain, r/MacApps — no self-promo yet)

Week 3
├── Send 10 personalized journalist pitches
├── Send 5 influencer emails (start with Keep Productive + Simpletivity — higher reply rate)
└── Finalize Show HN post draft and Product Hunt assets

Week 4 (Launch Week)
├── Show HN post — Tuesday 7–9 AM EST
├── Product Hunt launch — Wednesday 12:01 AM PST
├── LinkedIn personal story post — Wednesday
├── Reddit posts: r/MacApps, r/productivity, r/eyestrain — Thursday/Friday
└── Indie Hackers launch post
```

---

## Pre-Launch Checklist

> **Status as of 2026-03-30:** Distribution is live. Remaining items are marketing assets only.

**Completed:**
- [x] eyerest.net landing page live with download links
- [x] Microsoft Store listing approved
- [x] Windows installer code-signed (GitHub Releases)
- [x] Privacy policy page (required for MS Store — confirmed approved)

**Still needed before Tier 2 launch push:**
- [ ] Press kit at eyerest.net/press (fact sheet PDF + screenshots + 15s demo GIF)
- [ ] Google Search Console configured + sitemap submitted
- [ ] App screenshots at 1280×800, annotated (5 minimum)
- [ ] 15-second demo GIF/MP4 recorded
- [ ] Product Hunt thumbnail (240×240px) designed
- [ ] Twitter/X account ready
- [ ] Google Alerts set up for `EyeRest eyehealth app`

---

## Metrics to Track

| Metric | Tool | Frequency |
|--------|------|-----------|
| Download count | GitHub Releases API | Weekly |
| Website visitors | Google Analytics or Plausible | Weekly |
| Organic keyword rankings | Google Search Console | Monthly |
| Supporter key conversions | LemonSqueezy dashboard | Monthly |
| App directory referral traffic | GSC + Analytics | Monthly |
| Review/mention count | Google Alerts for "EyeRest" | Weekly |

Set up Google Alerts for `EyeRest eyehealth app` to catch press mentions automatically.

---

## Reference Resources

| Resource | URL |
|----------|-----|
| Softpedia submit | softpedia.com/user/submit.shtml |
| AlternativeTo | alternativeto.net |
| MacUpdate | macupdate.com |
| FossHub | fosshub.com |
| FileHorse | filehorse.com |
| Microsoft Store publish docs | learn.microsoft.com/en-us/windows/apps/publish/ |
| Product Hunt post | producthunt.com/posts/new |
| Hacker News submit | news.ycombinator.com/submit |
| Indie Hackers | indiehackers.com |
| Dev.to | dev.to |
| The Sweet Setup submission | thesweetsetup.com/sweet-setup-submission |
| FeedSpot eye health blogs | bloggers.feedspot.com/eye_health_blogs/ |
| Muck Rack journalist search | muckrack.com |
| PRLog press release | prlog.org |
| Google Search Console | search.google.com/search-console |
