import type { SVGProps } from 'react'

type IconProps = SVGProps<SVGSVGElement>

function Icon({ children, ...props }: IconProps) {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    >
      {children}
    </svg>
  )
}

export function OverviewIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <rect x="2" y="2" width="5" height="5" rx="1" />
      <rect x="9" y="2" width="5" height="5" rx="1" />
      <rect x="2" y="9" width="5" height="5" rx="1" />
      <rect x="9" y="9" width="5" height="5" rx="1" />
    </Icon>
  )
}

export function WorkIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M5.5 4h8" />
      <path d="M5.5 8h8" />
      <path d="M5.5 12h8" />
      <path d="M2.5 4h.01M2.5 8h.01M2.5 12h.01" />
    </Icon>
  )
}

export function SessionsIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <rect x="2" y="3" width="12" height="10" rx="1.5" />
      <path d="m5 6 2 2-2 2" />
      <path d="M9 10h2" />
    </Icon>
  )
}

export function AdminIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M3 4h10" />
      <path d="M3 8h10" />
      <path d="M3 12h10" />
      <circle cx="6" cy="4" r="1.4" fill="currentColor" stroke="none" />
      <circle cx="10" cy="8" r="1.4" fill="currentColor" stroke="none" />
      <circle cx="7" cy="12" r="1.4" fill="currentColor" stroke="none" />
    </Icon>
  )
}

export function SunIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <circle cx="8" cy="8" r="3" />
      <path d="M8 1.5v1M8 13.5v1M1.5 8h1M13.5 8h1M3.4 3.4l.7.7M11.9 11.9l.7.7M12.6 3.4l-.7.7M4.1 11.9l-.7.7" />
    </Icon>
  )
}

export function MoonIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M13 9.5A5.5 5.5 0 0 1 6.5 3a5.5 5.5 0 1 0 6.5 6.5Z" />
    </Icon>
  )
}

export function ExternalIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M9 3h4v4" />
      <path d="M13 3 7.5 8.5" />
      <path d="M12 9.5V13H3V4h3.5" />
    </Icon>
  )
}

export function ChevronRightIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="m6 3 5 5-5 5" />
    </Icon>
  )
}

export function PauseIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M6 3v10M10 3v10" />
    </Icon>
  )
}

export function PlayIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M4 2.5 13 8l-9 5.5Z" />
    </Icon>
  )
}

export function ArrowRightIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M3 8h10" />
      <path d="m9 4 4 4-4 4" />
    </Icon>
  )
}
