import { useEffect } from 'react';
import type { ConversionRequest } from '../bridge/types';

export function ModelTransition({ model, onComplete }: { model: ConversionRequest; onComplete: () => void }) {
  useEffect(() => {
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const timer = window.setTimeout(onComplete, reducedMotion ? 0 : 650);
    return () => window.clearTimeout(timer);
  }, [onComplete]);

  return (
    <main className="model-transition" role="status" aria-live="polite">
      <svg className="model-transition-check" viewBox="0 0 32 32" aria-hidden="true">
        <circle cx="16" cy="16" r="14" />
        <path d="m9 16 5 5 9-10" />
      </svg>
      <p className="model-transition-title">Model uploaded</p>
      <p className="model-transition-file" title={model.ifcPath}>{model.ifcPath.split(/[\\/]/).pop()}</p>
      <p className="model-transition-hint">Checking the next model…</p>
    </main>
  );
}
