import { Link } from 'react-router-dom'
import { buttonClass } from '../components/ui/button'
import { PageHeader } from '../components/ui/PageHeader'
import { usePageMeta } from '../lib/document-meta'

export function PreviewNotice({ title }: { title: string }) {
  usePageMeta({
    title: `${title} · Swarm`,
    description: `${title} is not part of the Admin design preview.`,
    path: `/${title.toLowerCase()}`,
  })

  return (
    <>
      <PageHeader title={title} />
      <div className="max-w-[62ch] pt-2">
        <p className="font-display text-xl text-ink">Not in this preview</p>
        <p className="mt-3 text-sm leading-relaxed text-ink-secondary">
          This pass covers Admin only. The rest of the dashboard follows once the direction is
          settled.
        </p>
        <Link to="/admin" className={buttonClass('primary', 'mt-6')}>
          Go to Admin
        </Link>
      </div>
    </>
  )
}

export function NotFoundPage() {
  usePageMeta({
    title: 'Page not found · Swarm',
    description: 'That page does not exist in the Swarm Admin preview.',
    path: '/404',
  })

  return (
    <>
      <PageHeader title="Page not found" />
      <div className="max-w-[62ch] pt-2">
        <p className="text-sm leading-relaxed text-ink-secondary">
          The address does not match a page in this preview.
        </p>
        <Link to="/admin" className={buttonClass('primary', 'mt-6')}>
          Go to Admin
        </Link>
      </div>
    </>
  )
}
