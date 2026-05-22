"""Flask app — Phases 1–5.

Routes:

  Opportunities:
    GET  /                  list active (sortable, searchable, filterable)
    GET  /today             today view — what needs attention right now
    GET  /dashboard         charts: funnel, activity, age, status
    GET  /archive           archived list
    GET  /new               new-opportunity form
    POST /new               create
    GET  /<id>              detail
    GET  /<id>/edit         edit form
    POST /<id>/edit         save
    POST /<id>/archive      soft delete
    POST /<id>/restore      un-archive
    POST /<id>/status       inline status change (HTMX)
    POST /<id>/snooze       set / clear snooze (HTMX)
    POST /<id>/tags         set tags (HTMX)
    POST /<id>/thank_you    toggle thank-you-sent (HTMX)
    GET  /<id>/ics          download interview as .ics

  Notes & attachments — see Phase 3.
"""
import csv
import io
import json
import mimetypes
import os
import re
import urllib.error
import urllib.request
from collections import Counter, defaultdict
from datetime import datetime, timedelta, timezone
from pathlib import Path

from flask import (Flask, Response, abort, flash, redirect, render_template,
                   request, send_from_directory, url_for)
from PIL import Image, UnidentifiedImageError
from werkzeug.utils import secure_filename

from models import (AGE_BUCKET_KEYS, AGE_BUCKET_LABELS, CLOSE_REASONS,
                    NOTE_TYPES, SOURCES, STATUSES, WORK_MODES,
                    Attachment, Opportunity, age_bucket, is_snoozed,
                    new_id, now_iso)
from store import Store

APP_DIR = Path(__file__).parent
_data_env = os.environ.get('PURSUITS_DATA_DIR')
DATA_DIR = Path(_data_env) if _data_env else APP_DIR / 'data'

# Upload limits and allowed mime prefixes. Keep this conservative — it's
# your local machine and you own the inputs, but a 10MB cap prevents
# accidental drag-of-the-wrong-folder mistakes.
MAX_UPLOAD_BYTES = 10 * 1024 * 1024  # 10 MB per file
ALLOWED_MIME_PREFIXES = ('image/', 'application/pdf', 'text/',
                         'application/msword',
                         'application/vnd.openxmlformats-officedocument',
                         'application/vnd.ms-')

app = Flask(__name__)
app.secret_key = 'local-dev-only-not-for-deployment'  # only used for flash messages
app.config['MAX_CONTENT_LENGTH'] = MAX_UPLOAD_BYTES * 10  # request total
store = Store(DATA_DIR)


@app.context_processor
def inject_globals():
    """Make these constants available in every template without passing them
    explicitly every time."""
    return {
        'STATUSES': STATUSES,
        'WORK_MODES': WORK_MODES,
        'SOURCES': SOURCES,
        'CLOSE_REASONS': CLOSE_REASONS,
        'NOTE_TYPES': NOTE_TYPES,
        'AGE_BUCKET_KEYS': AGE_BUCKET_KEYS,
        'AGE_BUCKET_LABELS': AGE_BUCKET_LABELS,
        # so templates can compute these without round-tripping
        'age_bucket': age_bucket,
        'is_snoozed': is_snoozed,
        'now_iso': now_iso,
        'llm_available': _llm_available,
    }


def _opp_from_form(form, existing: Opportunity = None) -> Opportunity:
    """Build/update an Opportunity from POSTed form data.

    `existing` is passed on edit so we preserve fields that aren't in the form
    yet (notes, attachments, tags, snooze, archived state, original created_at).
    """
    opp = existing or Opportunity()

    opp.company = form.get('company', '').strip()
    opp.role = form.get('role', '').strip()
    opp.location = form.get('location', '').strip()
    opp.work_mode = form.get('work_mode', '').strip()
    opp.url = form.get('url', '').strip()
    opp.source = form.get('source', '').strip()
    opp.status = form.get('status', 'lead').strip()
    opp.close_reason = form.get('close_reason', '').strip()

    # Salary — empty string means "unknown", store None
    sal_min = form.get('salary_min', '').strip().replace(',', '').replace('$', '')
    sal_max = form.get('salary_max', '').strip().replace(',', '').replace('$', '')
    opp.salary_min = int(sal_min) if sal_min.isdigit() else None
    opp.salary_max = int(sal_max) if sal_max.isdigit() else None

    opp.recruiter = {
        'name':     form.get('recruiter_name',     '').strip(),
        'phone':    form.get('recruiter_phone',    '').strip(),
        'email':    form.get('recruiter_email',    '').strip(),
        'org':      form.get('recruiter_org',      '').strip(),
        'linkedin': form.get('recruiter_linkedin', '').strip(),
    }
    opp.hiring_contact = {
        'name':     form.get('hiring_name',     '').strip(),
        'phone':    form.get('hiring_phone',    '').strip(),
        'email':    form.get('hiring_email',    '').strip(),
        'org':      form.get('hiring_role',     '').strip(),
        'linkedin': form.get('hiring_linkedin', '').strip(),
    }

    # Interview scheduling. <input type=datetime-local> gives us
    # 'YYYY-MM-DDTHH:MM' (no timezone). We treat it as the user's local
    # wall-clock time and store the ISO string verbatim — the .ics export
    # then writes it as a "floating" datetime, which calendar apps render
    # in the local timezone of whoever opens it. That's the right behavior
    # for "interview at 2pm" — you want it at 2pm wherever you are, not
    # converted to UTC and back.
    opp.interview_at = form.get('interview_at', '').strip()
    opp.thank_you_sent = form.get('thank_you_sent') == 'on'

    return opp


def _matches_query(opp: Opportunity, q: str) -> bool:
    """Case-insensitive substring search across the fields most useful
    for finding a specific opportunity in a hurry. Notes and tags are
    included so 'budapest' or 'great chat' surface the right rows."""
    parts = [
        opp.company, opp.role, opp.location, opp.status, opp.source,
        opp.recruiter.get('name', ''),
        opp.recruiter.get('org', ''),
        opp.hiring_contact.get('name', ''),
        opp.hiring_contact.get('org', ''),
        ' '.join(opp.tags),
        ' '.join(n.get('body', '') for n in opp.notes),
    ]
    return q in ' '.join(filter(None, parts)).lower()


def _is_htmx() -> bool:
    """HTMX sets this header on every request it issues. We use it to
    decide whether to return a full page or just a fragment."""
    return request.headers.get('HX-Request') == 'true'


# ---- routes ------------------------------------------------------------

@app.route('/')
def index():
    sort = request.args.get('sort', 'updated_at')
    direction = request.args.get('dir', 'desc')
    q = request.args.get('q', '').strip()

    # Multi-select filters via getlist — checkboxes share the same name.
    # Empty list means "no filter applied" (show everything).
    f_status = request.args.getlist('status')
    f_age    = request.args.getlist('age')
    f_source = request.args.getlist('source')
    f_tag    = request.args.getlist('tag')
    show_snoozed = request.args.get('show_snoozed') == '1'

    opps = store.all(include_archived=False)

    # Build the universe of all manual tags BEFORE filtering, so the
    # sidebar always shows all available tags even when the current
    # filter would hide some of them.
    all_tags = sorted({t for o in opps for t in o.tags})

    if q:
        needle = q.lower()
        opps = [o for o in opps if _matches_query(o, needle)]

    if f_status:
        opps = [o for o in opps if o.status in f_status]

    if f_age:
        opps = [o for o in opps if age_bucket(o.last_contact_at) in f_age]

    if f_source:
        opps = [o for o in opps if o.source in f_source]

    if f_tag:
        opps = [o for o in opps if any(t in f_tag for t in o.tags)]

    if not show_snoozed:
        # Hide snoozed entirely. The "show_snoozed" toggle in the sidebar
        # flips to keeping them in but still sorted to the bottom.
        opps = [o for o in opps if not is_snoozed(o.snoozed_until)]

    # Sort key. Snoozed always sort to the END regardless of direction —
    # that's the "out of the way for working but still present" behavior
    # you described. We do this with a tuple key: (is_snoozed, real_key).
    # Booleans sort False<True, so non-snoozed always come first.
    real_key = {
        'company':         lambda o: o.company.lower(),
        'role':            lambda o: o.role.lower(),
        'status':          lambda o: STATUSES.index(o.status) if o.status in STATUSES else 99,
        'created_at':      lambda o: o.created_at,
        'updated_at':      lambda o: o.updated_at,
        'last_contact_at': lambda o: o.last_contact_at,
    }.get(sort, lambda o: o.updated_at)

    if direction == 'desc':
        # For desc, we still want snoozed last. Trick: invert the
        # primary key by wrapping a Reverse helper, or just reverse the
        # whole list and flip the snooze bit. Simpler: sort ascending,
        # then reverse non-snoozed in place. Cleanest: two passes.
        opps.sort(key=real_key, reverse=True)
        opps.sort(key=lambda o: is_snoozed(o.snoozed_until))  # stable: snoozed last
    else:
        opps.sort(key=real_key)
        opps.sort(key=lambda o: is_snoozed(o.snoozed_until))

    filter_state = {
        'q': q,
        'status': f_status,
        'age': f_age,
        'source': f_source,
        'tag': f_tag,
        'show_snoozed': show_snoozed,
        'sort': sort,
        'dir': direction,
    }

    # HTMX search/filter: return only the rows fragment so it can be
    # swapped into #opp-list-wrapper without reloading the chrome.
    if _is_htmx():
        return render_template('_list_body.html', opps=opps, q=q)

    return render_template('list.html', opps=opps, all_tags=all_tags,
                           filters=filter_state, sort=sort, dir=direction, q=q)


@app.route('/archive')
def archive_view():
    archived = [o for o in store.all(include_archived=True) if o.archived]
    archived.sort(key=lambda o: o.archived_at or '', reverse=True)
    return render_template('archive.html', opps=archived)


@app.route('/new', methods=['GET', 'POST'])
def new():
    if request.method == 'POST':
        opp = _opp_from_form(request.form)
        if not opp.company or not opp.role:
            flash('Company and role are required.', 'error')
            return render_template('form.html', opp=opp, mode='new')
        store.save(opp)
        flash(f'Created — {opp.company} · {opp.role}', 'success')
        return redirect(url_for('detail', opp_id=opp.id))

    # Prefill from query params (used by the bookmarklet — see /bookmarklet)
    seed = Opportunity()
    seed.url = request.args.get('url', '').strip()
    seed.company = request.args.get('company', '').strip()
    seed.role = request.args.get('role', '').strip()
    paste = request.args.get('paste', '').strip()
    return render_template('form.html', opp=seed, mode='new', paste_seed=paste)


@app.route('/<opp_id>')
def detail(opp_id):
    opp = store.get(opp_id)
    if not opp:
        abort(404)
    return render_template('detail.html', opp=opp)


@app.route('/<opp_id>/edit', methods=['GET', 'POST'])
def edit(opp_id):
    opp = store.get(opp_id)
    if not opp:
        abort(404)

    if request.method == 'POST':
        opp = _opp_from_form(request.form, existing=opp)
        if not opp.company or not opp.role:
            flash('Company and role are required.', 'error')
            return render_template('form.html', opp=opp, mode='edit')
        store.save(opp)
        flash('Saved.', 'success')
        return redirect(url_for('detail', opp_id=opp.id))

    return render_template('form.html', opp=opp, mode='edit')


@app.route('/<opp_id>/status', methods=['POST'])
def update_status(opp_id):
    """HTMX endpoint for inline status changes from the list view.

    The select element on each row triggers this with name="status" in
    the form body. We save the new status and return the row's HTML,
    which HTMX swaps in to replace the existing row.
    """
    opp = store.get(opp_id)
    if not opp:
        abort(404)
    new_status = request.form.get('status', '').strip()
    if new_status in STATUSES:
        opp.status = new_status
        store.save(opp)
    return render_template('_opp_row.html', opp=opp)


@app.route('/<opp_id>/archive', methods=['POST'])
def archive_action(opp_id):
    if not store.get(opp_id):
        abort(404)
    store.archive(opp_id)
    if _is_htmx():
        # Empty body + outerHTML swap = the row gets replaced with
        # nothing, i.e. removed. The fade-out comes from the
        # `swap:250ms` delay in the button's hx-swap and the
        # .htmx-swapping CSS in base.html.
        return ''
    flash('Archived. You can restore it from the Archive view.', 'success')
    return redirect(url_for('index'))


@app.route('/<opp_id>/restore', methods=['POST'])
def restore_action(opp_id):
    if not store.get(opp_id):
        abort(404)
    store.restore(opp_id)
    flash('Restored.', 'success')
    return redirect(url_for('archive_view'))


# ---- API extract / bookmarklet / CSV ----------------------------------

# Default model for the extraction call. Haiku is the right choice for
# structured extraction — it's fast, cheap, and accurate enough for
# pulling fields out of a job posting. Override via env var if you want
# Sonnet/Opus.
ANTHROPIC_MODEL = os.environ.get('ANTHROPIC_MODEL', 'claude-haiku-4-5')

EXTRACT_PROMPT = """You are a structured data extractor. Given a job posting, extract these fields and return ONLY a JSON object — no commentary, no markdown fences, no explanation.

Schema:
{
  "company": "string (the hiring company name)",
  "role": "string (the job title)",
  "location": "string (city/region or 'Remote', empty if absent)",
  "work_mode": "remote | hybrid | onsite | empty",
  "salary_min": integer or null,
  "salary_max": integer or null,
  "url": "string or empty"
}

Rules:
- Extract numbers literally; if salary is in thousands ('$120k'), convert to 120000.
- If salary is a range, populate both min and max. If single, populate min only.
- Empty string for missing text fields, null for missing numeric fields.
- Do NOT invent data. If a field isn't in the posting, leave it empty.

Job posting:
---
"""


def _llm_available() -> bool:
    return bool(os.environ.get('ANTHROPIC_API_KEY'))


@app.route('/api/extract', methods=['POST'])
def api_extract():
    """Hit the Anthropic API to extract structured fields from a pasted
    job posting. Returns JSON: {ok: bool, fields: {...}, error?: str}.

    Why stdlib urllib instead of the anthropic SDK: keeps deps minimal.
    The API surface we need is one POST with three headers.
    """
    api_key = os.environ.get('ANTHROPIC_API_KEY', '').strip()
    if not api_key:
        return {'ok': False, 'error': 'ANTHROPIC_API_KEY not set in environment'}, 503

    text = request.form.get('text', '').strip()
    if not text:
        return {'ok': False, 'error': 'No text provided'}, 400
    # Generous cap — most job postings are well under this. Avoids
    # accidentally sending a whole webpage.
    if len(text) > 50_000:
        text = text[:50_000]

    payload = {
        'model': ANTHROPIC_MODEL,
        'max_tokens': 1024,
        'messages': [{'role': 'user', 'content': EXTRACT_PROMPT + text}],
    }
    req = urllib.request.Request(
        'https://api.anthropic.com/v1/messages',
        data=json.dumps(payload).encode('utf-8'),
        headers={
            'x-api-key': api_key,
            'anthropic-version': '2023-06-01',
            'content-type': 'application/json',
        },
        method='POST',
    )

    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            body = json.loads(resp.read().decode('utf-8'))
    except urllib.error.HTTPError as e:
        try:
            err_body = json.loads(e.read().decode('utf-8'))
            msg = err_body.get('error', {}).get('message', f'HTTP {e.code}')
        except Exception:
            msg = f'HTTP {e.code}'
        return {'ok': False, 'error': f'API error: {msg}'}, 502
    except (urllib.error.URLError, TimeoutError) as e:
        return {'ok': False, 'error': f'Network error: {e}'}, 502

    # Response shape: { content: [{type: 'text', text: '...'}], ... }
    try:
        content_blocks = body.get('content', [])
        text_out = next((b['text'] for b in content_blocks if b.get('type') == 'text'), '')
    except (KeyError, TypeError):
        return {'ok': False, 'error': 'Unexpected API response shape'}, 502

    # The model is instructed to return raw JSON. Strip whitespace and
    # any code fences in case it adds them anyway.
    cleaned = text_out.strip()
    if cleaned.startswith('```'):
        cleaned = re.sub(r'^```(?:json)?\s*', '', cleaned)
        cleaned = re.sub(r'\s*```$', '', cleaned)

    try:
        fields = json.loads(cleaned)
    except json.JSONDecodeError:
        return {'ok': False, 'error': 'Model returned non-JSON; try simplifying the input'}, 502

    # Defensive: only allow whitelisted keys through. Stops the model
    # from accidentally returning random extras.
    allowed = {'company', 'role', 'location', 'work_mode',
               'salary_min', 'salary_max', 'url'}
    fields = {k: v for k, v in fields.items() if k in allowed}

    return {'ok': True, 'fields': fields}


@app.route('/export/csv')
def export_csv():
    """Stream a CSV of all opportunities — including archived. Useful
    for backup, importing into a spreadsheet, or migrating elsewhere.

    csv.writer needs a real file-like object, so we use a StringIO
    buffer. For ~1000 records this is well under a megabyte.
    """
    opps = store.all(include_archived=True)

    buf = io.StringIO()
    writer = csv.writer(buf)
    writer.writerow([
        'id', 'company', 'role', 'location', 'work_mode',
        'status', 'close_reason', 'source',
        'salary_min', 'salary_max', 'url',
        'recruiter_name', 'recruiter_org', 'recruiter_email', 'recruiter_phone', 'recruiter_linkedin',
        'hiring_name', 'hiring_role', 'hiring_email', 'hiring_phone', 'hiring_linkedin',
        'interview_at', 'thank_you_sent',
        'tags', 'snoozed_until',
        'created_at', 'updated_at', 'last_contact_at',
        'note_count', 'attachment_count',
        'archived', 'archived_at',
    ])
    for o in opps:
        writer.writerow([
            o.id, o.company, o.role, o.location, o.work_mode,
            o.status, o.close_reason, o.source,
            o.salary_min or '', o.salary_max or '', o.url,
            o.recruiter.get('name', ''), o.recruiter.get('org', ''),
            o.recruiter.get('email', ''), o.recruiter.get('phone', ''),
            o.recruiter.get('linkedin', ''),
            o.hiring_contact.get('name', ''), o.hiring_contact.get('org', ''),
            o.hiring_contact.get('email', ''), o.hiring_contact.get('phone', ''),
            o.hiring_contact.get('linkedin', ''),
            o.interview_at, 'yes' if o.thank_you_sent else '',
            ', '.join(o.tags), o.snoozed_until,
            o.created_at, o.updated_at, o.last_contact_at,
            len(o.notes), len(o.attachments),
            'yes' if o.archived else '', o.archived_at,
        ])

    body = buf.getvalue()
    fname = f'pursuits-export-{datetime.now(timezone.utc).strftime("%Y%m%d")}.csv'
    return Response(
        body,
        mimetype='text/csv',
        headers={
            'Content-Type': 'text/csv; charset=utf-8',
            'Content-Disposition': f'attachment; filename="{fname}"',
        },
    )


@app.route('/bookmarklet')
def bookmarklet_page():
    """Page that explains and provides the install link for the
    'Save to Pursuits' bookmarklet."""
    return render_template('bookmarklet.html', llm_available=_llm_available())


# ---- today + dashboard + interview ------------------------------------

def _parse_local_iso(s: str):
    """Parse an ISO string that may or may not have tz info. Returns
    a datetime or None. Used for interview_at which is naive (local)."""
    if not s:
        return None
    try:
        return datetime.fromisoformat(s)
    except (ValueError, TypeError):
        return None


@app.route('/today')
def today_view():
    """The page you open every morning during a search.

    Four buckets, in priority order:
      1. Snoozes that have woken up since the last visit
      2. Stale items (2w+, not snoozed) — need a follow-up nudge
      3. Upcoming interviews in the next 7 days
      4. Outstanding thank-yous (interview happened, thank_you_sent is False)
    """
    opps = store.all(include_archived=False)
    now = datetime.now(timezone.utc)
    week_from_now = now + timedelta(days=7)

    # Bucket 1: woke up. snoozed_until is set, in the past, AND status
    # implies they're not done. (If you snoozed a closed opp, leave it.)
    woken = []
    for o in opps:
        if not o.snoozed_until or o.status == 'closed':
            continue
        ts = _parse_local_iso(o.snoozed_until)
        if ts and ts.tzinfo is None:
            ts = ts.replace(tzinfo=timezone.utc)
        if ts and ts <= now:
            woken.append(o)
    woken.sort(key=lambda o: o.snoozed_until or '', reverse=True)

    # Bucket 2: stale (2w+), not snoozed, not closed
    stale = []
    for o in opps:
        if is_snoozed(o.snoozed_until) or o.status == 'closed':
            continue
        bucket = age_bucket(o.last_contact_at)
        if bucket in ('stale_2w', 'stale_1m', 'stale_3m'):
            stale.append((bucket, o))
    # Most stale first
    bucket_order = {'stale_3m': 0, 'stale_1m': 1, 'stale_2w': 2}
    stale.sort(key=lambda pair: (bucket_order.get(pair[0], 99), pair[1].last_contact_at))
    stale_opps = [o for _, o in stale]

    # Bucket 3: interviews in next 7 days (or earlier — past interviews
    # without thank-yous fall into bucket 4 below).
    upcoming = []
    for o in opps:
        ts = _parse_local_iso(o.interview_at)
        if not ts:
            continue
        # Naive (local) — assume "now" in same offset for comparison
        if ts.tzinfo is None:
            ts_cmp = ts.replace(tzinfo=timezone.utc)
        else:
            ts_cmp = ts
        if now <= ts_cmp <= week_from_now:
            upcoming.append((ts, o))
    upcoming.sort(key=lambda pair: pair[0])
    upcoming_opps = [o for _, o in upcoming]

    # Bucket 4: outstanding thank-yous. Interview was in the past,
    # thank_you_sent is still False.
    thank_yous = []
    for o in opps:
        if o.thank_you_sent:
            continue
        ts = _parse_local_iso(o.interview_at)
        if not ts:
            continue
        if ts.tzinfo is None:
            ts_cmp = ts.replace(tzinfo=timezone.utc)
        else:
            ts_cmp = ts
        if ts_cmp < now:
            thank_yous.append((ts, o))
    thank_yous.sort(key=lambda pair: pair[0], reverse=True)
    thank_you_opps = [o for _, o in thank_yous]

    return render_template('today.html',
                           woken=woken,
                           stale=stale_opps,
                           upcoming=upcoming_opps,
                           thank_yous=thank_you_opps)


def _iso_year_week(dt: datetime) -> str:
    """ISO week label like '2026-W17'. Used for x-axis labels."""
    y, w, _ = dt.isocalendar()
    return f'{y}-W{w:02d}'


def _dashboard_data(opps):
    """Compute the data for all four charts in one pass over the active list.

    Why pre-compute everything server-side: the frontend doesn't have to
    re-aggregate, the chart configs stay tiny, and there's exactly one
    pass over the data. We hand the results to the template as Python
    objects; the template emits them as JSON via |tojson.
    """
    # 1. Funnel — counts of active (non-closed) opps per status
    funnel_counts = Counter()
    for o in opps:
        if o.status != 'closed':
            funnel_counts[o.status] += 1
    funnel = {
        'labels': [s for s in STATUSES if s != 'closed'],
        'data':   [funnel_counts[s] for s in STATUSES if s != 'closed'],
    }

    # 2. Activity by week — last 12 weeks, stacked by source.
    # Build the week list first so empty weeks still appear on the x-axis.
    now = datetime.now(timezone.utc)
    weeks = []
    for i in range(11, -1, -1):
        wk = now - timedelta(weeks=i)
        weeks.append(_iso_year_week(wk))

    # source -> {week_label: count}
    src_week = defaultdict(lambda: Counter())
    for o in opps:
        ts = _parse_local_iso(o.created_at)
        if not ts:
            continue
        if ts.tzinfo is None:
            ts = ts.replace(tzinfo=timezone.utc)
        # Only count opps within the visible window
        if (now - ts).days > 7 * 12:
            continue
        wk = _iso_year_week(ts)
        src = o.source or 'other'
        src_week[src][wk] += 1

    # Output: one dataset per source, aligned to the weeks array
    activity = {
        'labels':   weeks,
        'datasets': [
            {'label': src, 'data': [src_week[src].get(w, 0) for w in weeks]}
            for src in sorted(src_week.keys())
        ],
    }

    # 3. Age histogram — all active (including snoozed, since dashboard
    # is a snapshot view of the whole pipeline)
    age_counts = Counter()
    for o in opps:
        if o.status == 'closed':
            continue
        age_counts[age_bucket(o.last_contact_at)] += 1
    age_chart = {
        'labels': [AGE_BUCKET_LABELS[k] for k in AGE_BUCKET_KEYS],
        'data':   [age_counts[k] for k in AGE_BUCKET_KEYS],
    }

    # 4. Status donut — all opps including closed (so you can see the
    # cumulative outcomes in a search). Stable label order = STATUSES.
    status_counts = Counter(o.status for o in opps)
    status_chart = {
        'labels': [s for s in STATUSES if status_counts[s] > 0],
        'data':   [status_counts[s] for s in STATUSES if status_counts[s] > 0],
    }

    # Headline numbers for the tiles above the charts
    closed_total = status_counts.get('closed', 0)
    closed_outcomes = Counter()
    for o in opps:
        if o.status == 'closed' and o.close_reason:
            closed_outcomes[o.close_reason] += 1

    headline = {
        'total':       len(opps),
        'active':      sum(1 for o in opps if o.status != 'closed'),
        'interviews':  sum(1 for o in opps if o.status in ('screen', 'interview', 'onsite')),
        'offers':      status_counts.get('offer', 0),
        'closed':      closed_total,
        'accepted':    closed_outcomes.get('accepted', 0),
        'rejected':    closed_outcomes.get('rejected', 0),
        'ghosted':     closed_outcomes.get('ghosted', 0),
    }

    return {
        'headline': headline,
        'funnel': funnel,
        'activity': activity,
        'age': age_chart,
        'status': status_chart,
    }


@app.route('/dashboard')
def dashboard():
    opps = store.all(include_archived=False)
    data = _dashboard_data(opps)
    return render_template('dashboard.html', **data)


@app.route('/<opp_id>/thank_you', methods=['POST'])
def toggle_thank_you(opp_id):
    """Flip the thank_you_sent boolean. Used from the detail page."""
    opp = store.get(opp_id)
    if not opp:
        abort(404)
    opp.thank_you_sent = not opp.thank_you_sent
    store.save(opp)
    if _is_htmx():
        return render_template('_thank_you_widget.html', opp=opp)
    return redirect(url_for('detail', opp_id=opp_id))


def _ics_escape(s: str) -> str:
    """Per RFC 5545 §3.3.11. Backslash + comma + semicolon must be
    escaped in TEXT values, and CRLF becomes \\n."""
    return (s or '').replace('\\', '\\\\').replace(';', '\\;') \
                    .replace(',', '\\,').replace('\n', '\\n').replace('\r', '')


def _ics_dt_floating(dt: datetime) -> str:
    """Format datetime as ICS floating local time: YYYYMMDDTHHMMSS (no Z).
    Floating time means "render in the viewer's local zone" — exactly
    right for "interview at 2pm" type events."""
    return dt.strftime('%Y%m%dT%H%M%S')


def _ics_dt_utc(dt: datetime) -> str:
    """Format as UTC: YYYYMMDDTHHMMSSZ. Used for DTSTAMP."""
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc).strftime('%Y%m%dT%H%M%SZ')


@app.route('/<opp_id>/ics')
def export_ics(opp_id):
    """Download the interview as an .ics calendar file. Double-click it
    on macOS / Windows / Linux and your default calendar app imports it.

    No external dependencies — the .ics format is plain text, and a
    minimal-but-valid VEVENT is about 12 lines.
    """
    opp = store.get(opp_id)
    if not opp:
        abort(404)
    ts = _parse_local_iso(opp.interview_at)
    if not ts:
        abort(404)

    # Default duration: 1 hour. Most interviews are 30–60 min and a
    # 60-min block is easier to reschedule than 30. Calendar app users
    # can shorten after import.
    end = ts + timedelta(hours=1)

    # Build the VEVENT. The trailing CRLFs matter — RFC 5545 mandates
    # them and some calendar apps reject LF-only files.
    lines = [
        'BEGIN:VCALENDAR',
        'VERSION:2.0',
        'PRODID:-//Pursuits//Job CRM//EN',
        'CALSCALE:GREGORIAN',
        'METHOD:PUBLISH',
        'BEGIN:VEVENT',
        f'UID:{opp.id}@pursuits.local',
        f'DTSTAMP:{_ics_dt_utc(datetime.now(timezone.utc))}',
        f'DTSTART:{_ics_dt_floating(ts)}',
        f'DTEND:{_ics_dt_floating(end)}',
        f'SUMMARY:{_ics_escape(f"Interview — {opp.company} ({opp.role})")}',
    ]
    desc_parts = [opp.role]
    if opp.url:
        desc_parts.append(opp.url)
    if opp.recruiter.get('name'):
        desc_parts.append(f'Recruiter: {opp.recruiter["name"]}')
    if opp.hiring_contact.get('name'):
        desc_parts.append(f'Hiring contact: {opp.hiring_contact["name"]}')
    lines.append(f'DESCRIPTION:{_ics_escape(chr(10).join(desc_parts))}')
    if opp.location:
        lines.append(f'LOCATION:{_ics_escape(opp.location)}')
    if opp.url:
        lines.append(f'URL:{opp.url}')
    lines.append('END:VEVENT')
    lines.append('END:VCALENDAR')

    body = '\r\n'.join(lines) + '\r\n'

    # Filename uses the company so the file makes sense in the user's
    # downloads folder. secure_filename strips anything weird.
    safe_co = secure_filename(opp.company) or 'interview'
    fname = f'{safe_co}-interview.ics'
    return Response(
        body,
        mimetype='text/calendar',
        headers={
            'Content-Type': 'text/calendar; charset=utf-8',
            'Content-Disposition': f'attachment; filename="{fname}"',
        },
    )


# ---- snooze + tags -----------------------------------------------------

@app.route('/<opp_id>/snooze', methods=['POST'])
def set_snooze(opp_id):
    """Set or clear snooze. Form field `until` is a date string (YYYY-MM-DD)
    or empty to wake. We store it as ISO with end-of-day UTC so the
    opportunity stays snoozed for that whole day in any timezone."""
    opp = store.get(opp_id)
    if not opp:
        abort(404)

    until = request.form.get('until', '').strip()
    if until:
        try:
            d = datetime.fromisoformat(until)
            # End-of-day UTC so a snooze "until tomorrow" lasts all day
            d = d.replace(hour=23, minute=59, second=59, tzinfo=timezone.utc)
            until = d.isoformat()
        except ValueError:
            flash('Invalid snooze date.', 'error')
            return redirect(url_for('detail', opp_id=opp_id))

    store.set_snooze(opp_id, until)
    if _is_htmx():
        # Reload the snooze widget so it reflects the new state
        opp = store.get(opp_id)
        return render_template('_snooze_widget.html', opp=opp)
    return redirect(url_for('detail', opp_id=opp_id))


@app.route('/<opp_id>/tags', methods=['POST'])
def set_tags(opp_id):
    """Replace the manual tag list. Form field `tags` is a comma-separated
    string. The store normalizes (lowercase, dash/underscore only)."""
    opp = store.get(opp_id)
    if not opp:
        abort(404)
    raw = request.form.get('tags', '')
    parts = [p for p in raw.replace(',', ' ').split() if p]
    store.set_tags(opp_id, parts)
    if _is_htmx():
        opp = store.get(opp_id)
        return render_template('_tags_widget.html', opp=opp)
    return redirect(url_for('detail', opp_id=opp_id))


# ---- notes -------------------------------------------------------------

@app.route('/<opp_id>/notes', methods=['POST'])
def add_note(opp_id):
    """Append a note to an opportunity. Bumps last_contact_at via the store.

    Form fields: type (one of NOTE_TYPES), body (free text).
    HTMX response: just the new note's row, prepended to the timeline.
    """
    opp = store.get(opp_id)
    if not opp:
        abort(404)

    note_type = request.form.get('type', 'note').strip()
    if note_type not in NOTE_TYPES:
        note_type = 'note'
    body = request.form.get('body', '').strip()
    if not body:
        # Return 204 — HTMX won't swap anything, form just clears the input
        return '', 204

    note = store.add_note(opp_id, note_type, body)

    if _is_htmx():
        # The HTMX trigger on the form clears the input AND re-prepends
        # this note. We use hx-swap-oob via the wrapper structure on the
        # client-side too — but here, we just return the note partial.
        return render_template('_note_item.html', note=note, opp=opp)

    return redirect(url_for('detail', opp_id=opp_id))


@app.route('/<opp_id>/notes/<note_id>/delete', methods=['POST'])
def delete_note(opp_id, note_id):
    if not store.get(opp_id):
        abort(404)
    store.delete_note(opp_id, note_id)
    if _is_htmx():
        return ''  # row removed via outerHTML swap with empty body
    return redirect(url_for('detail', opp_id=opp_id))


# ---- attachments -------------------------------------------------------

# Filenames inside data/uploads/<opp_id>/ are generated by us — see
# add_attachments below — so they're already safe. But we still validate
# any incoming filename against this regex before opening files, as a
# defense-in-depth measure against path traversal (../etc/passwd etc).
SAFE_FILENAME_RE = re.compile(r'^[a-f0-9_]+(\.[a-zA-Z0-9]+)?$')


def _allowed_mime(mime: str) -> bool:
    return any(mime.startswith(p) for p in ALLOWED_MIME_PREFIXES)


@app.route('/<opp_id>/attachments', methods=['POST'])
def add_attachments(opp_id):
    """Accept one or more files via multipart/form-data.

    The drop zone in the UI submits files under field name "files". On
    desktop drag-drop and on touch tap-to-pick, the same endpoint handles
    both — that's the whole point of using a hidden <input type=file>
    behind the drop zone.
    """
    opp = store.get(opp_id)
    if not opp:
        abort(404)

    files = request.files.getlist('files')
    if not files or all(not f.filename for f in files):
        return ('No files received', 400)

    saved = []
    skipped = []

    for file_storage in files:
        if not file_storage or not file_storage.filename:
            continue

        original_name = file_storage.filename
        mime = file_storage.mimetype or mimetypes.guess_type(original_name)[0] or 'application/octet-stream'

        if not _allowed_mime(mime):
            skipped.append(f'{original_name} ({mime} not allowed)')
            continue

        # Read into memory so we can size-check before writing to disk.
        # 10MB cap × ~10 files at most makes this fine.
        data = file_storage.read()
        if len(data) > MAX_UPLOAD_BYTES:
            skipped.append(f'{original_name} (too large)')
            continue

        # Generate a safe filename of our own. Format: <random_id>.<ext>
        # We keep the original name in metadata for display.
        safe_orig = secure_filename(original_name) or 'file'
        ext = ''
        if '.' in safe_orig:
            ext = '.' + safe_orig.rsplit('.', 1)[1].lower()
            # extension whitelist — keep it short
            if ext not in ('.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp',
                           '.pdf', '.txt', '.md', '.csv', '.doc', '.docx',
                           '.xls', '.xlsx', '.ppt', '.pptx', '.rtf'):
                ext = ''

        att_id = new_id()
        on_disk_name = f'{att_id}{ext}'
        target = store.opp_upload_dir(opp_id) / on_disk_name
        with open(target, 'wb') as f:
            f.write(data)

        att = {
            'id': att_id,
            'filename': on_disk_name,
            'original_name': original_name,
            'mime': mime,
            'size': len(data),
            'uploaded_at': now_iso(),
        }
        store.add_attachment(opp_id, att)
        saved.append(att)

    if _is_htmx():
        # Return the new attachments rendered as cards. Client-side, the
        # drop zone targets the attachment grid with hx-swap="afterbegin",
        # which prepends these without disturbing existing items.
        return render_template('_attachments_added.html', attachments=saved, opp=opp, skipped=skipped)

    if skipped:
        flash('Some files skipped: ' + ', '.join(skipped), 'error')
    if saved:
        flash(f'Added {len(saved)} file{"s" if len(saved) != 1 else ""}.', 'success')
    return redirect(url_for('detail', opp_id=opp_id))


@app.route('/<opp_id>/attachments/<att_id>')
def download_attachment(opp_id, att_id):
    """Serve the original file. We look it up by id (safer than trusting
    a filename in the URL) and use send_from_directory which validates
    the filename against directory traversal."""
    opp = store.get(opp_id)
    if not opp:
        abort(404)

    att = next((a for a in opp.attachments if a.get('id') == att_id), None)
    if not att:
        abort(404)

    fname = att['filename']
    if not SAFE_FILENAME_RE.match(fname):
        abort(404)

    # `as_attachment=False` lets the browser display PDFs/images inline
    # if the user clicks; download_name preserves the original name if
    # they choose to save.
    return send_from_directory(
        store.opp_upload_dir(opp_id),
        fname,
        as_attachment=False,
        download_name=att.get('original_name') or fname,
    )


@app.route('/<opp_id>/attachments/<att_id>/thumb')
def attachment_thumb(opp_id, att_id):
    """Generate (and cache) a 240px thumbnail for image attachments.

    Thumbnails are written next to the original as `thumb_<filename>`.
    First request generates and saves; subsequent requests just send the
    cached file. Pillow handles all the heavy lifting.
    """
    opp = store.get(opp_id)
    if not opp:
        abort(404)
    att = next((a for a in opp.attachments if a.get('id') == att_id), None)
    if not att or not SAFE_FILENAME_RE.match(att['filename']):
        abort(404)
    if not att['mime'].startswith('image/'):
        abort(404)

    opp_dir = store.opp_upload_dir(opp_id)
    thumb_name = f'thumb_{att["filename"]}'
    thumb_path = opp_dir / thumb_name

    if not thumb_path.exists():
        src = opp_dir / att['filename']
        try:
            with Image.open(src) as im:
                im.thumbnail((240, 240))
                # Convert RGBA→RGB for JPEG; otherwise keep mode.
                if thumb_path.suffix.lower() in ('.jpg', '.jpeg') and im.mode != 'RGB':
                    im = im.convert('RGB')
                im.save(thumb_path)
        except (UnidentifiedImageError, OSError):
            abort(404)

    return send_from_directory(opp_dir, thumb_name)


@app.route('/<opp_id>/attachments/<att_id>/delete', methods=['POST'])
def delete_attachment(opp_id, att_id):
    if not store.get(opp_id):
        abort(404)
    store.delete_attachment(opp_id, att_id)
    if _is_htmx():
        return ''
    return redirect(url_for('detail', opp_id=opp_id))


# ---- jinja filters -----------------------------------------------------

@app.template_filter('datefmt')
def datefmt(iso_string: str, fmt: str = '%Y-%m-%d') -> str:
    """Format an ISO string for display. Empty string in -> empty string out."""
    if not iso_string:
        return ''
    try:
        return datetime.fromisoformat(iso_string).strftime(fmt)
    except (ValueError, TypeError):
        return iso_string[:10]  # fallback: trust the prefix


@app.template_filter('filesize')
def filesize(num_bytes) -> str:
    """1234 -> '1.2 KB'. Used in the attachments grid."""
    try:
        n = float(num_bytes)
    except (TypeError, ValueError):
        return ''
    for unit in ('B', 'KB', 'MB', 'GB'):
        if n < 1024 or unit == 'GB':
            return f'{n:.0f} {unit}' if unit == 'B' else f'{n:.1f} {unit}'
        n /= 1024


@app.template_filter('timeago')
def timeago(iso_string: str) -> str:
    """ISO timestamp -> 'just now', '5m ago', '3h ago', '2d ago', or 'YYYY-MM-DD' for older."""
    if not iso_string:
        return ''
    try:
        ts = datetime.fromisoformat(iso_string)
    except (ValueError, TypeError):
        return iso_string[:10]
    if ts.tzinfo is None:
        ts = ts.replace(tzinfo=timezone.utc)
    delta = datetime.now(timezone.utc) - ts
    secs = delta.total_seconds()
    if secs < 60:    return 'just now'
    if secs < 3600:  return f'{int(secs // 60)}m ago'
    if secs < 86400: return f'{int(secs // 3600)}h ago'
    if secs < 604800: return f'{int(secs // 86400)}d ago'
    return ts.strftime('%Y-%m-%d')


if __name__ == '__main__':
    import os
    import threading
    import webbrowser

    HOST = '127.0.0.1'
    PORT = int(os.environ.get('PURSUITS_PORT', 5000))

    # Open browser exactly once, on initial launch.
    #
    # In debug mode, Flask actually runs two processes:
    #   - parent: the file watcher / reloader
    #   - child:  the actual server (re-spawned on every save)
    # The child has WERKZEUG_RUN_MAIN=true; the parent doesn't. Gating on
    # that env var means we open the browser when you start the server,
    # but NOT every time you save a file and trigger a reload.
    #
    # The 1-second Timer gives the server a moment to bind to the port
    # before the browser tries to load it (avoids a "connection refused"
    # flash on slower machines).
    if not os.environ.get('WERKZEUG_RUN_MAIN'):
        threading.Timer(
            1.0, lambda: webbrowser.open_new(f'http://{HOST}:{PORT}')
        ).start()

    # 127.0.0.1 only — local workstation use, no LAN exposure
    app.run(host=HOST, port=PORT, debug=True)
