import { useState } from 'react'
import { trackThemeChange } from '../analytics'
import { useTrackSection } from '../hooks/useTrackSection'

export default function AppPreview() {
  const [isDark, setIsDark] = useState(false)
  const sectionRef = useTrackSection('preview')

  return (
    <section ref={sectionRef} id="preview" className="section" style={{ position: 'relative', zIndex: 1 }}>
      <div style={{ maxWidth: 1000, margin: '0 auto' }}>
        {/* Header */}
        <div style={{ textAlign: 'center', marginBottom: 48 }} className="anim-fade-up">
          <span className="section-label">App Preview</span>
          <h2 className="section-title" style={{ margin: '0 auto 16px' }}>
            See it in{' '}
            <em style={{ fontStyle: 'italic', color: 'var(--blue-600)' }}>action.</em>
          </h2>
          <p className="section-subtitle">
            A clean, distraction-free interface that lives quietly in your system tray.
          </p>

          {/* Theme toggle */}
          <div style={{ marginTop: 28, display: 'flex', justifyContent: 'center' }}>
            <div className="toggle-pill">
              <button
                className={!isDark ? 'active' : ''}
                onClick={() => { setIsDark(false); trackThemeChange('light', 'preview') }}
                aria-pressed={!isDark}
              >
                ☀ Light
              </button>
              <button
                className={isDark ? 'active' : ''}
                onClick={() => { setIsDark(true); trackThemeChange('dark', 'preview') }}
                aria-pressed={isDark}
              >
                ☽ Dark
              </button>
            </div>
          </div>
        </div>

        {/* Screenshot frame */}
        <div className="anim-fade-up-d2" style={{ position: 'relative' }}>
          {/* Ambient glow */}
          <div style={{
            position: 'absolute',
            inset: -48,
            borderRadius: '50%',
            background: isDark
              ? 'radial-gradient(ellipse at center, rgba(33,150,243,0.10) 0%, transparent 70%)'
              : 'radial-gradient(ellipse at center, rgba(142,202,230,0.22) 0%, transparent 70%)',
            transition: 'background 0.4s ease',
            pointerEvents: 'none',
          }} />

          <div className="glass-card" style={{ padding: 16, maxWidth: 800, margin: '0 auto' }}>
            {/* macOS-style window chrome */}
            <div style={{
              display: 'flex',
              gap: 6,
              alignItems: 'center',
              paddingBottom: 12,
              borderBottom: '1px solid rgba(255,255,255,0.5)',
              marginBottom: 12,
            }}>
              {['#FF5F57', '#FFBD2E', '#28CA41'].map((c, i) => (
                <div key={i} style={{ width: 12, height: 12, borderRadius: '50%', background: c }} />
              ))}
              <div style={{
                marginLeft: 'auto',
                fontFamily: 'var(--font-body)',
                fontSize: '0.72rem',
                color: 'var(--text-subtle)',
                letterSpacing: '0.06em',
              }}>
                Eye-Rest · {isDark ? 'Dark' : 'Light'} Theme
              </div>
            </div>

            {/* Screenshot with crossfade */}
            <div style={{ position: 'relative', borderRadius: 10, overflow: 'hidden' }}>
              <img
                src="/screenshot-light.png"
                alt="Eye-Rest light theme"
                style={{
                  width: '100%',
                  display: 'block',
                  borderRadius: 10,
                  opacity: isDark ? 0 : 1,
                  transition: 'opacity 0.4s ease',
                }}
              />
              <img
                src="/screenshot-dark.png"
                alt="Eye-Rest dark theme"
                style={{
                  width: '100%',
                  display: 'block',
                  borderRadius: 10,
                  position: 'absolute',
                  inset: 0,
                  opacity: isDark ? 1 : 0,
                  transition: 'opacity 0.4s ease',
                }}
              />
            </div>
          </div>

          {/* Feature callouts */}
          <div style={{
            display: 'flex',
            justifyContent: 'center',
            gap: 12,
            marginTop: 24,
            flexWrap: 'wrap',
          }}>
            {[
              { label: 'Timer', value: '19:42' },
              { label: 'Compliance', value: '94%' },
              { label: 'Break streak', value: '12 days' },
            ].map((stat) => (
              <div key={stat.label} className="glass" style={{
                borderRadius: 12,
                padding: '10px 20px',
                textAlign: 'center',
                minWidth: 100,
              }}>
                <div style={{
                  fontFamily: 'var(--font-display)',
                  fontWeight: 700,
                  fontSize: '1.25rem',
                  color: 'var(--blue-600)',
                  lineHeight: 1,
                }}>{stat.value}</div>
                <div style={{
                  fontFamily: 'var(--font-body)',
                  fontSize: '0.7rem',
                  color: 'var(--text-subtle)',
                  marginTop: 4,
                  letterSpacing: '0.08em',
                  textTransform: 'uppercase',
                  fontWeight: 600,
                }}>{stat.label}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  )
}
