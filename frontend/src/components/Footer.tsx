import { GithubIcon } from '../assets/icons'

const NAV = [
  { label: 'Features', href: '#features' },
  { label: 'Preview', href: '#preview' },
  { label: 'Download', href: '#download' },
  { label: 'Support', href: '#support' },
]

export default function Footer() {
  return (
    <footer style={{
      background: 'rgba(26, 54, 93, 0.07)',
      borderTop: '1px solid rgba(255,255,255,0.55)',
      backdropFilter: 'blur(12px)',
      padding: '48px 24px 36px',
      position: 'relative',
      zIndex: 1,
    }}>
      <div style={{ maxWidth: 1100, margin: '0 auto' }}>
        {/* Top row */}
        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'flex-start',
          flexWrap: 'wrap',
          gap: 32,
        }}>
          {/* Brand */}
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
              <img
                src="/app-icon.png"
                alt="Eye-Rest"
                style={{ width: 32, height: 32, borderRadius: 8, boxShadow: '0 2px 8px rgba(33,150,243,0.18)' }}
              />
              <span style={{
                fontFamily: 'var(--font-display)',
                fontWeight: 700,
                fontSize: '1.1rem',
                color: 'var(--text-heading)',
              }}>Eye-Rest</span>
            </div>
            <p style={{
              fontFamily: 'var(--font-body)',
              fontSize: '0.85rem',
              color: 'var(--text-muted)',
              margin: 0,
            }}>Protect your vision. One reminder at a time.</p>
          </div>

          {/* Nav */}
          <nav style={{ display: 'flex', gap: 28, flexWrap: 'wrap', alignItems: 'center' }}>
            {NAV.map((link) => (
              <a
                key={link.label}
                href={link.href}
                className="nav-link"
                style={{ fontFamily: 'var(--font-body)', fontSize: '0.875rem' }}
              >
                {link.label}
              </a>
            ))}
            <a
              href="https://github.com/tamtrantam/eye-rest"
              target="_blank"
              rel="noopener noreferrer"
              className="nav-link"
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: 6,
                fontFamily: 'var(--font-body)',
                fontSize: '0.875rem',
              }}
            >
              <GithubIcon size={16} color="currentColor" />
              GitHub
            </a>
          </nav>
        </div>

        {/* Divider */}
        <div style={{
          height: 1,
          background: 'linear-gradient(90deg, transparent, rgba(26,54,93,0.12) 20%, rgba(26,54,93,0.12) 80%, transparent)',
          margin: '32px 0 24px',
        }} />

        {/* Bottom row */}
        <div style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 12,
        }}>
          <span style={{
            fontFamily: 'var(--font-body)',
            fontSize: '0.8rem',
            color: 'var(--text-subtle)',
          }}>
            &copy; 2026 Eye-Rest. All rights reserved.
          </span>
          <span style={{
            fontFamily: 'var(--font-body)',
            fontSize: '0.8rem',
            color: 'var(--text-subtle)',
          }}>
            Made with ♥ for your eyes
          </span>
        </div>
      </div>
    </footer>
  )
}
