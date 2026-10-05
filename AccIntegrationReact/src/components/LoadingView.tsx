interface LoadingViewProps {
  progress: number; // 0-100
  onCancel: () => void;
}

export function LoadingView({ progress, onCancel }: LoadingViewProps) {
  const clamped = Math.max(0, Math.min(100, progress));

  return (
    <main className="loading-view">
      <picture className="conversion-logo">
        <source media="(prefers-reduced-motion: reduce)" srcSet={staticConversionLogo} />
        <img src={conversionLogo} alt="" width="128" height="128" />
      </picture>
      <p className="conversion-label" role="status">Converting model…</p>

      <div className="progress-track" role="progressbar" aria-label="Conversion progress" aria-valuemin={0} aria-valuemax={100} aria-valuenow={clamped}>
        <div className="progress-fill" style={{ width: `${clamped}%` }} />
      </div>
      <div className="progress-label">{clamped}%</div>
      <button type="button" className="btn-outline loading-cancel" onClick={onCancel}>Cancel model</button>
    </main>
  );
}
import conversionLogo from '../assets/conversion-logo.svg';
import staticConversionLogo from '../assets/conversion-logo-static.svg';

