import { useState } from 'react'
import { useGameStore } from '../store/useGameStore'

export default function LoginView() {
  const { joinSession } = useGameStore()

  const [userId, setUserId] = useState('')
  const [characterName, setCharacterName] = useState('')
  const [isDm, setIsDm] = useState(false)
  const [joining, setJoining] = useState(false)
  const [error, setError] = useState('')

  const handleJoin = async () => {
    if (!userId.trim()) {
      setError('Enter a user ID.')
      return
    }
    setError('')
    setJoining(true)
    try {
      await joinSession(userId.trim(), characterName.trim(), isDm)
    } catch (err) {
      setError('Failed to join. Is the server running?')
      setJoining(false)
    }
  }

  const handleKey = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') handleJoin()
  }

  return (
    <div className="min-h-screen flex items-center justify-center px-4">
      <div className="w-full max-w-sm">

        {/* Title */}
        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold text-gray-100 tracking-tight">
            Dungeon Runner
          </h1>
          <p className="text-gray-500 text-sm mt-1">Resource tracker for tabletop sessions</p>
        </div>

        {/* Card */}
        <div className="bg-gray-900 border border-gray-800 rounded-xl p-6 space-y-4">

          <div>
            <label className="block text-xs text-gray-400 uppercase tracking-wide mb-1">
              User ID
            </label>
            <input
              type="text"
              placeholder="e.g. dave"
              value={userId}
              onChange={e => setUserId(e.target.value)}
              onKeyDown={handleKey}
              className="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2
                         text-gray-100 text-sm placeholder-gray-600
                         focus:outline-none focus:border-indigo-500 transition-colors"
              autoFocus
            />
            <p className="text-gray-600 text-xs mt-1">
              Lowercase, no spaces. This identifies your character file.
            </p>
          </div>

          {!isDm && (
            <div>
              <label className="block text-xs text-gray-400 uppercase tracking-wide mb-1">
                Character Name
              </label>
              <input
                type="text"
                placeholder="e.g. Aldric Stonefist"
                value={characterName}
                onChange={e => setCharacterName(e.target.value)}
                onKeyDown={handleKey}
                className="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2
                           text-gray-100 text-sm placeholder-gray-600
                           focus:outline-none focus:border-indigo-500 transition-colors"
              />
              <p className="text-gray-600 text-xs mt-1">
                Only used the first time you join. Leave blank to use your user ID.
              </p>
            </div>
          )}

          <label className="flex items-center gap-3 cursor-pointer select-none">
            <div
              onClick={() => setIsDm(v => !v)}
              className={`relative w-9 h-5 rounded-full transition-colors ${
                isDm ? 'bg-indigo-600' : 'bg-gray-700'
              }`}
            >
              <div
                className={`absolute top-0.5 left-0.5 w-4 h-4 rounded-full bg-white
                            transition-transform ${isDm ? 'translate-x-4' : 'translate-x-0'}`}
              />
            </div>
            <span className="text-gray-300 text-sm">Join as Dungeon Master</span>
          </label>

          {error && (
            <p className="text-red-400 text-sm">{error}</p>
          )}

          <button
            onClick={handleJoin}
            disabled={joining || !userId.trim()}
            className="w-full bg-indigo-600 hover:bg-indigo-700 disabled:opacity-40
                       disabled:cursor-not-allowed text-white font-medium rounded-lg
                       py-2.5 text-sm transition-colors"
          >
            {joining ? 'Joining...' : isDm ? 'Enter as DM' : 'Enter as Player'}
          </button>
        </div>
      </div>
    </div>
  )
}
