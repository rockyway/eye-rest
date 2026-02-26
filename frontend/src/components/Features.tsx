import {
  EyeIcon, TimerIcon, ChartIcon,
  MonitorIcon, ShieldIcon, GlobeIcon, PaletteIcon, CursorClickIcon,
} from '../assets/icons'
import type { ReactNode } from 'react'
import { useTrackSection } from '../hooks/useTrackSection'

interface Feature {
  num: number
  icon: ReactNode
  title: string
  desc: string
  accent: string
}

const FEATURES: Feature[] = [
  {
    num: 1,
    icon: <EyeIcon size={28} />,
    title: '20-20-20 Rule',
    desc: 'Smart dual timers: 20-min eye rest with a 20-sec look-away, plus 55-min break cycles.',
    accent: '#2196F3',
  },
  {
    num: 2,
    icon: <CursorClickIcon size={28} />,
    title: 'Non-Blocking Reminders',
    desc: 'Click any break overlay to dismiss it instantly. Nothing stops you from getting back to urgent work.',
    accent: '#4CAF50',
  },
  {
    num: 3,
    icon: <TimerIcon size={28} />,
    title: 'Smart Pause Detection',
    desc: 'Auto-pauses when you step away from your desk. Resumes the moment you return.',
    accent: '#2196F3',
  },
  {
    num: 4,
    icon: <ChartIcon size={28} />,
    title: '90-Day Analytics',
    desc: 'Track break compliance over time with beautiful bar charts and weekly trend insights.',
    accent: '#2196F3',
  },
  {
    num: 5,
    icon: <MonitorIcon size={28} />,
    title: 'Multi-Monitor',
    desc: 'Break overlays appear across every connected display simultaneously.',
    accent: '#2196F3',
  },
  {
    num: 6,
    icon: <ShieldIcon size={28} />,
    title: 'Privacy-First',
    desc: 'All data stays local on your machine. Zero telemetry, zero cloud sync, zero accounts.',
    accent: '#4CAF50',
  },
  {
    num: 7,
    icon: <GlobeIcon size={28} />,
    title: 'Cross-Platform',
    desc: 'Native .NET 8 + Avalonia on Windows 10+ and macOS 12. Runs on ARM64 and x64.',
    accent: '#2196F3',
  },
  {
    num: 8,
    icon: <PaletteIcon size={28} />,
    title: 'Beautiful Themes',
    desc: 'Light and dark modes with a glass-card aesthetic and animated mesh gradient background.',
    accent: '#2196F3',
  },
]

export default function Features() {
  const sectionRef = useTrackSection('features')
  return (
    <section ref={sectionRef} id="features" className="section" style={{ position: 'relative', zIndex: 1 }}>
      <div style={{ maxWidth: 1200, margin: '0 auto' }}>
        {/* Header */}
        <div style={{ textAlign: 'center', marginBottom: 64 }} className="anim-fade-up">
          <span className="section-label">Why Eye-Rest?</span>
          <h2 className="section-title" style={{ margin: '0 auto 16px' }}>
            Everything for healthier<br />
            <em style={{ fontStyle: 'italic', color: 'var(--blue-600)' }}>screen time.</em>
          </h2>
          <p className="section-subtitle">
            Built to protect your vision without interrupting your flow.
          </p>
        </div>

        {/* Grid */}
        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))',
          gap: 20,
        }}>
          {FEATURES.map((f) => (
            <article
              key={f.num}
              className="glass-card glass-card-hover"
              style={{ padding: '28px 28px 32px', position: 'relative', overflow: 'hidden' }}
            >
              {/* Ghost number */}
              <div className="feature-num">{String(f.num).padStart(2, '0')}</div>

              {/* Icon bubble */}
              <div style={{
                width: 52,
                height: 52,
                borderRadius: 14,
                background: `${f.accent}14`,
                border: `1px solid ${f.accent}28`,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: f.accent,
                marginBottom: 18,
                flexShrink: 0,
              }}>
                {f.icon}
              </div>

              <h3 style={{
                fontFamily: 'var(--font-display)',
                fontWeight: 600,
                fontSize: '1.1rem',
                color: 'var(--text-heading)',
                margin: '0 0 10px',
              }}>
                {f.title}
              </h3>
              <p style={{
                fontFamily: 'var(--font-body)',
                fontSize: '0.9rem',
                color: 'var(--text-muted)',
                margin: 0,
                lineHeight: 1.65,
              }}>
                {f.desc}
              </p>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}
