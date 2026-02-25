import { useEffect, useState } from 'react'
import { ThemeProvider } from './theme'
import { initGA } from './analytics'
import Nav from './components/Nav'
import Hero from './components/Hero'
import Features from './components/Features'
import AppPreview from './components/AppPreview'
import Download from './components/Download'
import Support from './components/Support'
import Privacy from './components/Privacy'
import Footer from './components/Footer'

function AppInner() {
  const [path, setPath] = useState(window.location.pathname)

  useEffect(() => {
    initGA()

    const onPopState = () => setPath(window.location.pathname)
    window.addEventListener('popstate', onPopState)
    return () => window.removeEventListener('popstate', onPopState)
  }, [])

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
