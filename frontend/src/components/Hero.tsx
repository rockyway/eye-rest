import { trackDownload } from '../analytics'
import { DownloadIcon, WindowsIcon, AppleIcon } from '../assets/icons'

export default function Hero() {
  return (
    <section
      style={{ minHeight: '100vh', position: 'relative', overflow: 'hidden', zIndex: 1 }}
      className="flex items-center"
    >
      {/* Decorative lens rings — bioptic motif */}
      <div className="lens-motif" style={{
        width: 600, height: 600,
        top: '50%', right: '-120px',
        transform: 'translateY(-50%)',
        opacity: 0.10,
        background: 'radial-gradient(circle, rgba(33,150,243,0.12) 0%, transparent 70%)',
      }} />
      <div className="lens-motif" style={{
        width: 420, height: 420,
        top: '50%', right: '60px',
        transform: 'translateY(-50%)',
        opacity: 0.14,
      }} />
      <div className="lens-motif" style={{
        width: 260, height: 260,
        top: '50%', right: '190px',
        transform: 'translateY(-50%)',
        opacity: 0.18,
      }} />

      {/* "20" ghost watermark */}
      <div aria-hidden="true" style={{
        position: 'absolute',
        bottom: '-40px',
        left: '-20px',
        fontFamily: 'var(--font-display)',
        fontSize: 'clamp(180px, 22vw, 280px)',
        fontWeight: 900,
        lineHeight: 1,
        color: 'rgba(33, 150, 243, 0.055)',
        userSelect: 'none',
        pointerEvents: 'none',
        letterSpacing: '-0.04em',
      }}>20</div>

      <div style={{ maxWidth: 1200, margin: '0 auto', padding: '80px 24px', width: '100%' }}>
        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))',
          gap: 64,
          alignItems: 'center',
        }}>
          {/* Left: text content */}
          <div className="anim-fade-up">
            {/* App badge */}
            <div style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 10,
              marginBottom: 28,
            }}>
              <img
                src="/app-icon.png"
                alt="Eye-Rest"
                style={{ width: 52, height: 52, borderRadius: 14, boxShadow: '0 4px 16px rgba(33,150,243,0.22)' }}
              />
              <span style={{
                fontFamily: 'var(--font-body)',
                fontSize: '0.8rem',
                fontWeight: 700,
                letterSpacing: '0.12em',
                textTransform: 'uppercase',
                color: 'var(--blue-500)',
                padding: '4px 12px',
                background: 'rgba(33,150,243,0.10)',
                borderRadius: 100,
                border: '1px solid rgba(33,150,243,0.18)',
              }}>
                Free · Windows &amp; macOS
              </span>
            </div>

            <h1 style={{
              fontFamily: 'var(--font-display)',
              fontSize: 'clamp(2.8rem, 5.5vw, 4.2rem)',
              fontWeight: 700,
              color: 'var(--text-heading)',
              margin: 0,
              lineHeight: 1.08,
              letterSpacing: '-0.025em',
            }}>
              Protect your<br />
              <em style={{ fontStyle: 'italic', color: 'var(--blue-600)' }}>vision.</em>
            </h1>

            <p style={{
              fontFamily: 'var(--font-body)',
              fontSize: '1.2rem',
              fontWeight: 500,
              color: 'var(--blue-600)',
              margin: '14px 0 0',
              letterSpacing: '-0.01em',
            }}>
              Automated eye rest &amp; break reminders.
            </p>

            <p style={{
              fontFamily: 'var(--font-body)',
              fontSize: '1.05rem',
              color: 'var(--text-muted)',
              margin: '20px 0 0',
              maxWidth: '44ch',
              lineHeight: 1.65,
            }}>
              Eye-Rest runs quietly in your system tray, guiding you through the
              20-20-20 rule and regular breaks — so long coding sessions don't cost
              you your eyesight.
            </p>

            {/* CTA row */}
            <div style={{
              display: 'flex',
              flexWrap: 'wrap',
              gap: 14,
              marginTop: 40,
              alignItems: 'center',
            }}>
              <a
                href="#download"
                className="btn btn-primary"
                onClick={() => trackDownload('windows')}
              >
                <WindowsIcon size={18} color="#fff" />
                <DownloadIcon size={16} color="#fff" />
                Windows
              </a>
              <a
                href="#download"
                className="btn btn-outline"
                onClick={() => trackDownload('macos')}
              >
                <AppleIcon size={18} color="var(--blue-600)" />
                <DownloadIcon size={16} color="var(--blue-600)" />
                macOS
              </a>
            </div>

            <p style={{
              fontFamily: 'var(--font-body)',
              fontSize: '0.82rem',
              color: 'var(--text-subtle)',
              marginTop: 18,
              letterSpacing: '0.02em',
            }}>
              Free to use &middot; Pay what you want &middot; No accounts
            </p>
          </div>

          {/* Right: screenshot */}
          <div className="anim-fade-up-d3" style={{ position: 'relative' }}>
            {/* Outer glow */}
            <div style={{
              position: 'absolute',
              inset: -32,
              borderRadius: 32,
              background: 'radial-gradient(ellipse at center, rgba(33,150,243,0.15) 0%, transparent 70%)',
              pointerEvents: 'none',
            }} />

            <div
              className="glass-card anim-float"
              style={{
                padding: 12,
                transform: 'perspective(900px) rotateY(-5deg) rotateX(2deg)',
                transformStyle: 'preserve-3d',
              }}
            >
              {/* Window chrome dots */}
              <div style={{
                display: 'flex',
                gap: 6,
                padding: '8px 12px 10px',
                alignItems: 'center',
              }}>
                {['#FF5F57','#FFBD2E','#28CA41'].map((c, i) => (
                  <div key={i} style={{
                    width: 11, height: 11, borderRadius: '50%', background: c,
                  }} />
                ))}
              </div>
              <img
                src="/screenshot-light.png"
                alt="Eye-Rest app — light theme"
                style={{
                  width: '100%',
                  borderRadius: 12,
                  display: 'block',
                  boxShadow: '0 2px 12px rgba(0,0,0,0.12)',
                }}
              />
            </div>

            {/* Stat badges */}
            <div className="glass anim-fade-up-d4" style={{
              position: 'absolute',
              bottom: -20,
              left: -20,
              borderRadius: 14,
              padding: '12px 18px',
              display: 'flex',
              flexDirection: 'column',
              gap: 2,
            }}>
              <span style={{
                fontFamily: 'var(--font-display)',
                fontSize: '1.6rem',
                fontWeight: 700,
                color: 'var(--blue-600)',
                lineHeight: 1,
              }}>20-20-20</span>
              <span style={{
                fontFamily: 'var(--font-body)',
                fontSize: '0.72rem',
                fontWeight: 600,
                color: 'var(--text-muted)',
                letterSpacing: '0.08em',
                textTransform: 'uppercase',
              }}>Eye rest rule</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  )
}
