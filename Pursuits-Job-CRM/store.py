"""JSON-on-disk store.

Two safety mechanisms:

1. Atomic writes via tempfile + os.replace. If the process dies mid-write,
   you're left with either the old file or the new one — never a half-written
   one. os.replace is atomic on POSIX and on Windows (Python 3.3+).

2. Auto-backup before every save. We snapshot db.json into data/backups/
   with a UTC timestamp filename, then prune to the last 20. Cheap insurance.

When we outgrow JSON, we replace this file with a SQLite-backed implementation
that exposes the same methods. Nothing else in the app needs to change.
"""
import json
import os
import shutil
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import List, Optional

from models import Opportunity


class Store:
    def __init__(self, data_dir: Path):
        self.data_dir = Path(data_dir)
        self.db_path = self.data_dir / 'db.json'
        self.backup_dir = self.data_dir / 'backups'
        self.upload_dir = self.data_dir / 'uploads'

        for d in (self.data_dir, self.backup_dir, self.upload_dir):
            d.mkdir(parents=True, exist_ok=True)

        if not self.db_path.exists():
            self._write_raw({'opportunities': []})

    # ---- low-level I/O -------------------------------------------------

    def _read_raw(self) -> dict:
        with open(self.db_path, 'r', encoding='utf-8') as f:
            return json.load(f)

    def _write_raw(self, data: dict) -> None:
        # Write to a temp file in the same directory, then atomically rename.
        # Same directory matters: os.replace is only atomic across the same
        # filesystem, and tempfile.mkstemp's default temp dir might be elsewhere.
        tmp_fd, tmp_path = tempfile.mkstemp(
            dir=self.data_dir, prefix='.db.', suffix='.tmp'
        )
        try:
            with os.fdopen(tmp_fd, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2, sort_keys=True, ensure_ascii=False)
            os.replace(tmp_path, self.db_path)
        except Exception:
            # Clean up the temp file if anything went wrong
            try:
                os.unlink(tmp_path)
            except OSError:
                pass
            raise

    def _backup(self) -> None:
        """Snapshot current db.json. Keeps the 20 most recent."""
        if not self.db_path.exists():
            return
        ts = datetime.now(timezone.utc).strftime('%Y%m%d_%H%M%S_%f')
        target = self.backup_dir / f'db_{ts}.json'
        shutil.copy2(self.db_path, target)

        # prune oldest
        backups = sorted(self.backup_dir.glob('db_*.json'))
        for old in backups[:-20]:
            try:
                old.unlink()
            except OSError:
                pass

    # ---- public API ----------------------------------------------------

    def all(self, include_archived: bool = False) -> List[Opportunity]:
        data = self._read_raw()
        opps = [Opportunity.from_dict(o) for o in data.get('opportunities', [])]
        if not include_archived:
            opps = [o for o in opps if not o.archived]
        return opps

    def get(self, opp_id: str) -> Optional[Opportunity]:
        for o in self.all(include_archived=True):
            if o.id == opp_id:
                return o
        return None

    def save(self, opp: Opportunity) -> None:
        """Insert or update. Bumps updated_at."""
        self._backup()
        data = self._read_raw()
        opp.updated_at = datetime.now(timezone.utc).isoformat()

        opps = data.get('opportunities', [])
        for i, existing in enumerate(opps):
            if existing['id'] == opp.id:
                opps[i] = opp.to_dict()
                break
        else:
            opps.append(opp.to_dict())

        data['opportunities'] = opps
        self._write_raw(data)

    def archive(self, opp_id: str) -> None:
        opp = self.get(opp_id)
        if opp:
            opp.archived = True
            opp.archived_at = datetime.now(timezone.utc).isoformat()
            self.save(opp)

    def restore(self, opp_id: str) -> None:
        opp = self.get(opp_id)
        if opp:
            opp.archived = False
            opp.archived_at = ''
            self.save(opp)

    # ---- snooze --------------------------------------------------------

    def set_snooze(self, opp_id: str, until_iso: str) -> None:
        """Set or clear the snooze. Pass empty string to wake."""
        opp = self.get(opp_id)
        if opp:
            opp.snoozed_until = until_iso
            self.save(opp)

    # ---- tags ----------------------------------------------------------

    def set_tags(self, opp_id: str, tags: list) -> None:
        """Replace the manual-tag list. Tags are normalized: lowercased,
        stripped, deduplicated, and limited to alnum + dash + underscore."""
        opp = self.get(opp_id)
        if not opp:
            return
        clean = []
        seen = set()
        for t in tags:
            t = (t or '').strip().lower()
            # keep it tidy: alphanumerics, dash, underscore, no spaces
            t = ''.join(c for c in t if c.isalnum() or c in '-_')
            if t and t not in seen:
                seen.add(t)
                clean.append(t)
        opp.tags = clean
        self.save(opp)

    # ---- notes ---------------------------------------------------------

    def add_note(self, opp_id: str, note_type: str, body: str) -> Optional[dict]:
        """Append a timestamped note. Bumps last_contact_at — that's what
        the staleness tags in Phase 4 will key off of, so anything you
        log here counts as "I touched this opportunity"."""
        opp = self.get(opp_id)
        if not opp:
            return None
        from models import Note, now_iso
        note = Note(type=note_type, body=body)
        note_dict = {
            'id': note.id,
            'ts': note.ts,
            'type': note.type,
            'body': note.body,
        }
        opp.notes.append(note_dict)
        opp.last_contact_at = now_iso()
        self.save(opp)
        return note_dict

    def delete_note(self, opp_id: str, note_id: str) -> bool:
        opp = self.get(opp_id)
        if not opp:
            return False
        before = len(opp.notes)
        opp.notes = [n for n in opp.notes if n.get('id') != note_id]
        if len(opp.notes) == before:
            return False
        self.save(opp)
        return True

    # ---- attachments ---------------------------------------------------

    def opp_upload_dir(self, opp_id: str) -> Path:
        """Per-opportunity upload directory. Created on demand."""
        d = self.upload_dir / opp_id
        d.mkdir(parents=True, exist_ok=True)
        return d

    def add_attachment(self, opp_id: str, attachment: dict) -> Optional[dict]:
        opp = self.get(opp_id)
        if not opp:
            return None
        opp.attachments.append(attachment)
        self.save(opp)
        return attachment

    def delete_attachment(self, opp_id: str, att_id: str) -> bool:
        """Remove the metadata entry AND the bytes on disk."""
        opp = self.get(opp_id)
        if not opp:
            return False
        target = None
        for a in opp.attachments:
            if a.get('id') == att_id:
                target = a
                break
        if not target:
            return False

        # Delete the file from disk before saving — if file deletion fails
        # we'd rather keep the metadata than orphan the bytes.
        # Both the original and any thumbnail.
        opp_dir = self.opp_upload_dir(opp_id)
        for fname in (target['filename'], f'thumb_{target["filename"]}'):
            fpath = opp_dir / fname
            if fpath.exists():
                try:
                    fpath.unlink()
                except OSError:
                    pass

        opp.attachments = [a for a in opp.attachments if a.get('id') != att_id]
        self.save(opp)
        return True
