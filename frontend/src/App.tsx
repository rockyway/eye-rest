import { useEffect, useRef, useState } from 'react'
import { ThemeProvider } from './theme'
import { initGA, trackPageView } from './analytics'
import Nav from './components/Nav'
import Hero from './components/Hero'
import Features from './components/Features'
import AppPreview from './components/AppPreview'
import Download from './components/Download'
import Support from './components/Support'
import Privacy from './components/Privacy'
import Footer from './components/Footer'

function setMeta(name: string, content: string) {
  let el = document.querySelector(`meta[name="${name}"]`) as HTMLMetaElement | null
  if (el) {
    el.content = content
  } else {
    el = document.createElement('meta')
    el.name = name
    el.content = content
    document.head.appendChild(el)
  }
}

function setCanonical(href: string) {
  let el = document.querySelector('link[rel="canonical"]') as HTMLLinkElement | null
  if (el) {
    el.href = href
  } else {
    el = document.createElement('link')
    el.rel = 'canonical'
    el.href = href
    document.head.appendChild(el)
  }
}

function AppInner() {
  const [path, setPath] = useState(window.location.pathname)
  const isFirstRender = useRef(true)

  useEffect(() => {
    initGA()

    const onPopState = () => setPath(window.location.pathname)
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false
      return
    }
    trackPageView(path)
  }, [path])

  useEffect(() => {
    if (path === '/privacy') {
      document.title = 'Privacy Policy — Eye-Rest'
      setMeta('description', 'Eye-Rest privacy policy. All data stays local on your device. No accounts, no tracking, no telemetry.')
      setCanonical('https://eyerest.app/privacy')
    } else {
      document.title = 'Eye-Rest — Free 20-20-20 Eye Break Reminder App for Windows & macOS'
      setMeta('description', 'Eye-Rest is a free desktop app that helps reduce digital eye strain with automated 20-20-20 rule reminders and smart break timers. No accounts, no subscription — download for Windows & macOS.')
      setCanonical('https://eyerest.app/')
    }
  }, [path])

  const isPrivacy = path === '/privacy'

  return (
    <div className="site-bg nav-offset">
      <Nav />
      {isPrivacy ? (
        <Privacy />
      ) : (
        <>
          <Hero />
          <Features />
          <AppPreview />
          <Download />
          <Support />
        </>
      )}
      <Footer />
    </div>
  )
}

export default function App() {
  return (
    <ThemeProvider>
      <AppInner />
    </ThemeProvider>
  )
}
