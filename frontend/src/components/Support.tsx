import { trackDonate } from '../analytics'
import { HeartIcon } from '../assets/icons'

export default function Support() {
  return (
    <section id="support" className="section" style={{ position: 'relative', zIndex: 1 }}>
      <div style={{ maxWidth: 720, margin: '0 auto' }}>
        <div className="glass-card anim-fade-up" style={{ padding: '60px 48px', textAlign: 'center', position: 'relative', overflow: 'hidden' }}>
          {/* Background heart motif */}
          <div aria-hidden="true" style={{
            position: 'absolute',
            bottom: -60,
            right: -40,
            opacity: 0.04,
            pointerEvents: 'none',
          }}>
            <HeartIcon size={240} color="#EF4444" />
          </div>

          {/* Icon */}
          <div className="anim-pulse-slow" style={{
            width: 72,
            height: 72,
            borderRadius: 20,
            background: 'rgba(239, 68, 68, 0.08)',
            border: '1px solid rgba(239, 68, 68, 0.2)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            margin: '0 auto 24px',
          }}>
            <HeartIcon size={32} color="#EF4444" />
          </div>

          <h2 style={{
            fontFamily: 'var(--font-display)',
            fontWeight: 700,
            fontSize: 'clamp(1.75rem, 3.5vw, 2.5rem)',
            color: 'var(--text-heading)',
            margin: '0 0 16px',
            lineHeight: 1.15,
          }}>
            Support<br />
            <em style={{ fontStyle: 'italic', color: '#EF4444' }}>development.</em>
          </h2>

          <p style={{
            fontFamily: 'var(--font-body)',
            fontSize: '1.05rem',
            color: 'var(--text-muted)',
            maxWidth: '46ch',
            margin: '0 auto 28px',
            lineHeight: 1.65,
          }}>
            Eye-Rest is free and open-source. If it's helped protect your eyes during
            long work sessions, consider supporting continued development.
          </p>

          {/* Pay What You Want badge */}
          <div style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: 8,
            background: 'rgba(76, 175, 80, 0.08)',
            border: '1px solid rgba(76, 175, 80, 0.25)',
            borderRadius: 100,
            padding: '6px 18px',
            marginBottom: 32,
          }}>
            <span style={{ color: '#4CAF50', fontSize: '0.8rem', fontWeight: 700 }}>✓</span>
            <span style={{
              fontFamily: 'var(--font-body)',
              fontSize: '0.85rem',
              fontWeight: 600,
              color: '#2D7D32',
              letterSpacing: '0.02em',
            }}>Pay What You Want</span>
          </div>

          {/* Donate button */}
          <div>
            <a
              href="https://eyerest.lemonsqueezy.com/checkout/buy/361b6130-55df-4a74-8378-0e87fa355db4"
              target="_blank"
              rel="noopener noreferrer"
              className="btn btn-donate"
              onClick={() => trackDonate()}
            >
              <HeartIcon size={18} color="#fff" />
              Support with a Donation
            </a>
            <p style={{
              fontFamily: 'var(--font-body)',
              fontSize: '0.8rem',
              color: 'var(--text-subtle)',
              marginTop: 14,
            }}>
              Powered by LemonSqueezy &middot; Safe &amp; secure checkout
            </p>
          </div>
        </div>
      </div>
    </section>
  )
}
