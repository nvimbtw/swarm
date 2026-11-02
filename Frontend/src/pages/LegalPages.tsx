import type { ReactNode } from 'react'
import { PageHeader } from '../components/ui/PageHeader'
import { usePageMeta } from '../lib/document-meta'

export function Placeholder({ children }: { children: ReactNode }) {
  return (
    <mark className="bg-transparent text-warning underline decoration-dotted underline-offset-2">
      {children}
    </mark>
  )
}

interface LegalPageProps {
  title: string
  path: string
  description: string
  children: ReactNode
}

export function LegalPage({ title, path, description, children }: LegalPageProps) {
  usePageMeta({ title: `${title} · Swarm`, description, path })

  return (
    <>
      <PageHeader title={title} />
      <article className="max-w-[68ch] pb-8 pt-2 text-sm leading-relaxed text-ink-secondary [&_h2]:mt-8 [&_h2]:font-display [&_h2]:text-lg [&_h2]:font-medium [&_h2]:text-ink [&_p]:mt-3 [&_ul]:mt-3 [&_ul]:list-disc [&_ul]:pl-5">
        {children}
      </article>
    </>
  )
}

export function PrivacyPage() {
  return (
    <LegalPage
      title="Privacy"
      path="/privacy"
      description="What the Swarm Admin preview does and does not collect."
    >
      <p>
        Overview requests connection diagnostics from the configured Swarm server, including
        connection identifiers, timestamps, durations, and message counts. Message contents are
        not displayed. Admin uses demo data. The dashboard does not collect analytics, set cookies,
        or ask you to sign up. It loads no third-party scripts.
      </p>
      <h2>Hosting</h2>
      <p>
        <Placeholder>[PLACEHOLDER: hosting provider, server logs, retention period]</Placeholder>
      </p>
      <h2>Contact</h2>
      <p>
        <Placeholder>[PLACEHOLDER: contact email for privacy questions]</Placeholder>
      </p>
      <h2>Status</h2>
      <p>
        This is a draft for an internal design preview, not legal advice. It must be reviewed before
        the dashboard is deployed publicly.
      </p>
    </LegalPage>
  )
}

export function ImprintPage() {
  return (
    <LegalPage
      title="Imprint"
      path="/imprint"
      description="Who operates the Swarm Admin preview."
    >
      <h2>Operator</h2>
      <p>
        <Placeholder>[PLACEHOLDER: legal name of the person or company]</Placeholder>
      </p>
      <h2>Address</h2>
      <p>
        <Placeholder>[PLACEHOLDER: street, city, postal code, country]</Placeholder>
      </p>
      <h2>Contact</h2>
      <p>
        <Placeholder>[PLACEHOLDER: contact email]</Placeholder>
      </p>
      <h2>Responsible for content</h2>
      <p>
        <Placeholder>[PLACEHOLDER: name]</Placeholder>
      </p>
      <p>
        An imprint is required in several jurisdictions, including Germany and Austria. Confirm the
        jurisdiction that applies before publishing.
      </p>
    </LegalPage>
  )
}

export function AccessibilityPage() {
  return (
    <LegalPage
      title="Accessibility"
      path="/accessibility"
      description="The accessibility target and how to report a problem with the Swarm Admin preview."
    >
      <h2>Target</h2>
      <p>
        This preview targets WCAG 2.2 AA. Contrast is checked at the token level, keyboard focus is
        visible, status is never carried by colour alone, and reduced motion is respected.
      </p>
      <h2>Known gaps</h2>
      <p>
        The crab sprites are decorative and carry no text alternative; the state word beside each
        session carries the meaning. The preview has not been independently audited.
      </p>
      <h2>Report a problem</h2>
      <p>
        <Placeholder>[PLACEHOLDER: contact for accessibility reports]</Placeholder>
      </p>
    </LegalPage>
  )
}
