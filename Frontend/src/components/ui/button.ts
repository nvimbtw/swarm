import { cn } from '../../lib/cn'

export const buttonStyles = {
  primary:
    'inline-flex h-9 items-center justify-center gap-2 rounded-control bg-primary px-3 text-sm font-medium text-primary-fg transition-colors duration-[var(--duration-fast)] hover:bg-primary-hover active:bg-primary-active',
  quiet:
    'inline-flex h-9 items-center gap-2 rounded-control px-2 text-sm text-ink-muted transition-colors duration-[var(--duration-fast)] hover:bg-hover hover:text-primary',
  icon: 'inline-flex size-8 items-center justify-center rounded-control text-ink-muted transition-colors duration-[var(--duration-fast)] hover:bg-hover hover:text-ink',
} as const

export type ButtonVariant = keyof typeof buttonStyles

export function buttonClass(variant: ButtonVariant, className?: string) {
  return cn(buttonStyles[variant], className)
}
