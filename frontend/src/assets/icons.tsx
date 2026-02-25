interface IconProps {
  className?: string;
  size?: number;
  color?: string;
}

// All icons use currentColor by default so they inherit the parent's text color
// via Tailwind utilities. Pass `color` explicitly only when you need a specific
// hex/named value that cannot be set by a parent class.

export function EyeIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {/* Eye outline */}
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
      {/* Pupil */}
      <circle cx="12" cy="12" r="3" />
      {/* Rays symbolising the 20-20-20 rule */}
      <line x1="12" y1="1" x2="12" y2="3.5" />
      <line x1="12" y1="20.5" x2="12" y2="23" />
      <line x1="4.22" y1="4.22" x2="5.64" y2="5.64" />
      <line x1="18.36" y1="18.36" x2="19.78" y2="19.78" />
      <line x1="1" y1="12" x2="3.5" y2="12" />
      <line x1="20.5" y1="12" x2="23" y2="12" />
      <line x1="4.22" y1="19.78" x2="5.64" y2="18.36" />
      <line x1="18.36" y1="5.64" x2="19.78" y2="4.22" />
    </svg>
  );
}

export function TimerIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {/* Stopwatch body */}
      <circle cx="12" cy="14" r="8" />
      {/* Crown */}
      <line x1="12" y1="6" x2="12" y2="2" />
      <line x1="9" y1="2" x2="15" y2="2" />
      {/* Side buttons */}
      <line x1="4.93" y1="7.93" x2="3.51" y2="6.51" />
      <line x1="19.07" y1="7.93" x2="20.49" y2="6.51" />
      {/* Clock hand */}
      <polyline points="12,10 12,14 15,14" />
    </svg>
  );
}

export function ShieldIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
      <polyline points="9,12 11,14 15,10" />
    </svg>
  );
}

export function MonitorIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {/* Primary monitor */}
      <rect x="1" y="3" width="15" height="11" rx="2" ry="2" />
      {/* Secondary monitor offset to suggest multi-monitor setup */}
      <rect x="8" y="9" width="15" height="11" rx="2" ry="2" />
      {/* Stand */}
      <line x1="8.5" y1="14" x2="8.5" y2="20" />
      <line x1="5.5" y1="20" x2="11.5" y2="20" />
    </svg>
  );
}

export function ChartIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {/* Axes */}
      <line x1="3" y1="20" x2="21" y2="20" />
      <line x1="3" y1="20" x2="3" y2="3" />
      {/* Bars */}
      <rect x="5" y="13" width="3" height="7" rx="1" fill={color} stroke="none" />
      <rect x="10" y="8" width="3" height="12" rx="1" fill={color} stroke="none" />
      <rect x="15" y="10" width="3" height="10" rx="1" fill={color} stroke="none" />
    </svg>
  );
}

export function MeetingIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {/* Calendar outline */}
      <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
      {/* Header divider */}
      <line x1="3" y1="9" x2="21" y2="9" />
      {/* Calendar pins */}
      <line x1="8" y1="2" x2="8" y2="6" />
      <line x1="16" y1="2" x2="16" y2="6" />
      {/* Video camera shape inside calendar body */}
      <polygon points="9,12 9,17 14,17 14,12" />
      <polyline points="14,14 17,12 17,17 14,15" />
    </svg>
  );
}

export function GlobeIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      <circle cx="12" cy="12" r="10" />
      <line x1="2" y1="12" x2="22" y2="12" />
      <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
    </svg>
  );
}

export function PaletteIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z" />
      {/* Color dots on the palette */}
      <circle cx="8.5" cy="7.5" r="1.5" fill={color} stroke="none" />
      <circle cx="12" cy="6" r="1.5" fill={color} stroke="none" />
      <circle cx="15.5" cy="8" r="1.5" fill={color} stroke="none" />
      <circle cx="17" cy="11.5" r="1.5" fill={color} stroke="none" />
    </svg>
  );
}

export function WindowsIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={color}
      className={className}
      aria-hidden="true"
    >
      {/* Windows logo — four panes in a 2x2 grid */}
      <rect x="2" y="2" width="9.5" height="9.5" rx="1" />
      <rect x="12.5" y="2" width="9.5" height="9.5" rx="1" />
      <rect x="2" y="12.5" width="9.5" height="9.5" rx="1" />
      <rect x="12.5" y="12.5" width="9.5" height="9.5" rx="1" />
    </svg>
  );
}

export function AppleIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={color}
      className={className}
      aria-hidden="true"
    >
      <path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.8-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83zM13 3.5c.73-.83 1.94-1.46 2.94-1.5.13 1.17-.34 2.35-1.04 3.19-.69.85-1.83 1.51-2.95 1.42-.15-1.15.41-2.35 1.05-3.11z" />
    </svg>
  );
}

export function HeartIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={color}
      className={className}
      aria-hidden="true"
    >
      <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
    </svg>
  );
}

export function GithubIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={color}
      className={className}
      aria-hidden="true"
    >
      <path d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.531 1.032 1.531 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0 1 12 6.844a9.59 9.59 0 0 1 2.504.337c1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.02 10.02 0 0 0 22 12.017C22 6.484 17.522 2 12 2z" />
    </svg>
  );
}

export function DownloadIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke={color}
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden="true"
    >
      {/* Tray with downward arrow */}
      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
      <polyline points="7 10 12 15 17 10" />
      <line x1="12" y1="15" x2="12" y2="3" />
    </svg>
  );
}

export function StarIcon({ className, size = 24, color = 'currentColor' }: IconProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={color}
      className={className}
      aria-hidden="true"
    >
      <polygon points="12,2 15.09,8.26 22,9.27 17,14.14 18.18,21.02 12,17.77 5.82,21.02 7,14.14 2,9.27 8.91,8.26 12,2" />
    </svg>
  );
}
