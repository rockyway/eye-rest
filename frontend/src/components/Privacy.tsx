export default function Privacy() {
  return (
    <section className="section" style={{ position: 'relative', zIndex: 1 }}>
      <div style={{ maxWidth: 720, margin: '0 auto' }}>
        <div className="glass-card anim-fade-up" style={{ padding: '48px 40px' }}>
          <h1 style={{
            fontFamily: 'var(--font-display)',
            fontWeight: 700,
            fontSize: 'clamp(1.5rem, 3vw, 2rem)',
            color: 'var(--text-heading)',
            margin: '0 0 8px',
          }}>
            Privacy Policy
          </h1>
          <p style={{
            fontSize: '0.85rem',
            color: 'var(--text-muted)',
            margin: '0 0 32px',
          }}>
            Last updated: February 25, 2026
          </p>

          <Section title="Overview">
            <p>
              Eye-Rest is a free, open-source desktop application that provides automated eye rest and break reminders.
              We are committed to protecting your privacy. This policy explains what data Eye-Rest collects, how it is used,
              and your choices regarding that data.
            </p>
          </Section>

          <Section title="Data We Collect">
            <p><strong>Data stored locally on your device:</strong></p>
            <ul>
              <li><strong>Configuration settings</strong> &mdash; your preferences for timers, audio, theme, and display options. Stored as JSON files in your app data folder.</li>
              <li><strong>Usage analytics</strong> &mdash; session counts, break compliance, and timer statistics. Stored in a local SQLite database for the analytics dashboard. This data never leaves your device.</li>
              <li><strong>Donation status</strong> &mdash; if you choose to donate and enter a license key, a masked version of the key and verification timestamp are stored securely using Windows DPAPI or macOS Keychain.</li>
            </ul>

            <p><strong>Data transmitted to third parties:</strong></p>
            <ul>
              <li><strong>Donation validation</strong> &mdash; if you voluntarily enter a donation license key, the key is sent to the <a href="https://www.lemonsqueezy.com/privacy" target="_blank" rel="noopener noreferrer">LemonSqueezy API</a> for one-time validation. No other data is sent.</li>
              <li><strong>Website analytics</strong> &mdash; the Eye-Rest website (<a href="https://eyerest.net">eyerest.net</a>) uses Google Analytics to understand visitor traffic. This does not apply to the desktop application.</li>
            </ul>
          </Section>

          <Section title="Data We Do NOT Collect">
            <ul>
              <li>No personal information (name, email, address)</li>
              <li>No screen content or screenshots</li>
              <li>No browsing history or application usage outside Eye-Rest</li>
              <li>No telemetry or crash reports sent to us</li>
              <li>No advertising or tracking in the desktop application</li>
            </ul>
          </Section>

          <Section title="Local Storage Locations">
            <ul>
              <li><strong>Windows:</strong> <code>%APPDATA%\EyeRest\</code></li>
              <li><strong>macOS:</strong> <code>~/.config/EyeRest/</code></li>
            </ul>
            <p>You can delete all stored data by removing this folder. Uninstalling the application does not automatically delete your data.</p>
          </Section>

          <Section title="Microsoft Store">
            <p>
              If you install Eye-Rest from the Microsoft Store, the app runs in a sandboxed environment.
              Configuration data is stored in a per-package location managed by Windows and is removed when you uninstall the app.
            </p>
          </Section>

          <Section title="Third-Party Services">
            <ul>
              <li><strong>LemonSqueezy</strong> &mdash; payment processing for voluntary donations. See their <a href="https://www.lemonsqueezy.com/privacy" target="_blank" rel="noopener noreferrer">privacy policy</a>.</li>
              <li><strong>Google Analytics</strong> &mdash; website traffic analytics only (not in the desktop app). See Google's <a href="https://policies.google.com/privacy" target="_blank" rel="noopener noreferrer">privacy policy</a>.</li>
            </ul>
          </Section>

          <Section title="Open Source">
            <p>
              Eye-Rest is open source. You can review the complete source code to verify our privacy practices at{' '}
              <a href="https://github.com/rockyway/eye-rest" target="_blank" rel="noopener noreferrer">github.com/rockyway/eye-rest</a>.
            </p>
          </Section>

          <Section title="Children's Privacy">
            <p>
              Eye-Rest does not knowingly collect any personal information from children under 13. The application does not
              require account creation or collect personal data.
            </p>
          </Section>

          <Section title="Changes to This Policy">
            <p>
              We may update this privacy policy from time to time. Changes will be posted on this page with an updated
              revision date. Since Eye-Rest does not collect email addresses, we cannot notify you directly of changes.
            </p>
          </Section>

          <Section title="Contact">
            <p>
              If you have questions about this privacy policy, please open an issue on our{' '}
              <a href="https://github.com/rockyway/eye-rest/issues" target="_blank" rel="noopener noreferrer">GitHub repository</a>.
            </p>
          </Section>
        </div>
      </div>
    </section>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div style={{ marginBottom: 24 }}>
      <h2 style={{
        fontFamily: 'var(--font-display)',
        fontWeight: 700,
        fontSize: '1.25rem',
        color: 'var(--text-heading)',
        margin: '0 0 12px',
      }}>
        {title}
      </h2>
      <div style={{
        fontSize: '0.95rem',
        lineHeight: 1.7,
        color: 'var(--text-body)',
      }}>
        {children}
      </div>
    </div>
  )
}
