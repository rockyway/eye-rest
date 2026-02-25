import { useEffect } from 'react'
import { ThemeProvider } from './theme'
import { initGA } from './analytics'
import Nav from './components/Nav'
import Hero from './components/Hero'
import Features from './components/Features'
import AppPreview from './components/AppPreview'
import Download from './components/Download'
import Support from './components/Support'
import Footer from './components/Footer'

function AppInner() {
  useEffect(() => {
    initGA()
  }, [])

  return (
    <div className="site-bg nav-offset">
      <Nav />
      <Hero />
      <Features />
      <AppPreview />
      <Download />
      <Support />
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
