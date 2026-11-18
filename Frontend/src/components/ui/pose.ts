import type { CompanionPose } from '../../data/fixtures'

export const POSE_LABEL: Record<CompanionPose, string> = {
  generating: 'generating',
  'request-sent': 'request sent',
  'request-received': 'request received',
  away: 'away',
  completed: 'completed',
  offline: 'offline',
}

export const POSE_TONE: Record<CompanionPose, string> = {
  generating: 'text-progress',
  'request-sent': 'text-queued',
  'request-received': 'text-queued',
  away: 'text-ink-muted',
  completed: 'text-closed',
  offline: 'text-ink-muted',
}
