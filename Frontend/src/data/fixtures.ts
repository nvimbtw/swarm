import type { Initiative, RequestState, WorkItem, WorkState } from '../lib/admin-state'

export type Harness = 'codex' | 'opencode' | 't3code'

export type CompanionPose =
  | 'generating'
  | 'request-sent'
  | 'request-received'
  | 'away'
  | 'completed'
  | 'offline'

export interface Session {
  id: string
  owner: string
  harness: Harness
  machine: string
  pose: CompanionPose
  connected: boolean
}

export interface Request {
  id: string
  title: string
  sessionId: string
  itemId: string
  state: RequestState
  delivery: 'queue' | 'inject'
  age: string
}

export interface HistoryEvent {
  id: string
  text: string
  detail: string
  age: string
  kind: 'request' | 'session' | 'permission' | 'state'
}

export const sessions: Session[] = [
  {
    id: 's1',
    owner: 'jan',
    harness: 'codex',
    machine: 'jan@framework',
    pose: 'generating',
    connected: true,
  },
  {
    id: 's2',
    owner: 'aleks',
    harness: 'opencode',
    machine: 'aleks@t14',
    pose: 'request-sent',
    connected: true,
  },
  {
    id: 's3',
    owner: 'tadej',
    harness: 'codex',
    machine: 'tadej@nixos',
    pose: 'request-received',
    connected: true,
  },
  {
    id: 's4',
    owner: 'jan',
    harness: 't3code',
    machine: 'jan@framework',
    pose: 'away',
    connected: true,
  },
  {
    id: 's5',
    owner: 'aleks',
    harness: 'codex',
    machine: 'aleks@t14',
    pose: 'completed',
    connected: true,
  },
  {
    id: 's6',
    owner: 'tadej',
    harness: 'opencode',
    machine: 'tadej@nixos',
    pose: 'offline',
    connected: false,
  },
]

export const requests: Request[] = [
  {
    id: 'req-415',
    title: 'Review the adapter fallback brief',
    sessionId: 's2',
    itemId: '409',
    state: 'waiting',
    delivery: 'queue',
    age: '2m',
  },
  {
    id: 'req-418',
    title: 'Close the handshake item after review',
    sessionId: 's3',
    itemId: '405',
    state: 'queued',
    delivery: 'queue',
    age: '6m',
  },
  {
    id: 'req-421',
    title: 'Answer the queue and inject question',
    sessionId: 's5',
    itemId: '391',
    state: 'waiting',
    delivery: 'inject',
    age: '18m',
  },
]

export const nowRequest: Request = {
  id: 'req-412',
  title: 'Inject a request into an existing session',
  sessionId: 's1',
  itemId: '412',
  state: 'in-progress',
  delivery: 'inject',
  age: '12s',
}

export const history: HistoryEvent[] = [
  { id: 'h1', text: 'Request completed', detail: '#409 — aleks / opencode', age: '14m', kind: 'request' },
  { id: 'h2', text: 'Session connected', detail: 'aleks / codex — aleks@t14', age: '22m', kind: 'session' },
  { id: 'h3', text: 'Request declined', detail: 'no permission — #408', age: '41m', kind: 'permission' },
  { id: 'h4', text: 'Moved to verification', detail: '#402 — jan', age: '1h', kind: 'state' },
  { id: 'h5', text: 'Session disconnected', detail: 'tadej / opencode', age: '2h', kind: 'session' },
  { id: 'h6', text: 'Request completed', detail: '#387 — tadej / codex', age: '3h', kind: 'request' },
  { id: 'h7', text: 'Moved to in progress', detail: '#421 — aleks', age: '4h', kind: 'state' },
  { id: 'h8', text: 'Session connected', detail: 'jan / t3code — jan@framework', age: '5h', kind: 'session' },
  { id: 'h9', text: 'Request queued', detail: '#398 — jan / codex', age: '6h', kind: 'request' },
  { id: 'h10', text: 'Request completed', detail: '#391 — aleks / codex', age: '8h', kind: 'request' },
  { id: 'h11', text: 'Session disconnected', detail: 'jan / codex', age: '9h', kind: 'session' },
  { id: 'h12', text: 'Moved to closed', detail: '#419 — tadej', age: '1d', kind: 'state' },
]

export const initiatives: Initiative[] = [
  { id: 'connector', name: 'Swarm connector' },
  { id: 'discord', name: 'Discord threads' },
  { id: 'dashboard', name: 'Dashboard' },
  { id: 'documentation', name: 'Documentation' },
]

const item = (
  id: string,
  title: string,
  initiativeId: string,
  state: WorkState,
  owner: string,
  sessionId: string | null,
): WorkItem => ({ id, title, initiativeId, state, owner, sessionId })

export const workItems: WorkItem[] = [
  item('412', 'Inject a request into an existing session', 'connector', 'in-progress', 'jan', 's1'),
  item('409', 'Adapter fallback testing', 'connector', 'verification', 'aleks', 's2'),
  item('405', 'Session registration and permissions', 'connector', 'in-progress', 'tadej', 's3'),
  item('398', 'Reconnect after the machine sleeps', 'connector', 'open', 'jan', 's4'),
  item('391', 'Queue and inject delivery modes', 'connector', 'open', 'aleks', 's5'),
  item('387', 'Authenticated WebSocket handshake', 'connector', 'closed', 'tadej', 's3'),

  item('417', 'Name the larger grouping', 'discord', 'open', 'aleks', 's2'),
  item('414', 'Publish decisions to #docs', 'discord', 'in-progress', 'jan', 's1'),
  item('408', 'Command surface for requests', 'discord', 'open', 'tadej', 's3'),
  item('402', 'Retained notifications', 'discord', 'verification', 'jan', 's4'),

  item('421', 'Admin queue and history', 'dashboard', 'in-progress', 'aleks', 's5'),
  item('420', 'Session presence and state', 'dashboard', 'open', 'jan', 's1'),
  item('419', 'Work item state words', 'dashboard', 'closed', 'tadej', 's3'),
  item('422', 'Pause control for the queue', 'dashboard', 'open', 'aleks', 's2'),
]

export const sessionById = (id: string) => sessions.find((session) => session.id === id)
export const itemById = (id: string) => workItems.find((item) => item.id === id)
