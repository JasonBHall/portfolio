import { useEffect, useRef } from 'react'
import { useGameStore } from '../../store/useGameStore'

export function ActionLog() {
  const { actionLog } = useGameStore()
  const bottomRef = useRef<HTMLDivElement>(null)

  // Auto-scroll to latest entry
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [actionLog])

  return (
    <div className="bg-gray-900 border border-gray-800 rounded-xl overflow-hidden flex flex-col h-64">
      <div className="px-4 py-2 border-b border-gray-800 shrink-0">
        <p className="text-gray-400 text-xs uppercase tracking-wide">Action Log</p>
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-2 space-y-1 font-mono text-xs">
        {actionLog.length === 0 ? (
          <p className="text-gray-600 text-center py-4">No actions yet.</p>
        ) : (
          actionLog.map((entry, i) => (
            <div key={i} className="flex gap-2 text-gray-400">
              <span className="text-gray-600 shrink-0">{entry.timestamp}</span>
              <span className={isDm(entry.userId) ? 'text-amber-400' : 'text-indigo-400'}>
                {entry.characterName}
              </span>
              <span className="text-gray-300">{entry.action}</span>
            </div>
          ))
        )}
        <div ref={bottomRef} />
      </div>
    </div>
  )
}

function isDm(userId: string) {
  return userId === 'dm'
}
