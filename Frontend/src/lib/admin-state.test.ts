import { describe, expect, it } from 'vitest'
import {
  adminReducer,
  groupWorkItems,
  initialAdminState,
  waitingCount,
  workCounts,
  type AdminState,
  type Initiative,
  type WorkItem,
} from './admin-state'

const initiatives: Initiative[] = [
  { id: 'connector', name: 'Swarm connector' },
  { id: 'docs', name: 'Documentation' },
]

const items: WorkItem[] = [
  {
    id: '412',
    title: 'Inject a request',
    initiativeId: 'connector',
    state: 'in-progress',
    owner: 'jan',
    sessionId: 's1',
  },
  {
    id: '409',
    title: 'Adapter fallback',
    initiativeId: 'connector',
    state: 'verification',
    owner: 'aleks',
    sessionId: 's2',
  },
  {
    id: '387',
    title: 'Handshake',
    initiativeId: 'connector',
    state: 'closed',
    owner: 'tadej',
    sessionId: 's3',
  },
]

const run = (state: AdminState, ...actions: Parameters<typeof adminReducer>[1][]) =>
  actions.reduce(adminReducer, state)

describe('adminReducer', () => {
  it('starts on the queue with nothing collapsed or selected', () => {
    expect(initialAdminState).toEqual({
      tab: 'queue',
      queuePaused: false,
      selectedItemId: null,
      collapsedGroupIds: [],
    })
  })

  it('switches between queue and history', () => {
    expect(run(initialAdminState, { type: 'set-tab', tab: 'history' }).tab).toBe('history')
    expect(
      run(initialAdminState, { type: 'set-tab', tab: 'history' }, { type: 'set-tab', tab: 'queue' })
        .tab,
    ).toBe('queue')
  })

  it('pauses and resumes the queue', () => {
    const paused = run(initialAdminState, { type: 'toggle-queue' })
    expect(paused.queuePaused).toBe(true)
    expect(adminReducer(paused, { type: 'toggle-queue' }).queuePaused).toBe(false)
  })

  it('keeps a single selected work item', () => {
    const first = run(initialAdminState, { type: 'select-item', id: '412' })
    const second = adminReducer(first, { type: 'select-item', id: '409' })
    expect(second.selectedItemId).toBe('409')
  })

  it('collapses and expands a group without touching the others', () => {
    const collapsed = run(initialAdminState, { type: 'toggle-group', id: 'connector' })
    expect(collapsed.collapsedGroupIds).toEqual(['connector'])
    expect(adminReducer(collapsed, { type: 'toggle-group', id: 'connector' }).collapsedGroupIds).toEqual(
      [],
    )
  })

  it('collapses all groups and expands them again', () => {
    const collapsed = run(initialAdminState, {
      type: 'set-groups-collapsed',
      ids: ['connector', 'docs'],
    })
    expect(collapsed.collapsedGroupIds).toEqual(['connector', 'docs'])
    expect(adminReducer(collapsed, { type: 'expand-all-groups' }).collapsedGroupIds).toEqual([])
  })
})

describe('groupWorkItems', () => {
  it('groups in initiative order and keeps empty groups', () => {
    const groups = groupWorkItems(initiatives, items, [])
    expect(groups.map((group) => group.id)).toEqual(['connector', 'docs'])
    expect(groups[0].items).toHaveLength(3)
    expect(groups[1].items).toEqual([])
  })

  it('keeps the count when a group is collapsed and hides its rows', () => {
    const groups = groupWorkItems(initiatives, items, ['connector'])
    expect(groups[0]).toMatchObject({ count: 3, collapsed: true, items: [] })
    expect(groups[1]).toMatchObject({ count: 0, collapsed: false })
  })

  it('does not mutate the item list', () => {
    const copy = structuredClone(items)
    groupWorkItems(initiatives, items, ['connector'])
    expect(items).toEqual(copy)
  })
})

describe('counts', () => {
  it('counts every item that is not closed as active', () => {
    expect(workCounts(items)).toEqual({ active: 2, open: 0, inProgress: 1, verification: 1, closed: 1 })
  })

  it('counts only requests waiting for a person', () => {
    expect(
      waitingCount([
        { state: 'waiting' },
        { state: 'queued' },
        { state: 'waiting' },
        { state: 'in-progress' },
      ]),
    ).toBe(2)
  })
})
