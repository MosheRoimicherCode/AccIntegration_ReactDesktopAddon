import { useRef, useState } from 'react';
import type { ModelResult, QueueState } from '../bridge/types';

const stageLabels = {
  reading: 'Reading coordinates',
  awaitingInput: 'Waiting for your confirmation',
  converting: 'Converting and importing',
};

function fileName(path: string) {
  return path.split(/[\\/]/).pop() || path;
}

export function QueueStatus({ queue, results }: { queue: QueueState; results: ModelResult[] }) {
  const [expanded, setExpanded] = useState(false);
  const toggleRef = useRef<HTMLButtonElement>(null);
  const closeQueue = () => {
    setExpanded(false);
    toggleRef.current?.focus();
  };
  const latestResult = results.at(-1);
  if (!queue.current && queue.pending.length === 0 && !latestResult) return null;

  return (
    <section className="queue-status" aria-label="Model queue" onKeyDown={(event) => {
      if (event.key === 'Escape') closeQueue();
    }}>
      <div className="queue-summary">
        <span className="queue-file" title={queue.current?.ifcPath ?? latestResult?.ifcPath}>
          {queue.current ? `IFC: ${fileName(queue.current.ifcPath)}` : queue.pending.length > 0 ? 'Waiting to start…'
            : `${latestResult?.status === 'uploaded' ? 'Uploaded' : 'Failed'}: ${fileName(latestResult!.ifcPath)}`}
        </span>
        <button
          ref={toggleRef}
          type="button"
          className="queue-toggle"
          aria-expanded={expanded}
          aria-controls="model-queue-details"
          aria-label={`Model queue: ${queue.pending.length} waiting`}
          onClick={() => setExpanded(!expanded)}
        >
          <span aria-live="polite" aria-atomic="true">{queue.pending.length} waiting</span>
          <svg className="queue-chevron" viewBox="0 0 16 16" aria-hidden="true">
            <path d={expanded ? 'm4 10 4-4 4 4' : 'm4 6 4 4 4-4'} />
          </svg>
        </button>
      </div>
      {expanded && (
        <div
          id="model-queue-details"
          className="queue-panel"
        >
          <div className="queue-panel-header">
            <strong>Model queue</strong>
            <button type="button" className="icon-button" aria-label="Close queue" onClick={closeQueue}>
              <svg className="header-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m5 5 14 14M19 5 5 19" /></svg>
            </button>
          </div>
          {queue.current && (
            <div className="queue-current">
              <span className="queue-stage">{queue.stage ? stageLabels[queue.stage] : 'Current model'}</span>
              <span className="queue-model-name" title={queue.current.ifcPath}>{fileName(queue.current.ifcPath)}</span>
            </div>
          )}
          {queue.pending.length > 0 ? (
            <ol className="queue-list" aria-label="Waiting models, in processing order">
              {queue.pending.map((job, index) => (
                <li key={`${job.ifcPath}:${job.outputPath}:${index}`} title={job.ifcPath}>
                  <span className="queue-model-name">{fileName(job.ifcPath)}</span>
                  <span className="queue-position">{index === 0 ? 'Next' : 'Waiting'}</span>
                </li>
              ))}
            </ol>
          ) : <p className="queue-empty">No other models waiting.</p>}
          {results.length > 0 && (
            <div className="queue-results">
              <strong>Finished models</strong>
              <ul className="queue-results-list" aria-label="Finished models">
                {results.map((result) => (
                  <li key={JSON.stringify([result.ifcPath, result.outputPath])}>
                    <span className="queue-model-name" title={result.ifcPath}>{fileName(result.ifcPath)}</span>
                    <span className={`queue-result-status queue-result-${result.status}`}>
                      {result.status === 'uploaded' ? 'Uploaded' : 'Failed'}
                    </span>
                    {result.message && <span className="queue-result-message">{result.message}</span>}
                  </li>
                ))}
              </ul>
            </div>
          )}
          <p className="queue-hint">Models are processed one at a time.</p>
        </div>
      )}
    </section>
  );
}
