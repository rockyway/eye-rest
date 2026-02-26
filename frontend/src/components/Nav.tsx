import { useTheme, type ThemeMode } from '../theme'
import { trackNavClick, trackThemeChange } from '../analytics'

function SunIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="5"/>
      <line x1="12" y1="1" x2="12" y2="3"/>
      <line x1="12" y1="21" x2="12" y2="23"/>
      <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"/>
      <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"/>
      <line x1="1" y1="12" x2="3" y2="12"/>
      <line x1="21" y1="12" x2="23" y2="12"/>
      <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"/>
      <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"/>
    </svg>
  )
}

function MoonIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/>
    </svg>
  )
}

function AutoIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10"/>
      <path d="M12 8v4l3 3"/>
    </svg>
  )
}

const OPTIONS: { value: ThemeMode; label: string; icon: React.ReactNode }[] = [
  { value: 'light', label: 'Light', icon: <SunIcon /> },
  { value: 'auto',  label: 'Auto',  icon: <AutoIcon /> },
  { value: 'dark',  label: 'Dark',  icon: <MoonIcon /> },
]

export default function Nav() {
  const { mode, setMode, isDark } = useTheme()

  return (
    <nav style={{
      position: 'fixed',
      top: 0,
      left: 0,
      right: 0,
      zIndex: 100,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'space-between',
      padding: '12px 28px',
      background: isDark
        ? 'rgba(10, 15, 30, 0.72)'
        : 'rgba(255, 255, 255, 0.45)',
      backdropFilter: 'blur(20px)',
      WebkitBackdropFilter: 'blur(20px)',
      borderBottom: isDark
        ? '1px solid rgba(255,255,255,0.06)'
        : '1px solid rgba(255,255,255,0.65)',
      transition: 'background 0.3s ease, border-color 0.3s ease',
    }}>
      {/* Brand */}
      <a
        href="/"
        onClick={(e) => {
          e.preventDefault()
          trackNavClick('home', 'nav')
          window.history.pushState({}, '', '/')
          window.dispatchEvent(new PopStateEvent('popstate'))
          window.scrollTo(0, 0)
        }}
        style={{ display: 'flex', alignItems: 'center', gap: 8, textDecoration: 'none' }}
      >
        <img src="/app-icon.png" alt="Eye-Rest" style={{ width: 26, height: 26, borderRadius: 7 }} />
        <span style={{
          fontFamily: 'var(--font-display)',
          fontWeight: 700,
          fontSize: '1rem',
          color: 'var(--text-heading)',
        }}>Eye-Rest</span>
      </a>

      {/* Nav links (hidden on small screens) */}
      <div style={{
        display: 'flex',
        gap: 24,
        alignItems: 'center',
      }} className="nav-links-desktop">
        {[
          { label: 'Features', href: '#features' },
          { label: 'Preview', href: '#preview' },
          { label: 'Download', href: '#download' },
          { label: 'Support', href: '#support' },
          { label: 'Contact', href: '#contact' },
        ].map((l) => (
          <a key={l.label} href={`/${l.href}`} className="nav-link" style={{ fontSize: '0.875rem' }}
             onClick={(e) => {
               e.preventDefault()
               trackNavClick(l.label.toLowerCase(), 'nav')
               if (window.location.pathname !== '/') {
                 window.history.pushState({}, '', '/')
                 window.dispatchEvent(new PopStateEvent('popstate'))
                 setTimeout(() => {
                   document.querySelector(l.href)?.scrollIntoView({ behavior: 'smooth' })
                 }, 100)
               } else {
                 document.querySelector(l.href)?.scrollIntoView({ behavior: 'smooth' })
               }
             }}>
            {l.label}
          </a>
        ))}
      </div>

      {/* Theme toggle pill */}
      <div style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 2,
        background: isDark ? 'rgba(255,255,255,0.06)' : 'rgba(33,150,243,0.07)',
        border: isDark ? '1px solid rgba(255,255,255,0.10)' : '1px solid rgba(33,150,243,0.18)',
        borderRadius: 100,
        padding: 3,
      }}>
        {OPTIONS.map((opt) => (
          <button
            key={opt.value}
            onClick={() => { setMode(opt.value); trackThemeChange(opt.value, 'nav') }}
            title={opt.label}
            aria-pressed={mode === opt.value}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 5,
              padding: '5px 11px',
              borderRadius: 100,
              border: 'none',
              cursor: 'pointer',
              fontSize: '0.75rem',
              fontFamily: 'var(--font-body)',
              fontWeight: 600,
              transition: 'all 0.18s ease',
              background: mode === opt.value
                ? (isDark ? 'rgba(255,255,255,0.15)' : '#fff')
                : 'transparent',
              color: mode === opt.value
                ? 'var(--blue-600)'
                : 'var(--text-muted)',
              boxShadow: mode === opt.value
                ? (isDark ? '0 1px 4px rgba(0,0,0,0.3)' : '0 1px 4px rgba(33,150,243,0.15)')
                : 'none',
            }}
          >
            {opt.icon}
            <span style={{ display: 'none' }} className="theme-label">{opt.label}</span>
          </button>
        ))}
      </div>
    </nav>
  )
}
