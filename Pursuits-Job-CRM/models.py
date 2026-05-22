"""Data model for the job-search CRM.

We use plain dataclasses with to_dict / from_dict so the JSON store can
serialize them cleanly. from_dict ignores unknown fields, which gives us
forward compatibility — older db.json files keep working when we add
new fields in later phases.
"""
from dataclasses import dataclass, field, asdict, fields
from datetime import datetime, timezone
from typing import Optional
import uuid


# Pipeline stages, in progression order. Order matters for sorting.
STATUSES = ['lead', 'applied', 'screen', 'interview', 'onsite', 'offer', 'closed']

# Sub-status for closed opportunities
CLOSE_REASONS = ['rejected', 'withdrawn', 'accepted', 'ghosted']

WORK_MODES = ['remote', 'hybrid', 'onsite']

SOURCES = ['linkedin', 'indeed', 'referral', 'recruiter', 'company-site', 'other']

# Note types — maps to a small icon + accent color in the timeline UI
NOTE_TYPES = ['note', 'call', 'email', 'meeting']

# Age buckets, computed on the fly from last_contact_at. Order matters
# for filter UI — keep ascending. The thresholds (in days) are the
# UPPER bound of each bucket; "stale_3m" is the catch-all.
AGE_BUCKETS = [
    ('fresh',    'Fresh',     7),    # 0–7 days
    ('aging',    'Aging',     14),   # 7–14
    ('stale_2w', '2w stale',  30),   # 14–30
    ('stale_1m', '1m stale',  90),   # 30–90
    ('stale_3m', '3m+ stale', None), # 90+ (catch-all)
]
AGE_BUCKET_KEYS = [k for k, _, _ in AGE_BUCKETS]
AGE_BUCKET_LABELS = {k: label for k, label, _ in AGE_BUCKETS}


def age_bucket(last_contact_iso: str) -> str:
    """Map an ISO timestamp to an age bucket key. Empty string is
    treated as the most stale — encourages logging at least one note."""
    from datetime import datetime, timezone
    if not last_contact_iso:
        return 'stale_3m'
    try:
        ts = datetime.fromisoformat(last_contact_iso)
    except (ValueError, TypeError):
        return 'stale_3m'
    if ts.tzinfo is None:
        ts = ts.replace(tzinfo=timezone.utc)
    days = (datetime.now(timezone.utc) - ts).total_seconds() / 86400
    for key, _, threshold in AGE_BUCKETS:
        if threshold is None or days < threshold:
            return key
    return 'stale_3m'


def is_snoozed(snoozed_until_iso: str) -> bool:
    """True if `snoozed_until` is in the future."""
    from datetime import datetime, timezone
    if not snoozed_until_iso:
        return False
    try:
        ts = datetime.fromisoformat(snoozed_until_iso)
    except (ValueError, TypeError):
        return False
    if ts.tzinfo is None:
        ts = ts.replace(tzinfo=timezone.utc)
    return ts > datetime.now(timezone.utc)


def now_iso() -> str:
    """ISO-8601 UTC timestamp. Stored as string so JSON can hold it directly."""
    return datetime.now(timezone.utc).isoformat()


def new_id() -> str:
    """12-char hex id. Short enough for URLs, plenty of entropy for personal use."""
    return uuid.uuid4().hex[:12]


@dataclass
class Contact:
    """Used for both recruiter and hiring contact slots."""
    name: str = ''
    phone: str = ''
    email: str = ''
    org: str = ''       # agency for recruiter, "their role" for hiring contact
    linkedin: str = ''  # full URL to their LinkedIn profile


@dataclass
class Note:
    """A timestamped note. Comes online in Phase 3."""
    id: str = field(default_factory=new_id)
    ts: str = field(default_factory=now_iso)
    type: str = 'note'  # note | call | email | meeting
    body: str = ''


@dataclass
class Attachment:
    """Uploaded file metadata. The bytes live on disk in data/uploads/<opp_id>/."""
    id: str = field(default_factory=new_id)
    filename: str = ''       # name on disk (we generate this)
    original_name: str = ''  # what the user dragged in
    mime: str = ''
    size: int = 0
    uploaded_at: str = field(default_factory=now_iso)


@dataclass
class Opportunity:
    id: str = field(default_factory=new_id)

    # The job
    company: str = ''
    role: str = ''
    location: str = ''
    work_mode: str = ''  # remote | hybrid | onsite | ''
    salary_min: Optional[int] = None
    salary_max: Optional[int] = None
    url: str = ''
    source: str = ''
    status: str = 'lead'
    close_reason: str = ''  # only meaningful when status == 'closed'

    # People — stored as dicts (not nested Contact objects) so JSON round-trip is trivial
    recruiter: dict = field(default_factory=lambda: asdict(Contact()))
    hiring_contact: dict = field(default_factory=lambda: asdict(Contact()))

    # Timestamps
    created_at: str = field(default_factory=now_iso)
    updated_at: str = field(default_factory=now_iso)
    last_contact_at: str = field(default_factory=now_iso)

    # Interview tracking (used in later phases)
    interview_at: str = ''
    thank_you_sent: bool = False

    # Snooze (used in Phase 4)
    snoozed_until: str = ''  # ISO date

    # Lists (populated in later phases)
    tags: list = field(default_factory=list)
    notes: list = field(default_factory=list)
    attachments: list = field(default_factory=list)

    # Soft delete
    archived: bool = False
    archived_at: str = ''

    @classmethod
    def from_dict(cls, d: dict) -> 'Opportunity':
        """Build from JSON. Unknown keys are ignored so we can add fields later
        without breaking existing db.json files."""
        valid = {f.name for f in fields(cls)}
        clean = {k: v for k, v in d.items() if k in valid}
        return cls(**clean)

    def to_dict(self) -> dict:
        return asdict(self)
