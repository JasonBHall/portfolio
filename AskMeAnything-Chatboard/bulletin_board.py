#!/usr/bin/env python3
"""
Ask Me Anything - Anonymous Bulletin Board (Proof of Concept)
------------------------------------------------------
Requires:  pip install flask
Run:       python bulletin_board.py
Board:     http://localhost:5000
Login:     http://localhost:5000/user
Admin:     http://localhost:5000/admin
"""

from flask import Flask, request, jsonify, session
import json, os, random, string
from datetime import datetime

app = Flask(__name__)
app.secret_key = "dev-secret-boardit-poc"
DATA_FILE = "board_data.json"


# ── Helpers ──────────────────────────────────────────────────────────────────

def rand_id(n=12):
    return "".join(random.choices(string.ascii_letters + string.digits, k=n))

DEFAULT_DATA = {
    "users": [
        {"id": "u1", "username": "Alice", "anon_id": "Xk9mP2nQ8rTs"},
        {"id": "u2", "username": "Bob",   "anon_id": "Ht5jW7cR3vLq"},
        {"id": "u3", "username": "Carol", "anon_id": "Mn4bY6dF1xZp"},
        {"id": "u4", "username": "Dave",  "anon_id": "Gs2eA0kU9wJo"},
    ],
    "topics": [
        {"id": "politics",   "name": "Politics",   "approved": True,  "created": "2024-01-01"},
        {"id": "baking",     "name": "Baking",     "approved": True,  "created": "2024-01-01"},
        {"id": "autorepair", "name": "Auto Repair","approved": True,  "created": "2024-01-01"},
    ],
    "posts": [],
    "votes": {}
}

def load():
    if not os.path.exists(DATA_FILE):
        save(DEFAULT_DATA)
        return DEFAULT_DATA
    with open(DATA_FILE) as f:
        return json.load(f)

def save(data):
    with open(DATA_FILE, "w") as f:
        json.dump(data, f, indent=2)


# ── API: Auth ─────────────────────────────────────────────────────────────────

@app.route("/api/me")
def me():
    uid = session.get("user_id")
    if not uid:
        return jsonify(None)
    d = load()
    return jsonify(next((u for u in d["users"] if u["id"] == uid), None))

@app.route("/api/login", methods=["POST"])
def login():
    uid = request.json.get("user_id")
    d = load()
    user = next((u for u in d["users"] if u["id"] == uid), None)
    if not user:
        return jsonify({"error": "not found"}), 404
    session["user_id"] = uid
    return jsonify(user)

@app.route("/api/logout", methods=["POST"])
def logout():
    session.clear()
    return jsonify({"ok": True})


# ── API: Topics ───────────────────────────────────────────────────────────────

@app.route("/api/topics")
def get_topics():
    d = load()
    return jsonify([t for t in d["topics"] if t["approved"]])

@app.route("/api/topics/all")
def get_all_topics():
    return jsonify(load()["topics"])

@app.route("/api/topics", methods=["POST"])
def create_topic():
    name = (request.json or {}).get("name", "").strip()
    if not name:
        return jsonify({"error": "name required"}), 400
    d = load()
    tid = name.lower().replace(" ", "_")
    if any(t["id"] == tid for t in d["topics"]):
        return jsonify({"error": "already exists"}), 409
    topic = {"id": tid, "name": name, "approved": False, "created": datetime.now().isoformat()}
    d["topics"].append(topic)
    save(d)
    return jsonify(topic), 201

@app.route("/api/topics/<tid>/approve", methods=["POST"])
def approve_topic(tid):
    d = load()
    topic = next((t for t in d["topics"] if t["id"] == tid), None)
    if not topic:
        return jsonify({"error": "not found"}), 404
    topic["approved"] = True
    save(d)
    return jsonify(topic)

@app.route("/api/topics/<tid>/reject", methods=["POST"])
def reject_topic(tid):
    d = load()
    d["topics"] = [t for t in d["topics"] if t["id"] != tid]
    save(d)
    return jsonify({"ok": True})


# ── API: Posts ────────────────────────────────────────────────────────────────

@app.route("/api/posts/<tid>")
def get_posts(tid):
    d = load()
    posts = sorted(
        [p for p in d["posts"] if p["topic_id"] == tid],
        key=lambda p: p["created"], reverse=True
    )
    return jsonify(posts)

@app.route("/api/posts", methods=["POST"])
def create_post():
    uid = session.get("user_id")
    if not uid:
        return jsonify({"error": "not logged in"}), 401
    d = load()
    user = next((u for u in d["users"] if u["id"] == uid), None)
    body = request.json or {}
    post = {
        "id": rand_id(8),
        "topic_id": body.get("topic_id"),
        "title": body.get("title", "").strip(),
        "content": body.get("content", "").strip(),
        "anon_id": user["anon_id"],
        "user_id": uid,
        "upvotes": 0, "downvotes": 0,
        "created": datetime.now().isoformat(),
        "answers": []
    }
    if not post["title"]:
        return jsonify({"error": "title required"}), 400
    d["posts"].append(post)
    save(d)
    return jsonify(post), 201

def apply_vote(d, target, key_prefix, uid, direction):
    """Toggle-aware vote. Mutates target dict, updates d['votes']."""
    if "votes" not in d:
        d["votes"] = {}
    key = f"{key_prefix}_{uid}"
    prev = d["votes"].get(key)
    if prev == direction:          # undo vote
        d["votes"].pop(key)
        if direction == "up":   target["upvotes"]   = max(0, target["upvotes"]   - 1)
        else:                   target["downvotes"] = max(0, target["downvotes"] - 1)
    else:
        if prev == "up":        target["upvotes"]   = max(0, target["upvotes"]   - 1)
        elif prev == "down":    target["downvotes"] = max(0, target["downvotes"] - 1)
        d["votes"][key] = direction
        if direction == "up":   target["upvotes"]   += 1
        else:                   target["downvotes"] += 1

@app.route("/api/posts/<pid>/vote", methods=["POST"])
def vote_post(pid):
    uid = session.get("user_id")
    if not uid:
        return jsonify({"error": "not logged in"}), 401
    d = load()
    post = next((p for p in d["posts"] if p["id"] == pid), None)
    if not post:
        return jsonify({"error": "not found"}), 404
    apply_vote(d, post, pid, uid, request.json.get("direction"))
    save(d)
    return jsonify(post)

@app.route("/api/posts/<pid>/answers", methods=["POST"])
def add_answer(pid):
    uid = session.get("user_id")
    if not uid:
        return jsonify({"error": "not logged in"}), 401
    d = load()
    post = next((p for p in d["posts"] if p["id"] == pid), None)
    if not post:
        return jsonify({"error": "not found"}), 404
    user = next((u for u in d["users"] if u["id"] == uid), None)
    answer = {
        "id": rand_id(8),
        "content": (request.json or {}).get("content", "").strip(),
        "anon_id": user["anon_id"],
        "user_id": uid,
        "upvotes": 0, "downvotes": 0,
        "created": datetime.now().isoformat()
    }
    if not answer["content"]:
        return jsonify({"error": "content required"}), 400
    post["answers"].append(answer)
    save(d)
    return jsonify(answer), 201

@app.route("/api/posts/<pid>/answers/<aid>/vote", methods=["POST"])
def vote_answer(pid, aid):
    uid = session.get("user_id")
    if not uid:
        return jsonify({"error": "not logged in"}), 401
    d = load()
    post = next((p for p in d["posts"] if p["id"] == pid), None)
    if not post:
        return jsonify({"error": "not found"}), 404
    answer = next((a for a in post["answers"] if a["id"] == aid), None)
    if not answer:
        return jsonify({"error": "not found"}), 404
    apply_vote(d, answer, aid, uid, request.json.get("direction"))
    save(d)
    return jsonify(answer)


# ── HTML (React SPA) ──────────────────────────────────────────────────────────

HTML = r"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Ask Me Anything</title>
<script src="https://unpkg.com/react@18/umd/react.development.js"></script>
<script src="https://unpkg.com/react-dom@18/umd/react-dom.development.js"></script>
<script src="https://unpkg.com/@babel/standalone/babel.min.js"></script>
<script src="https://cdn.tailwindcss.com"></script>
</head>
<body class="bg-gray-100 min-h-screen">
<div id="root"></div>
<script type="text/babel">
const { useState, useEffect } = React;

const api  = (p, o={}) => fetch(p, {headers:{"Content-Type":"application/json"}, credentials:"include", ...o}).then(r=>r.json());
const post = (p, b)    => api(p, {method:"POST", body:JSON.stringify(b)});

// ─── Vote Buttons ────────────────────────────────────────────────────────────
function VoteBar({ up, down, onVote }) {
  return (
    <div className="flex items-center gap-1 shrink-0">
      <button onClick={()=>onVote("up")}
        className="flex items-center gap-1 px-2 py-1 bg-green-50 hover:bg-green-100 border border-green-200 rounded text-xs font-medium text-green-700 transition-colors">
        ▲ {up}
      </button>
      <button onClick={()=>onVote("down")}
        className="flex items-center gap-1 px-2 py-1 bg-red-50 hover:bg-red-100 border border-red-200 rounded text-xs font-medium text-red-700 transition-colors">
        ▼ {down}
      </button>
    </div>
  );
}

// ─── Answer Row ──────────────────────────────────────────────────────────────
function AnswerRow({ a, pid, user, onChange }) {
  const [data, setData] = useState(a);
  const vote = async dir => {
    const updated = await post(`/api/posts/${pid}/answers/${a.id}/vote`, {direction:dir});
    setData(updated);
    if (onChange) onChange(updated);
  };
  return (
    <div className="flex justify-between items-start gap-3 py-2">
      <div className="flex-1">
        <p className="text-sm text-gray-700">{data.content}</p>
        <p className="text-xs text-gray-400 mt-0.5">
          by <span className="font-mono bg-gray-100 px-1 rounded text-gray-500">{data.anon_id}</span>
          {" · "}{new Date(data.created).toLocaleDateString()}
        </p>
      </div>
      {user && <VoteBar up={data.upvotes} down={data.downvotes} onVote={vote} />}
    </div>
  );
}

// ─── Post Card ───────────────────────────────────────────────────────────────
function PostCard({ p, user }) {
  const [data,      setData]      = useState(p);
  const [answers,   setAnswers]   = useState(p.answers || []);
  const [showForm,  setShowForm]  = useState(false);
  const [answerTxt, setAnswerTxt] = useState("");
  const [expanded,  setExpanded]  = useState(false);

  const votePost = async dir => setData(await post(`/api/posts/${p.id}/vote`, {direction:dir}));

  const submitAnswer = async () => {
    if (!answerTxt.trim()) return;
    const a = await post(`/api/posts/${p.id}/answers`, {content: answerTxt});
    setAnswers([...answers, a]);
    setAnswerTxt(""); setShowForm(false); setExpanded(true);
  };

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-4 mb-3">
      {/* Post header */}
      <div className="flex justify-between items-start gap-3">
        <div className="flex-1 min-w-0">
          <h3 className="font-semibold text-gray-900 leading-snug">{data.title}</h3>
          {data.content && <p className="text-sm text-gray-600 mt-1">{data.content}</p>}
          <p className="text-xs text-gray-400 mt-1.5">
            by <span className="font-mono bg-gray-100 px-1 rounded text-gray-500">{data.anon_id}</span>
            {" · "}{new Date(data.created).toLocaleDateString()}
            {answers.length > 0 &&
              <button onClick={()=>setExpanded(!expanded)}
                className="ml-2 text-blue-400 hover:underline">
                {expanded ? "▲ hide" : `▼ ${answers.length} answer${answers.length!==1?"s":""}`}
              </button>
            }
          </p>
        </div>
        {user && <VoteBar up={data.upvotes} down={data.downvotes} onVote={votePost} />}
      </div>

      {/* Answers */}
      {expanded && answers.length > 0 && (
        <div className="mt-3 pl-3 border-l-2 border-gray-100 divide-y divide-gray-50">
          {answers.map(a => <AnswerRow key={a.id} a={a} pid={p.id} user={user} />)}
        </div>
      )}

      {/* Answer form */}
      {user && (
        <div className="mt-3">
          {showForm ? (
            <div className="flex gap-2 items-center">
              <input autoFocus
                className="flex-1 border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-300"
                placeholder="Write your answer..."
                value={answerTxt}
                onChange={e=>setAnswerTxt(e.target.value)}
                onKeyDown={e=>e.key==="Enter" && submitAnswer()} />
              <button onClick={submitAnswer}
                className="bg-blue-600 hover:bg-blue-700 text-white px-3 py-1.5 rounded-lg text-sm transition-colors">
                Post
              </button>
              <button onClick={()=>setShowForm(false)}
                className="text-gray-400 hover:text-gray-600 text-sm px-1">✕</button>
            </div>
          ) : (
            <button onClick={()=>setShowForm(true)}
              className="text-sm text-blue-500 hover:text-blue-700 hover:underline">
              + Answer
            </button>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Topic View ───────────────────────────────────────────────────────────────
function TopicView({ topic, user }) {
  const [posts,   setPosts]   = useState([]);
  const [title,   setTitle]   = useState("");
  const [content, setContent] = useState("");
  const [showNew, setShowNew] = useState(false);

  useEffect(() => {
    api(`/api/posts/${topic.id}`).then(setPosts);
  }, [topic.id]);

  const submitPost = async () => {
    if (!title.trim()) return;
    const p = await post("/api/posts", {topic_id: topic.id, title, content});
    if (p.error) { alert(p.error); return; }
    setPosts([p, ...posts]);
    setTitle(""); setContent(""); setShowNew(false);
  };

  return (
    <div>
      <div className="flex justify-between items-center mb-4">
        <div>
          <h2 className="text-xl font-bold text-gray-800">
            <span className="text-gray-400">/</span>{topic.id}
          </h2>
          <p className="text-xs text-gray-400">{topic.name}</p>
        </div>
        {user && (
          <button onClick={()=>setShowNew(!showNew)}
            className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors">
            + New Post
          </button>
        )}
      </div>

      {showNew && (
        <div className="bg-white rounded-xl shadow-sm border border-blue-200 p-4 mb-4">
          <input
            className="w-full border border-gray-300 rounded-lg px-3 py-2 mb-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-300"
            placeholder="Post title or question..."
            value={title} onChange={e=>setTitle(e.target.value)} />
          <textarea
            className="w-full border border-gray-300 rounded-lg px-3 py-2 mb-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-300"
            rows="3" placeholder="Details (optional)..."
            value={content} onChange={e=>setContent(e.target.value)} />
          <div className="flex gap-2">
            <button onClick={submitPost}
              className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors">
              Post
            </button>
            <button onClick={()=>setShowNew(false)}
              className="text-gray-500 hover:text-gray-700 text-sm px-3 py-2">Cancel</button>
          </div>
        </div>
      )}

      {!user && (
        <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-3 mb-4 text-sm text-blue-600">
          <a href="/user" className="font-medium hover:underline">Log in</a> to post or vote.
        </div>
      )}

      {posts.length === 0 && (
        <div className="text-center text-gray-400 py-12">
          <p className="text-4xl mb-2">💬</p>
          <p>No posts yet. Be the first!</p>
        </div>
      )}

      {posts.map(p => <PostCard key={p.id} p={p} user={user} />)}
    </div>
  );
}

// ─── Request Topic Modal ──────────────────────────────────────────────────────
function RequestTopicModal({ onClose, onCreated }) {
  const [name, setName] = useState("");
  const submit = async () => {
    if (!name.trim()) return;
    const t = await post("/api/topics", {name});
    if (t.error) { alert(t.error); return; }
    onCreated(t);
    onClose();
  };
  return (
    <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-50">
      <div className="bg-white rounded-xl shadow-xl p-6 w-80">
        <h3 className="font-bold text-lg mb-1">Request a Topic</h3>
        <p className="text-xs text-gray-400 mb-4">An admin will review and approve it.</p>
        <input autoFocus
          className="w-full border border-gray-300 rounded-lg px-3 py-2 mb-3 text-sm focus:outline-none focus:ring-2 focus:ring-blue-300"
          placeholder="Topic name (e.g. Gardening)"
          value={name} onChange={e=>setName(e.target.value)}
          onKeyDown={e=>e.key==="Enter" && submit()} />
        <div className="flex gap-2">
          <button onClick={submit}
            className="flex-1 bg-blue-600 hover:bg-blue-700 text-white py-2 rounded-lg text-sm font-medium">
            Submit
          </button>
          <button onClick={onClose}
            className="flex-1 border border-gray-300 hover:bg-gray-50 text-gray-600 py-2 rounded-lg text-sm">
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}

// ─── User Login Page ──────────────────────────────────────────────────────────
function UserLoginPage() {
  const users = [
    {id:"u1", name:"Alice", color:"bg-purple-100 border-purple-300 hover:border-purple-500 hover:bg-purple-50"},
    {id:"u2", name:"Bob",   color:"bg-blue-100   border-blue-300   hover:border-blue-500   hover:bg-blue-50"},
    {id:"u3", name:"Carol", color:"bg-green-100  border-green-300  hover:border-green-500  hover:bg-green-50"},
    {id:"u4", name:"Dave",  color:"bg-orange-100 border-orange-300 hover:border-orange-500 hover:bg-orange-50"},
  ];
  const login = async uid => {
    const u = await post("/api/login", {user_id: uid});
    if (!u.error) window.location.href = "/7f6s4kh398nt";
  };
  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 to-gray-100">
      <div className="bg-white rounded-2xl shadow-lg p-8 w-80">
        <div className="text-center mb-6">
          <p className="text-3xl mb-1">📋</p>
          <h2 className="text-xl font-bold text-gray-800">Ask Me Anything</h2>
          <p className="text-sm text-gray-400 mt-1">Choose a demo user to continue</p>
        </div>
        <div className="space-y-3">
          {users.map(u => (
            <button key={u.id} onClick={()=>login(u.id)}
              className={`w-full py-3 rounded-xl border-2 font-medium text-gray-700 transition-all ${u.color}`}>
              {u.name}
            </button>
          ))}
        </div>
        <p className="text-xs text-gray-400 text-center mt-4">
          Posts will show an anonymous ID, not your name.
        </p>
      </div>
    </div>
  );
}

// ─── Admin Page ───────────────────────────────────────────────────────────────
function AdminPage() {
  const [topics, setTopics] = useState([]);

  useEffect(() => { api("/api/topics/all").then(setTopics); }, []);

  const approve = async tid => {
    await post(`/api/topics/${tid}/approve`, {});
    setTopics(ts => ts.map(t => t.id===tid ? {...t, approved:true} : t));
  };
  const reject = async tid => {
    await post(`/api/topics/${tid}/reject`, {});
    setTopics(ts => ts.filter(t => t.id!==tid));
  };

  const pending  = topics.filter(t => !t.approved);
  const approved = topics.filter(t =>  t.approved);

  return (
    <div className="max-w-2xl mx-auto px-4 py-8">
      <div className="flex justify-between items-center mb-8">
        <div>
          <h1 className="text-2xl font-bold text-gray-800">🔧 Admin Panel</h1>
          <p className="text-sm text-gray-400">Manage topics and content</p>
        </div>
        <a href="/" className="text-blue-500 text-sm hover:underline">← Back to board</a>
      </div>

      {/* Pending */}
      <section className="mb-8">
        <h2 className="font-semibold text-gray-700 mb-3 flex items-center gap-2">
          Pending Approval
          {pending.length > 0 && (
            <span className="bg-orange-100 text-orange-600 text-xs font-bold px-2 py-0.5 rounded-full">
              {pending.length}
            </span>
          )}
        </h2>
        {pending.length === 0 ? (
          <div className="bg-white rounded-xl border border-gray-200 p-6 text-center text-gray-400 text-sm">
            Nothing pending — all clear ✓
          </div>
        ) : (
          <div className="space-y-2">
            {pending.map(t => (
              <div key={t.id} className="bg-white rounded-xl border border-orange-200 p-4 flex justify-between items-center">
                <div>
                  <span className="font-mono font-medium text-gray-700">/{t.id}</span>
                  <span className="ml-2 text-sm text-gray-400">{t.name}</span>
                </div>
                <div className="flex gap-2">
                  <button onClick={()=>approve(t.id)}
                    className="bg-green-500 hover:bg-green-600 text-white px-3 py-1.5 rounded-lg text-sm font-medium transition-colors">
                    ✓ Approve
                  </button>
                  <button onClick={()=>reject(t.id)}
                    className="bg-red-400 hover:bg-red-500 text-white px-3 py-1.5 rounded-lg text-sm font-medium transition-colors">
                    ✗ Reject
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* Live */}
      <section>
        <h2 className="font-semibold text-gray-700 mb-3">
          Live Topics ({approved.length})
        </h2>
        <div className="space-y-2">
          {approved.map(t => (
            <div key={t.id} className="bg-white rounded-xl border border-gray-200 p-4 flex justify-between items-center">
              <div className="flex items-center gap-2">
                <span className="text-green-500 text-sm">●</span>
                <span className="font-mono font-medium text-gray-700">/{t.id}</span>
                <span className="text-sm text-gray-400">{t.name}</span>
              </div>
              <button onClick={()=>reject(t.id)}
                className="text-red-400 hover:text-red-600 text-sm hover:underline transition-colors">
                Remove
              </button>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}

// ─── Main Board ───────────────────────────────────────────────────────────────
function BoardPage() {
  const [user,         setUser]         = useState(undefined);
  const [topics,       setTopics]       = useState([]);
  const [activeTopic,  setActiveTopic]  = useState(null);
  const [showRequest,  setShowRequest]  = useState(false);

  useEffect(() => {
    api("/api/me").then(setUser);
    api("/api/topics").then(ts => { setTopics(ts); if (ts.length) setActiveTopic(ts[0]); });
  }, []);

  const doLogout = async () => {
    await post("/api/logout", {});
    setUser(null);
    window.location.href = "/";
  };

  return (
    <div className="max-w-5xl mx-auto px-4 py-6">
      {/* Header */}
      <header className="flex justify-between items-center mb-6 pb-4 border-b border-gray-200">
        <div>
          <h1 className="text-2xl font-bold text-blue-600 tracking-tight">📋 Ask Me Anything</h1>
          <p className="text-xs text-gray-400">anonymous community Q&amp;A</p>
        </div>
        <div className="flex items-center gap-3 text-sm">
          {user === undefined ? null : user ? (
            <div className="flex items-center gap-3">
              <span className="text-gray-600">
                <strong>{user.username}</strong>
                <span className="text-gray-400 font-mono text-xs ml-2">({user.anon_id})</span>
              </span>
              <button onClick={doLogout}
                className="text-red-400 hover:text-red-600 text-xs border border-red-200 hover:border-red-400 px-2 py-1 rounded transition-colors">
                Logout
              </button>
            </div>
          ) : (
            <a href="/user"
              className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg font-medium transition-colors">
              Login
            </a>
          )}
          <a href="/admin" className="text-gray-300 hover:text-gray-500 text-xs">Admin</a>
        </div>
      </header>

      <div className="flex gap-6">
        {/* Sidebar */}
        <aside className="w-48 shrink-0">
          <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Topics</p>
          <nav className="space-y-1">
            {topics.map(t => (
              <button key={t.id} onClick={()=>setActiveTopic(t)}
                className={`w-full text-left px-3 py-2 rounded-lg text-sm font-medium transition-colors
                  ${activeTopic?.id===t.id
                    ? "bg-blue-600 text-white shadow-sm"
                    : "text-gray-600 hover:bg-gray-200"}`}>
                <span className="text-gray-300">/</span>{t.id}
              </button>
            ))}
          </nav>
          {user && (
            <button onClick={()=>setShowRequest(true)}
              className="mt-4 w-full text-left px-3 py-2 rounded-lg text-xs text-gray-400 hover:text-blue-500 hover:bg-blue-50 border border-dashed border-gray-300 hover:border-blue-300 transition-colors">
              + Request topic
            </button>
          )}
        </aside>

        {/* Content */}
        <main className="flex-1 min-w-0">
          {activeTopic
            ? <TopicView key={activeTopic.id} topic={activeTopic} user={user} />
            : <div className="text-center text-gray-400 py-16">Loading...</div>
          }
        </main>
      </div>

      {showRequest && (
        <RequestTopicModal
          onClose={()=>setShowRequest(false)}
          onCreated={t => {/* pending, won't appear in sidebar until approved */}}
        />
      )}
    </div>
  );
}

// ─── Router ───────────────────────────────────────────────────────────────────
function App() {
  const path = window.location.pathname;
  if (path === "/admin") return <AdminPage />;
  if (path === "/user")  return <UserLoginPage />;
  return <BoardPage />;   // "/" and "/7f6s4kh398nt"
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);
</script>
</body>
</html>"""


# ── Page routes ───────────────────────────────────────────────────────────────

@app.route("/")
@app.route("/user")
@app.route("/7f6s4kh398nt")
@app.route("/admin")
def index():
    return HTML


# ── Bootstrap & run ───────────────────────────────────────────────────────────

if __name__ == "__main__":
    if not os.path.exists(DATA_FILE):
        save(DEFAULT_DATA)
        print(f"✓ Created {DATA_FILE} with default data")

    print("\n╔══════════════════════════════════════╗")
    print("║     Ask Me Anything  ·  POC v0.1     ║")
    print("╠══════════════════════════════════════╣")
    print("║  Board  →  http://localhost:5000     ║")
    print("║  Login  →  http://localhost:5000/user║")
    print("║  Admin  →  http://localhost:5000/admin║")
    print("╚══════════════════════════════════════╝\n")

    app.run(debug=True, host="0.0.0.0", port=5000)
