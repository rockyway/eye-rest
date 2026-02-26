declare global {
  interface Window {
    gtag: (...args: unknown[]) => void
    dataLayer: unknown[]
  }
}

const GA_ID = 'G-075GMTH58H'

export function initGA(): void {
  if (typeof window === 'undefined' || typeof window.gtag !== 'function') return
  window.gtag('config', GA_ID)
}

function trackEvent(action: string, params?: Record<string, string>): void {
  if (typeof window !== 'undefined' && typeof window.gtag === 'function') {
    window.gtag('event', action, params)
  }
}

export function trackPageView(path: string): void {
  trackEvent('page_view', { page_path: path, page_title: document.title })
}

export function trackCtaClick(platform: 'windows' | 'macos'): void {
  trackEvent('cta_click', { platform, source: 'hero' })
}

export function trackDownload(platform: 'windows' | 'macos'): void {
  trackEvent('download_click', { platform, source: 'download_section' })
}

export function trackDonate(): void {
  trackEvent('donate_click', { source: 'support_section' })
}

export function trackNavClick(label: string, source: 'nav' | 'footer'): void {
  trackEvent('nav_click', { label, source })
}

export function trackThemeChange(theme: string, source: 'nav' | 'preview'): void {
  trackEvent('theme_change', { theme, source })
}

export function trackExternalLink(url: string, label: string): void {
  trackEvent('external_link_click', { url, label })
}

export function trackSectionView(sectionId: string): void {
  trackEvent('section_view', { section: sectionId })
}

export function trackContactSubmit(status: 'success' | 'error'): void {
  trackEvent('contact_submit', { status })
}
