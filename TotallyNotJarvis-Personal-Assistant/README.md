# IRONLOG

**A self-hosted personal dashboard for lifting, tasks, and voice capture.**

No accounts, no subscriptions, no monthly fees. Runs on a home PC and is accessible from your phone and laptop over a private network. Built because the math behind a lifting program isn't worth $15/month, and a task manager that creates iOS Reminders doesn't need to live on someone else's server.

<br>

![Active workout — bench press with warmup sets, working sets, AMRAP counter, FSL supplemental](screenshots/jarvis_workout.jpg)

*Active workout: lift name, cycle/week position, plate math, warmup and working sets, live AMRAP counter, FSL supplemental tracker, and a note field.*

<br>

## Why I built this

Paid fitness apps charge a monthly fee for deterministic math. A 5/3/1 program takes your training max, applies known percentages, and tells you what to lift. There is no reason to pay for that indefinitely.

Task managers with reminders are the same story. The hard part — actually buzzing your phone at the right time — is already solved by iOS Reminders. What was missing was a unified dashboard that ties fitness, tasks, and voice capture into one place without a recurring fee.

So I built it.

<br>

## What it does

### 5/3/1 program tracking

Four lifts: squat, bench, deadlift, OHP. The program advances automatically — set your training maxes, pick your volume mode, and the app calculates every warmup, working set, and supplemental set for each session. AMRAP performance on the last working set is the feedback signal. Log more reps than prescribed and the program flags it; fall short and it flags that too. Training maxes increment automatically at the end of each cycle.

Three volume modes:

| Mode | Supplemental | When to use |
|---|---|---|
| Light | FSL 3×5 | Starting out, returning after a break, prioritising recovery |
| Medium | FSL 5×5 | Solid base, want more volume |
| Heavy | BBB 5×10 | High-recovery capacity, chasing hypertrophy |

Plate math is automatic — the app shows which plates to load per side so you don't have to calculate it mid-session.

![Settings — program position, units, volume mode, training maxes](screenshots/jarvis_workout_settings.jpg)

*Settings: current cycle/week/day, lbs or kg, volume mode selector, and training maxes. TMs auto-increment each cycle; edit here only to correct an error.*

### Tasks that span everything

Tasks come from two sources: typed directly into the Quick Add bar, or created automatically by the voice pipeline when you say something like "remind me to call the physio on Monday." Either way they land in the same list.

Four views — Today, Upcoming, All, Done — sorted by due date. Overdue items are highlighted. Tasks created from a voice memo carry a linked badge so you can trace where they came from. The REMIND button generates an iOS Reminder URL for the task's time, so the actual alert comes from your phone rather than a web push.

![Tasks — Today view with overdue item, REMIND button, voice-linked badges](screenshots/jarvis_tasks_today.jpg)

*Today: three tasks due today, one overdue (highlighted), voice-linked items showing their source, and a REMIND button for iOS Reminder export.*

![Tasks — Upcoming view mixing fitness and life tasks](screenshots/jarvis_tasks_upcoming.jpg)

*Upcoming: fitness and life tasks together — groceries, a physio appointment, a belt order, a phone call — sorted by due date.*

![Tasks — All view showing full open list](screenshots/jarvis_tasks_all.jpg)

*All: every open task in due-date order. The same list, no filter.*

![Tasks — Done view with completed items](screenshots/jarvis_tasks_done.jpg)

*Done: completed tasks with their notes intact. Voice-linked tasks preserve the JARVIS answer that created them.*

### Voice capture

Say it, JARVIS handles it. The pipeline is: tap to record (or type) → Whisper transcribes → Claude classifies intent → routed to the right place → Piper speaks the response back.

Four intent types:

- **TASK** — "remind me to call the physio Monday afternoon" → task created with due date parsed from natural language
- **FITNESS** — "nine reps on the deadlift AMRAP, back felt solid" → attached to the current workout as a note
- **Q&A** — "do I need a loading phase for creatine?" → answered by JARVIS, optionally saved as a task
- **NOTE** — "rest day, felt sluggish all afternoon" → saved to the memo log

Three status indicators show whether Whisper, Claude, and Piper are all configured and reachable. A text input is available as a bypass when you want to use JARVIS without speaking.

![Voice — status pills, record button, text input, recent captures list](screenshots/jarvis_voice_history.JPG)

*Voice: WHISPER · CLAUDE · PIPER status, TAP TO RECORD, text input fallback, and the full capture history with intent badges and action labels.*

### Natural language quick add

The task Quick Add bar understands plain English: *"remind me to call Dave Thursday 3:30pm"* creates a task with a Thursday 3:30 PM due date. Powered by chrono-node — no cloud, no NLP service.

### iOS Reminders integration

IRONLOG doesn't try to send push notifications — iOS Reminders already does that perfectly. Tasks with a `remind_at` time get a REMIND button. On iPhone it opens the Reminders app directly; on desktop it copies a URL to open on your phone.

### Bodyweight tracking

Log your weight each day. The Body view plots your trend over time.

<br>

## Quick start

**Prerequisites:** Node.js 18+, Python 3.10+, pip.

```bash
git clone https://github.com/yourusername/ironlog.git
cd ironlog
npm install
npm run dev
```

The web app opens at `http://localhost:5173`. The API runs at `http://localhost:3001`.

Data lives in `apps/server/data/ironlog.json` — gitignored. Back it up like any other file you care about.

<br>

## Voice pipeline setup

Voice input requires three external tools. Each is optional — the app works fine without them, and the text input is always available.

### Whisper (transcription)

IRONLOG uses [faster-whisper](https://github.com/SYSTRAN/faster-whisper) — a Python implementation that runs on CPU without AVX2.

```bash
pip install faster-whisper
```

A wrapper script is included at `scripts/transcribe.py`. Point the env var at the batch file:

```
WHISPER_BIN=C:\path\to\ironlog\scripts\transcribe.bat
WHISPER_MODEL=base.en
```

### Piper (text-to-speech)

Download [Piper](https://github.com/rhasspy/piper/releases) and a voice model. The repo was built with a British male voice for the JARVIS character — any Piper model works.

```
PIPER_BIN=C:\piper\piper.exe
PIPER_VOICE=C:\piper\voices\your-voice.onnx
PIPER_LENGTH_SCALE=0.95
```

`PIPER_LENGTH_SCALE` controls speech speed — values below 1.0 are faster. Omit it to use the model default.

### Claude API key (intent classification + answers)

Required for the voice pipeline's intent parsing and Q&A responses. Get a key at [console.anthropic.com](https://console.anthropic.com).

```bash
# macOS / Linux
export ANTHROPIC_API_KEY=sk-ant-...
npm run dev

# Windows (PowerShell)
$env:ANTHROPIC_API_KEY = "sk-ant-..."
npm run dev
```

Or copy `.env.example` to `.env` and fill it in.

The key is only used when the voice pipeline processes a capture. There is no background syncing.

<br>

## Accessing from your phone

IRONLOG is a PWA. For phone access without port-forwarding your home network, install [Tailscale](https://tailscale.com) on both your PC and phone — free for personal use. Your PC's Tailscale IP becomes a private address accessible only to your devices. Navigate to `http://<tailscale-ip>:5173` in Safari and add it to your home screen.

<br>

## Auto-start with PM2

To keep the server running through reboots:

```bash
npm install -g pm2
npm run build
pm2 start ecosystem.config.js
pm2 save
pm2 startup
```

<br>

## How it's built

Two runtime dependencies outside of the Node ecosystem: `faster-whisper` (Python) for transcription and `piper` (binary) for TTS. Everything else is npm.

- **React 18 + Vite + TypeScript** — fast builds, PWA plugin, no ceremony. `npm run dev` and it's running.
- **vite-plugin-pwa** — service worker caches the app shell so the UI loads offline. `localStorage` mirrors critical task state so you can read and check off tasks without a server connection.
- **Tailwind CSS** — industrial dark theme (near-black base, yellow accent) built without a custom CSS file. Anton for display type, JetBrains Mono for all numbers and labels, IBM Plex Sans for body copy.
- **chrono-node** — parses natural language dates in the Quick Add bar and voice task creation. Runs entirely in the browser/server, no API call.
- **Express + TypeScript** — the API server. CommonJS output compiled via `tsc`. One language across the whole stack.
- **lowdb** — JSON on disk. Single-user data volume doesn't need a database server. The file is trivially backed up and human-readable.
- **faster-whisper** — local speech transcription via Python. Runs on CPU with int8 quantization, no GPU required, no API cost. Wrapped in a batch script so the server doesn't need to know it's Python.
- **Piper TTS** — local speech synthesis. Free, self-hosted, no API cost. Voice quality and speed are tunable per model.
- **Claude API** — the one external dependency. Used for intent classification ("is this a task or a fitness note?") and Q&A responses. Prompt caching keeps costs low — the system prompt is cached and reused across captures. The key only leaves your machine when you make a capture.
- **PM2** — keeps the server running on Windows through reboots. `pm2 startup` hooks into the Windows task scheduler.
- **Tailscale** — private mesh network between your PC and devices. No port forwarding, no dynamic DNS, no public exposure. Free for personal use.

<br>

## Privacy

The app makes exactly one outbound request — to `api.anthropic.com` when the voice pipeline processes a capture. Whisper transcription and Piper synthesis run locally. There is no telemetry, no analytics, no background syncing. Your data never leaves your machine except in that single HTTPS call, and only the transcript and minimal context are sent — not your full history.

---

*Built for personal use and shared as-is. Issues and PRs welcome.*
