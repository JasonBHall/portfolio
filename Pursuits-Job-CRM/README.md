# Pursuits

**A personal job-search CRM that lives on your machine.**

No subscriptions, no accounts, no data sent anywhere. Just a clean local web app for tracking applications through the pipeline — with notes from your phone calls, files you've dragged in, and enough charts to know whether your search is actually moving.

Built with Python + Flask. Runs with a double-click on Windows and macOS.

<br>

![Active list view](screenshots/active-list.png)

<br>

## Why I built this

Every spreadsheet I tried to use for job tracking turned into a mess by week two. Every commercial CRM wanted a monthly fee and put my data on their servers. I wanted something that felt like a real tool, worked offline, and let me actually own my history — including the notes from that recruiter call six weeks ago that I'd otherwise forget entirely.

This is that tool.

<br>

## What it does

### Track every opportunity through the pipeline

Seven stages: Lead → Applied → Screen → Interview → Onsite → Offer → Closed. Change status inline from the list — one click, no page reload. Archive anything you're done with; it stays in the record and can be restored.

### Know what needs attention without thinking about it

The **Today view** opens every morning and surfaces four buckets: snoozes that have woken up, applications gone stale (2+ weeks without contact), interviews scheduled this week, and past interviews where you haven't sent a thank-you yet.

![Today view](screenshots/today.png)

### Age badges on every row

Each opportunity shows a colored staleness badge computed from your last logged contact — green for fresh, shading through amber and orange to red at 3+ months. You can't ignore what's gone cold when it's sitting there in red.

### Log every call, email, and meeting

The notes timeline on each opportunity is reverse-chronological and timestamped. Type, pick call/email/meeting/note, submit. Adding a note resets the staleness clock — that's the whole mechanism.

### Drag and drop files

Drop a PDF job description, a screenshot, or an offer letter directly onto the opportunity. Images get thumbnails. Everything else gets a file icon. Delete removes it from disk.

### Filter and search that actually works

The filter sidebar lets you narrow by status, age bucket, source, and your own tags — all combinable, all live-updating. Search reaches into note bodies and tags, not just the company name.

![Detail page showing notes timeline, interview banner, and contacts](screenshots/detail.png)

### Schedule interviews and export to your calendar

Set an interview date/time on any opportunity. The detail page shows a banner with a one-click `.ics` download — double-click it to add the interview to your calendar app. No calendar API, no OAuth dance.

### Dashboard charts

Four charts: pipeline funnel, applications per week by source (last 12 weeks), age distribution, and a status donut. The funnel is the one you'll check most — it tells you if you're getting stuck somewhere.

![Dashboard showing funnel and charts](screenshots/dashboard.png)

### Snooze anything

Set a snooze date on any opportunity. It drops to the bottom of the active list and hides until that day — useful for "check back in two weeks" situations. Woken items show up in Today.

### Tags

Free-form tags on each opportunity. Tag something `dream` or `remote-only` or `backup` and filter by them later. Tags feed into the search too.

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

Your data lives in `./data/`. It's gitignored. Back it up like any other important folder.

<br>

## Paste a job description, get a filled form

If you have an Anthropic API key, the **Paste & extract** panel on the New Opportunity form will fill in company, role, salary range, location, and work mode from a pasted job description. One click.

To set it up: open **Settings** (bottom-left footer), paste your key, click Test, then Save. The key is stored in your data folder with restricted permissions and only ever sent to `api.anthropic.com`.

![Settings page showing API key configuration](screenshots/settings.png)

No key? Everything else still works. The extract panel just stays disabled.

<br>

## Save job postings with a bookmarklet

Visit **Bookmarklet** in the footer. Drag the link to your bookmarks bar. On any job posting page, optionally select the description text, then click the bookmark — a new tab opens with the URL and selected text prefilled. Hit Extract to fill the form from the description.

<br>

## Keyboard shortcuts

| Key | Action |
|---|---|
| `/` | Focus search |
| `n` | New opportunity |
| `t` | Today |
| `d` | Dashboard |
| `a` | Active list |
| `?` | Help |

<br>

## Double-click launcher (Windows and macOS)

The `packaging/` folder contains scripts to build a standalone `.exe` (Windows) or `.app` (macOS) — no Python installation required for the end user.

```cmd
cd packaging
build_windows.bat
```

Output is `packaging/Pursuits-Windows.zip`. Unzip, double-click `Pursuits.exe`. Data goes to `Documents\Pursuits\` so it's never inside the app bundle.

See [`packaging/INSTALL.md`](packaging/INSTALL.md) for full instructions including macOS and code signing notes.

<br>

## Tech

- **Python / Flask** — server-side rendering, no build step
- **HTMX** — live search, inline status changes, note timeline, drag-drop uploads — without a JS framework
- **Tailwind CSS** — via CDN, warm-dark palette
- **Chart.js** — dashboard charts, loaded only on the dashboard page
- **Pillow** — image thumbnails on upload
- **JSON on disk** — atomic writes with temp-file-rename, 20-snapshot auto-backup before every save. No database.
- **PyInstaller** — standalone bundling for distribution

Two runtime dependencies: `Flask` and `Pillow`. Everything else is stdlib.

<br>

## Data and privacy

Everything runs locally. The only outbound requests this app makes are:

- To `api.anthropic.com` when you use the Paste & extract feature (opt-in, requires your own API key)
- The 1-token "Test" call on the Settings page when you click Test

No telemetry. No analytics. No accounts. Your `data/` folder is yours.

<br>

## Developer notes

See [`DEVELOPER_NOTES.md`](DEVELOPER_NOTES.md) for architecture decisions, the HTMX patterns used, the data model in detail, and notes on each build phase.

---

*Pursuits is a personal tool shared as-is. Issues and PRs welcome.*
