import { useEffect } from 'react'
import { initGA } from './analytics'
import Hero from './components/Hero'
import Features from './components/Features'
import AppPreview from './components/AppPreview'
import Download from './components/Download'
import Support from './components/Support'
import Footer from './components/Footer'

function App() {
  useEffect(() => {
    initGA()
  }, [])

  return (
    <div className="mesh-bg min-h-screen">
      <Hero />
      <Features />
      <AppPreview />
      <Download />
      <Support />
      <Footer />
    </div>
  )
}

export default App
