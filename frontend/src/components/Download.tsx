import { trackDownload } from '../analytics'
import { WindowsIcon, AppleIcon, DownloadIcon } from '../assets/icons'
import { useTrackSection } from '../hooks/useTrackSection'

interface PlatformCardProps {
  platform: 'windows' | 'macos'
  icon: React.ReactNode
  name: string
  subtitle: string
  reqs: string[]
  label: string
  variant: 'primary' | 'outline'
  fileType?: string
  href: string
  available: boolean
  external?: boolean
}

function PlatformCard({ platform, icon, name, subtitle, reqs, label, variant, fileType, href, available, external }: PlatformCardProps) {
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

      {available ? (
        <a
          href={href}
          {...(!external && { download: true })}
          {...(external && { target: '_blank', rel: 'noopener noreferrer' })}
          className={`btn btn-${variant}`}
          style={{ width: '100%', justifyContent: 'center' }}
          onClick={() => trackDownload(platform)}
        >
          <DownloadIcon size={16} color={variant === 'primary' ? '#fff' : 'var(--blue-600)'} />
          {label}{fileType && <span style={{ opacity: 0.7, fontSize: '0.85em' }}> .{fileType}</span>}
        </a>
      ) : (
        <div
          className={`btn btn-${variant}`}
          style={{
            width: '100%',
            justifyContent: 'center',
            opacity: 0.55,
            cursor: 'not-allowed',
            pointerEvents: 'none',
          }}
        >
          Coming Soon
        </div>
      )}
    </div>
  )
}

export default function Download() {
  const sectionRef = useTrackSection('download')
  return (
    <section ref={sectionRef} id="download" className="section" style={{ position: 'relative', zIndex: 1 }}>
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
            subtitle="Windows 10 version 1809 or later"
            reqs={['Auto-updates via Microsoft Store', '100 MB disk space', 'Sandboxed for security', 'x64 architecture']}
            label="Get from Microsoft Store"
            variant="primary"
            href="https://apps.microsoft.com/detail/9NHN1R0RLH60"
            available={true}
            external={true}
          />
          <PlatformCard
            platform="macos"
            icon={<AppleIcon size={36} color="var(--text-heading)" />}
            name="macOS"
            subtitle="macOS 12 Monterey or later"
            reqs={['4 GB RAM minimum', '100 MB disk space', 'Native Apple Silicon (ARM64)', 'Self-contained — no runtime needed']}
            label="Download"
            variant="outline"
            fileType="zip"
            href="https://dl.eyerest.net/latest/EyeRest-macOS-arm64.zip"
            available={true}
          />
        </div>

        {/* macOS install instructions */}
        <div className="glass-card" style={{
          maxWidth: 780,
          margin: '28px auto 0',
          padding: '20px 28px',
        }}>
          <h4 style={{
            fontFamily: 'var(--font-body)',
            fontSize: '0.85rem',
            fontWeight: 600,
            color: 'var(--text-heading)',
            margin: '0 0 10px',
          }}>
            macOS install instructions
          </h4>
          <ol style={{
            fontFamily: 'var(--font-body)',
            fontSize: '0.83rem',
            color: 'var(--text-body)',
            margin: 0,
            paddingLeft: 20,
            lineHeight: 1.8,
          }}>
            <li>Download and unzip <strong>EyeRest-macOS-arm64.zip</strong></li>
            <li>Move <strong>EyeRest.app</strong> to your Applications folder</li>
            <li>
              On first launch, macOS may block it. To allow it:<br />
              <code style={{
                fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
                fontSize: '0.78rem',
                background: 'rgba(33,150,243,0.08)',
                padding: '2px 8px',
                borderRadius: 6,
                border: '1px solid rgba(33,150,243,0.15)',
              }}>
                xattr -cr /Applications/EyeRest.app
              </code>
            </li>
            <li>Or: right-click the app → <strong>Open</strong> → click <strong>Open</strong> in the dialog</li>
          </ol>
        </div>
      </div>
    </section>
  )
}
