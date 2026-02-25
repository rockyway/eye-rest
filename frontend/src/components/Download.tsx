import { trackDownload } from '../analytics'
import { WindowsIcon, AppleIcon, DownloadIcon } from '../assets/icons'

interface PlatformCardProps {
  platform: 'windows' | 'macos'
  icon: React.ReactNode
  name: string
  subtitle: string
  reqs: string[]
  label: string
  variant: 'primary' | 'outline'
  fileType: string
}

function PlatformCard({ platform, icon, name, subtitle, reqs, label, variant, fileType }: PlatformCardProps) {
  return (
    <div className="glass-card glass-card-hover" style={{ padding: '36px 32px', textAlign: 'center', position: 'relative', overflow: 'hidden' }}>
      {/* Top accent line */}
      <div style={{
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        height: 3,
        background: variant === 'primary'
          ? 'linear-gradient(90deg, #2196F3, #1976D2)'
          : 'linear-gradient(90deg, #1A365D, #2196F3)',
        borderRadius: '18px 18px 0 0',
      }} />

      <div style={{
        width: 72,
        height: 72,
        borderRadius: 20,
        background: variant === 'primary' ? 'rgba(33,150,243,0.08)' : 'rgba(26,54,93,0.06)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        margin: '0 auto 20px',
        border: `1px solid ${variant === 'primary' ? 'rgba(33,150,243,0.18)' : 'rgba(26,54,93,0.12)'}`,
      }}>
        {icon}
      </div>

      <h3 style={{
        fontFamily: 'var(--font-display)',
        fontWeight: 700,
        fontSize: '1.5rem',
        color: 'var(--text-heading)',
        margin: '0 0 6px',
      }}>{name}</h3>
      <p style={{
        fontFamily: 'var(--font-body)',
        fontSize: '0.85rem',
        color: 'var(--text-muted)',
        margin: '0 0 24px',
      }}>{subtitle}</p>

      <ul style={{
        listStyle: 'none',
        padding: 0,
        margin: '0 0 28px',
        textAlign: 'left',
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
      }}>
        {reqs.map((r) => (
          <li key={r} style={{
            fontFamily: 'var(--font-body)',
            fontSize: '0.875rem',
            color: 'var(--text-body)',
            display: 'flex',
            alignItems: 'center',
            gap: 10,
          }}>
            <span style={{
              width: 20, height: 20,
              borderRadius: '50%',
              background: 'rgba(76, 175, 80, 0.12)',
              border: '1px solid rgba(76,175,80,0.28)',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: '0.7rem',
              color: '#4CAF50',
              flexShrink: 0,
              fontWeight: 700,
            }}>✓</span>
            {r}
          </li>
        ))}
      </ul>

      <a
        href="#"
        className={`btn btn-${variant}`}
        style={{ width: '100%', justifyContent: 'center' }}
        onClick={(e) => { e.preventDefault(); trackDownload(platform) }}
      >
        <DownloadIcon size={16} color={variant === 'primary' ? '#fff' : 'var(--blue-600)'} />
        {label} <span style={{ opacity: 0.7, fontSize: '0.85em' }}>.{fileType}</span>
      </a>
    </div>
  )
}

export default function Download() {
  return (
    <section id="download" className="section" style={{ position: 'relative', zIndex: 1 }}>
      <div style={{ maxWidth: 1000, margin: '0 auto' }}>
        {/* Header */}
        <div style={{ textAlign: 'center', marginBottom: 56 }} className="anim-fade-up">
          <span className="section-label">Download</span>
          <h2 className="section-title" style={{ margin: '0 auto 16px' }}>
            Get Eye-Rest<br />
            <em style={{ fontStyle: 'italic', color: 'var(--blue-600)' }}>for free.</em>
          </h2>
          <p className="section-subtitle">
            Free to use. Pay what you want to support continued development.
          </p>
        </div>

        {/* Platform cards */}
        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))',
          gap: 28,
          maxWidth: 780,
          margin: '0 auto',
        }}>
          <PlatformCard
            platform="windows"
            icon={<WindowsIcon size={36} color="#2196F3" />}
            name="Windows"
            subtitle="Windows 10 or later"
            reqs={['4 GB RAM minimum', '100 MB disk space', '.NET 8 Runtime (auto-installed)', 'x64 or ARM64']}
            label="Download"
            variant="primary"
            fileType="exe"
          />
          <PlatformCard
            platform="macos"
            icon={<AppleIcon size={36} color="#1A365D" />}
            name="macOS"
            subtitle="macOS 12 Monterey or later"
            reqs={['4 GB RAM minimum', '100 MB disk space', 'Native Apple Silicon (ARM64)', 'Intel Macs also supported']}
            label="Download"
            variant="outline"
            fileType="dmg"
          />
        </div>

        <p style={{
          textAlign: 'center',
          marginTop: 28,
          fontFamily: 'var(--font-body)',
          fontSize: '0.85rem',
          color: 'var(--text-muted)',
        }}>
          Releases coming soon &mdash; check back shortly.
        </p>
      </div>
    </section>
  )
}
