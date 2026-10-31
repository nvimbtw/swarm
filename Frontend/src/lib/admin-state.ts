export type AdminTab = 'queue' | 'history'

export type WorkState = 'open' | 'in-progress' | 'verification' | 'closed'

export type RequestState = 'queued' | 'waiting' | 'in-progress'

export interface Initiative {
  id: string
  name: string
}

export interface WorkItem {
  id: string
  title: string
  initiativeId: string
  state: WorkState
  owner: string
  sessionId: string | null
}

export interface WorkGroup {
  id: string
  name: string
  count: number
  collapsed: boolean
  items: WorkItem[]
}

export interface AdminState {
  tab: AdminTab
  queuePaused: boolean
  selectedItemId: string | null
  collapsedGroupIds: string[]
}

export type AdminAction =
  | { type: 'set-tab'; tab: AdminTab }
  | { type: 'toggle-queue' }
  | { type: 'select-item'; id: string }
  | { type: 'toggle-group'; id: string }
  | { type: 'set-groups-collapsed'; ids: string[] }
  | { type: 'expand-all-groups' }

export const initialAdminState: AdminState = {
  tab: 'queue',
  queuePaused: false,
  selectedItemId: null,
  collapsedGroupIds: [],
}

export function adminReducer(state: AdminState, action: AdminAction): AdminState {
  switch (action.type) {
    case 'set-tab':
      return { ...state, tab: action.tab }
    case 'toggle-queue':
      return { ...state, queuePaused: !state.queuePaused }
    case 'select-item':
      return { ...state, selectedItemId: action.id }
    case 'toggle-group': {
      const collapsed = state.collapsedGroupIds.includes(action.id)
      return {
        ...state,
        collapsedGroupIds: collapsed
          ? state.collapsedGroupIds.filter((id) => id !== action.id)
          : [...state.collapsedGroupIds, action.id],
      }
    }
    case 'set-groups-collapsed':
      return { ...state, collapsedGroupIds: action.ids }
    case 'expand-all-groups':
      return { ...state, collapsedGroupIds: [] }
    default:
      return state
  }
}

export function groupWorkItems(
  initiatives: Initiative[],
  items: WorkItem[],
  collapsedGroupIds: string[],
): WorkGroup[] {
  return initiatives.map((initiative) => {
    const groupItems = items.filter((item) => item.initiativeId === initiative.id)
    const collapsed = collapsedGroupIds.includes(initiative.id)
    return {
      id: initiative.id,
      name: initiative.name,
      count: groupItems.length,
      collapsed,
      items: collapsed ? [] : groupItems,
    }
  })
}

export interface WorkCounts {
  active: number
  open: number
  inProgress: number
  verification: number
  closed: number
}

export function workCounts(items: WorkItem[]): WorkCounts {
  return {
    active: items.filter((item) => item.state !== 'closed').length,
    open: items.filter((item) => item.state === 'open').length,
    inProgress: items.filter((item) => item.state === 'in-progress').length,
    verification: items.filter((item) => item.state === 'verification').length,
    closed: items.filter((item) => item.state === 'closed').length,
  }
}

export function waitingCount(requests: Array<{ state: RequestState }>): number {
  return requests.filter((request) => request.state === 'waiting').length
}
