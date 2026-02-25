import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'

export type ThemeMode = 'light' | 'dark' | 'auto'

interface ThemeCtx {
  mode: ThemeMode
  isDark: boolean
  setMode: (m: ThemeMode) => void
}

const ThemeContext = createContext<ThemeCtx>({
  mode: 'auto',
  isDark: false,
  setMode: () => {},
})

export function useTheme() {
  return useContext(ThemeContext)
}

function getSystemDark() {
  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

function resolveIsDark(mode: ThemeMode, systemDark: boolean) {
  if (mode === 'dark') return true
  if (mode === 'light') return false
  return systemDark
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>(() => {
    const saved = localStorage.getItem('theme-mode') as ThemeMode | null
    return saved ?? 'auto'
  })
  const [systemDark, setSystemDark] = useState(getSystemDark)

  // Listen for system preference changes
  useEffect(() => {
    const mq = window.matchMedia('(prefers-color-scheme: dark)')
    const handler = (e: MediaQueryListEvent) => setSystemDark(e.matches)
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [])

  const isDark = resolveIsDark(mode, systemDark)

  // Apply data-theme to <html>
  useEffect(() => {
    document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light')
  }, [isDark])

  function setMode(m: ThemeMode) {
    setModeState(m)
    localStorage.setItem('theme-mode', m)
  }

  return (
    <ThemeContext.Provider value={{ mode, isDark, setMode }}>
      {children}
    </ThemeContext.Provider>
  )
}
