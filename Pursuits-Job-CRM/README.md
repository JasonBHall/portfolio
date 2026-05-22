# Pursuits

**A job-search CRM that runs on your laptop.**

No accounts, no subscriptions, no vendor holding your history hostage. It's a local Flask app — start it, use it, own everything. Built out of frustration with spreadsheets and the recurring-fee crowd.

Runs on Windows and macOS. Double-click to launch.

<br>

![Active list — filter sidebar, age badges, status dropdowns, tag chips](screenshots/pursuits_active.jpg)

*Active list: filter sidebar, colour-coded age badges (Fresh → 2w Stale), inline status dropdowns, tag chips.*

<br>

## Why I built this

Every spreadsheet I tried to use for job tracking turned into a mess by week two. Every commercial CRM wanted a monthly fee and put my data on their servers. I wanted something that felt like a real tool, worked offline, and let me actually own my history — including the notes from that recruiter call six weeks ago that I'd otherwise forget entirely.

So I built it.

<br>

## What it does

### Pipeline tracking

Seven stages: Lead → Applied → Screen → Interview → Onsite → Offer → Closed. Change status inline from the list — one click, no page reload. Archive anything you're done with; it stays in the record and restores with one click.

### A morning view that tells you what needs attention

Open **Today** first thing. It breaks your pipeline into four buckets: snoozes that expired overnight, applications with no contact in 2+ weeks, interviews scheduled this week, and past interviews where you haven't sent a thank-you yet.

![Today view — upcoming interviews, woke-up snoozes, stale follow-ups, outstanding thank-yous](screenshots/pursuits_today.jpg)

*Today view: upcoming interviews with timestamps, woken snoozes, applications needing follow-up (2W STALE badges), and outstanding thank-you reminders.*

### Staleness badges on every row

Each row shows a badge based on how long since you last logged contact — green for fresh, shading through amber, orange, and red-orange past the month mark. You notice the red ones. That's the point.

### Notes timeline

The timeline on each opportunity is reverse-chronological and timestamped. Pick call / email / meeting / note, write what happened, submit. Logging a note resets the staleness clock. That's the mechanism — no automation, no magic.

### File attachments

Drop a PDF offer letter, a job description screenshot, or any document directly onto the opportunity. Images get thumbnails. Delete removes the bytes from disk.

### Search and filters that reach into notes

The sidebar filters by status, staleness, source, and your own tags — all combinable, all live-updating as you tick boxes. The search bar reaches into note text, not just company name. If you wrote "great chat with Marcus" on a call three weeks ago, searching `marcus` finds it.

### Interview scheduling with calendar export

Set a date and time on any opportunity. The detail page shows a banner with a one-click `.ics` download — open it in any calendar app to add the interview. No OAuth, no calendar API, no account needed.

![Detail page for Stratos Capital — compensation, hiring contact, snooze, tags, notes timeline](screenshots/pursuits_detail.jpg)

*Detail page: salary range, hiring contact with email and LinkedIn, snooze widget, tags, file drop zone, and a full call/email/meeting timeline.*

### Dashboard

Four charts: pipeline funnel, applications per week by source (last 12 weeks), age distribution, and a status donut. The funnel is the one worth checking regularly — it shows exactly where things are stacking up.

![Dashboard — headline tiles, pipeline funnel, status donut, activity by week, age distribution](screenshots/pursuits_dashboard.jpg)

*Dashboard: headline tiles (Total, Active, In Flight, Offers), pipeline funnel, status donut, 12-week activity chart stacked by source, and age-distribution histogram.*

### Snooze

Set a date, the opportunity hides until then. Useful when a recruiter says "reach back out in two weeks." Woken items show in Today.

### Tags

Free-form tags on each record: `dream`, `remote-only`, `backup`, whatever. Tags filter in the sidebar and show up in search.

### Archive

Close an opportunity and archive it when you're done. Still searchable, still there, one click to restore.

![Archive view — closed and archived opportunities with Restore buttons](screenshots/pursuits_archive.jpg)

*Archive view: every closed or archived opportunity, sorted by archive date, one click to restore.*

<br>

## Quick start

**Prerequisites:** Python 3.10+ and pip.

```bash
git clone https://github.com/yourusername/pursuits.git
cd pursuits
pip install -r requirements.txt
python app.py
```

Your browser opens to `http://127.0.0.1:5000` automatically.

Data lives in `./data/` — gitignored. Back it up like any other folder you care about.

<br>

## Paste a job description, get a filled form

If you have an Anthropic API key, the **Paste & extract** panel on the New Opportunity form fills in company, role, salary range, location, and work mode from a pasted job posting. One click.

To set it up: open **Settings** (bottom-left footer), paste your key, click Test, then Save. The key is stored in your data folder with restricted permissions and only ever sent to `api.anthropic.com`.

![New opportunity form with Paste & Extract panel and Extract with Claude button](screenshots/pursuits_new_with_claude.jpg)

*New opportunity form: paste any job description into the Paste & Extract panel and click "Extract with Claude" to auto-fill the fields. No API key? The rest of the form works fine without it.*

No key? Everything else works fine — the panel just stays disabled.

<br>

## Bookmarklet

Visit **Bookmarklet** in the footer and drag the link to your bookmarks bar. On any job posting page, select the description text and click it — a new tab opens with the URL and selected text prefilled. Hit Extract to fill the form.

<br>

## Keyboard shortcuts

Press `?` anywhere to open the shortcuts panel.

![Keyboard shortcuts modal](screenshots/pursuits_keyboard.jpg)

| Key | Action |
|---|---|
| `/` | Focus search |
| `n` | New opportunity |
| `t` | Today |
| `d` | Dashboard |
| `a` | Active list |
| `?` | This help |
| `Esc` | Close modal |

Shortcuts don't fire while you're typing in an input field.

<br>

## Standalone launcher (Windows and macOS)

The `packaging/` folder has scripts to build a `.exe` (Windows) or `.app` (macOS) — no Python required on the target machine.

```cmd
cd packaging
build_windows.bat
```

Output is `packaging/Pursuits-Windows.zip`. Unzip, double-click `Pursuits.exe`. Data goes to `Documents\Pursuits\` so it survives app updates.

See [`packaging/INSTALL.md`](packaging/INSTALL.md) for macOS and code signing notes.

<br>

## How it's built

Two runtime dependencies: `Flask` and `Pillow`. Everything else is stdlib. Here's what each piece does and why it's there:

- **Python + Flask** — server-side rendering with Jinja templates. No build step, no bundler, no `node_modules`. `python app.py` and it runs.
- **HTMX** — handles all the interactive bits (live search, inline status changes, note submissions, file uploads) through HTML attributes rather than a JavaScript framework. The whole frontend is about 50 lines of JS across the entire app.
- **Tailwind CSS (CDN)** — warm dark palette, zero local toolchain. For a single-user app where first-load time is irrelevant, CDN Tailwind is the right call.
- **Chart.js** — the four dashboard charts. Loaded only on the dashboard route, nowhere else.
- **Pillow** — generates 240px thumbnails for image attachments on upload. That's its only job.
- **JSON on disk** — all records live in `data/db.json`. Every write goes through a temp-file-rename (atomic on POSIX and Windows), and every save first snapshots the current file to `data/backups/` — last 20 kept automatically. No database to install, no migrations to run, no connection strings.
- **PyInstaller** — bundles the whole thing into a self-contained binary for distribution. See `packaging/`.

<br>

## Privacy

The app makes exactly two outbound requests, both requiring your explicit action: one to `api.anthropic.com` when you use Paste & extract, and one test call when you click "Test" on the Settings page. There's no telemetry, no background pings, no third-party services. Your `data/` folder never leaves your machine.

<br>

## Developer notes

See [`DEVELOPER_NOTES.md`](DEVELOPER_NOTES.md) for architecture decisions, the HTMX patterns used, the data model, and notes on each build phase.

---

*Built for personal use and shared as-is. Issues and PRs welcome.*
