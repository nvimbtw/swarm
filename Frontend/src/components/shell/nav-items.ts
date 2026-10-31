import type { ComponentType, SVGProps } from 'react'
import { AdminIcon, OverviewIcon, SessionsIcon, WorkIcon } from '../ui/icons'

export interface NavItem {
  to: string
  label: string
  icon: ComponentType<SVGProps<SVGSVGElement>>
}

export const NAV_ITEMS: NavItem[] = [
  { to: '/overview', label: 'Overview', icon: OverviewIcon },
  { to: '/work', label: 'Work', icon: WorkIcon },
  { to: '/sessions', label: 'Sessions', icon: SessionsIcon },
  { to: '/admin', label: 'Admin', icon: AdminIcon },
]
