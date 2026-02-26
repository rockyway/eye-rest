import { useState } from 'react'
import { trackContactSubmit } from '../analytics'
import { MailIcon } from '../assets/icons'
import { useTrackSection } from '../hooks/useTrackSection'

export default function Contact() {
  const sectionRef = useTrackSection('contact')
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [message, setMessage] = useState('')
  const [status, setStatus] = useState<'idle' | 'loading' | 'success' | 'error'>('idle')

  const inputStyle: React.CSSProperties = {
    width: '100%',
    background: 'var(--glass-bg)',
    border: '1px solid var(--glass-border)',
    borderRadius: 10,
    padding: '12px 16px',
    fontFamily: 'var(--font-body)',
    fontSize: '0.95rem',
    color: 'var(--text-body)',
    outline: 'none',
    transition: 'border-color 0.2s ease',
    boxSizing: 'border-box',
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setStatus('loading')
    try {
      const res = await fetch('/api/contact', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name: name.trim(), email: email.trim(), message: message.trim() }),
      })
      if (!res.ok) throw new Error()
      setStatus('success')
      trackContactSubmit('success')
      setName('')
      setEmail('')
      setMessage('')
    } catch {
      setStatus('error')
      trackContactSubmit('error')
    }
  }

  return (
    <section ref={sectionRef} id="contact" className="section" style={{ position: 'relative', zIndex: 1 }}>
      <div style={{ maxWidth: 720, margin: '0 auto' }}>
        <div className="glass-card anim-fade-up" style={{ padding: '60px 48px', textAlign: 'center', position: 'relative', overflow: 'hidden' }}>
          {/* Background mail motif */}
          <div aria-hidden="true" style={{
            position: 'absolute',
            bottom: -60,
            right: -40,
            opacity: 0.04,
            pointerEvents: 'none',
          }}>
            <MailIcon size={240} color="#2196F3" />
          </div>

          {/* Icon */}
          <div className="anim-pulse-slow" style={{
            width: 72,
            height: 72,
            borderRadius: 20,
            background: 'rgba(33, 150, 243, 0.08)',
            border: '1px solid rgba(33, 150, 243, 0.2)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            margin: '0 auto 24px',
          }}>
            <MailIcon size={32} color="#2196F3" />
          </div>

          <h2 style={{
            fontFamily: 'var(--font-display)',
            fontWeight: 700,
            fontSize: 'clamp(1.75rem, 3.5vw, 2.5rem)',
            color: 'var(--text-heading)',
            margin: '0 0 16px',
            lineHeight: 1.15,
          }}>
            Get in<br />
            <em style={{ fontStyle: 'italic', color: '#2196F3' }}>touch.</em>
          </h2>

          <p style={{
            fontFamily: 'var(--font-body)',
            fontSize: '1.05rem',
            color: 'var(--text-muted)',
            maxWidth: '46ch',
            margin: '0 auto 28px',
            lineHeight: 1.65,
          }}>
            Have a question, suggestion, or found a bug? We'd love to hear from you.
          </p>

          <form onSubmit={handleSubmit} style={{ textAlign: 'left', maxWidth: 520, margin: '0 auto' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              <input type="text" placeholder="Your name" value={name} onChange={e => setName(e.target.value)} required style={inputStyle} />
              <input type="email" placeholder="Email address" value={email} onChange={e => setEmail(e.target.value)} required style={inputStyle} />
              <textarea placeholder="Your message..." value={message} onChange={e => setMessage(e.target.value)} required rows={5} style={{ ...inputStyle, resize: 'vertical' }} />
            </div>
            <div style={{ textAlign: 'center', marginTop: 24 }}>
              <button type="submit" className="btn btn-primary" disabled={status === 'loading'} style={{ minWidth: 180 }}>
                {status === 'loading' ? 'Sending...' : 'Send Message'}
              </button>
            </div>
          </form>

          {status === 'success' && (
            <p style={{ fontFamily: 'var(--font-body)', fontSize: '0.9rem', color: '#4CAF50', marginTop: 16, textAlign: 'center' }}>
              Thanks for reaching out! We'll get back to you soon.
            </p>
          )}
          {status === 'error' && (
            <p style={{ fontFamily: 'var(--font-body)', fontSize: '0.9rem', color: '#EF4444', marginTop: 16, textAlign: 'center' }}>
              Something went wrong. Please try again or email us directly.
            </p>
          )}
        </div>
      </div>
    </section>
  )
}
