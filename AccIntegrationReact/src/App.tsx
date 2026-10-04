import { useCallback, useEffect, useState } from 'react';
import { Header } from './components/Header';
import { ImportView } from './components/ImportView';
import { LoadingView } from './components/LoadingView';
import { ReadingView } from './components/ReadingView';
import { SettingsPanel } from './components/SettingsPanel';
import { ErrorBanner } from './components/ErrorBanner';
import { QueueStatus } from './components/QueueStatus';
import { ModelTransition } from './components/ModelTransition';
import { sendToHost, useHostMessage } from './bridge/webviewBridge';
import type { ConversionRequest, CoordinateSystem, Coordinates, ModelResult, QueueState } from './bridge/types';
import './App.css';

type View = 'import' | 'reading' | 'loading';

const NO_COORDINATES: Coordinates = { x: 0, y: 0, z: 0 };

function sameModel(left: ConversionRequest, right: ConversionRequest) {
  return left.ifcPath === right.ifcPath && left.outputPath === right.outputPath;
}

function App() {
  const [view, setView] = useState<View>('import');
  const [coordinateSystem, setCoordinateSystem] = useState<CoordinateSystem | null>(null);
  const [coordinates, setCoordinates] = useState<Coordinates>(NO_COORDINATES);
  const [progress, setProgress] = useState(0);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [conversionRequest, setConversionRequest] = useState<ConversionRequest | null>(null);
  const [queue, setQueue] = useState<QueueState>({ current: null, stage: null, pending: [] });
  const [modelResults, setModelResults] = useState<ModelResult[]>([]);
  const [completedModel, setCompletedModel] = useState<ConversionRequest | null>(null);
  const finishTransition = useCallback(() => setCompletedModel(null), []);

  const recordModelResult = (result: ModelResult) => {
    setModelResults((previous) => [...previous.filter((item) => !sameModel(item, result)), result]);
  };

  const markModelAsUploaded = (model: ConversionRequest) => {
    recordModelResult({ ...model, status: 'uploaded' });
  };

  useHostMessage((message) => {
    switch (message.type) {
      case 'queueState':
        setQueue(message.payload);
        break;
      case 'conversionRequest':
        // A new IFC: clear the previous one's values until the brain has read this IFC's placement.
        setConversionRequest(message.payload);
        setModelResults((previous) => previous.filter((item) => !sameModel(item, message.payload)));
        setCoordinates(NO_COORDINATES);
        setCoordinateSystem(null);
        setErrorMessage(null);
        setProgress(0);
        setView('reading');
        break;
      case 'coordinates':
        setCoordinates(message.payload);
        break;
      case 'coordinateSystemSelected':
        setCoordinateSystem(message.payload);
        // The host sends this after coordinates, including null when no system was found.
        setView((current) => current === 'reading' ? 'import' : current);
        break;
      case 'importProgress':
        setProgress(message.payload.percent);
        setView('loading');
        break;
      case 'error':
        setErrorMessage(message.payload.message);
        break;
      case 'importResult': {
        const result = message.payload;
        // The host includes paths for both outcomes. Also support a failure containing only a message.
        const model = result.ifcPath && result.outputPath
          ? { ifcPath: result.ifcPath, outputPath: result.outputPath }
          : conversionRequest;

        if (result.success) {
          markModelAsUploaded(result);
        } else if (model) {
          recordModelResult({ ...model, status: 'failed', message: result.message });
        }

        // A delayed result for another model must not clear the active model's form.
        if (!conversionRequest || (model && sameModel(model, conversionRequest))) {
          // Only the current job's success can trigger the handoff. Incoming host messages
          // continue updating the next model while this short visual transition is shown.
          setCompletedModel(result.success && conversionRequest && queue.pending.length > 0 ? result : null);
          setView('import');
          setProgress(0);
          setErrorMessage(result.success ? null : result.message ?? 'Conversion failed.');
          setConversionRequest(null);
        }
        break;
      }
    }
  });

  // Declared after useHostMessage so the listener is attached before the host answers.
  useEffect(() => {
    sendToHost({ type: 'ready' });
  }, []);

  const handleConvert = () => {
    if (!conversionRequest || !coordinateSystem) return;
    setProgress(0);
    setErrorMessage(null);
    setView('loading');
    sendToHost({ type: 'convert', payload: { coordinates, coordinateSystem } });
  };

  return (
    <div className="app">
      <Header onSettingsClick={() => setSettingsOpen(true)} />

      <QueueStatus queue={queue} results={modelResults} />

      <div
        className="model-stage"
        key={completedModel ? 'handoff' : JSON.stringify([conversionRequest?.ifcPath, conversionRequest?.outputPath, view])}
      >
      {completedModel ? (
        <ModelTransition model={completedModel} onComplete={finishTransition} />
      ) : view === 'reading' ? (
        <ReadingView />
      ) : view === 'import' ? (
        <ImportView
          coordinateSystemName={coordinateSystem?.name ?? null}
          onChooseCoordinateSystem={() =>
            sendToHost({ type: 'chooseCoordinateSystem', payload: { wkt: coordinateSystem?.wkt ?? null } })
          }
          coordinates={coordinates}
          onCoordinatesChange={setCoordinates}
          onConvert={handleConvert}
          convertDisabled={!conversionRequest || !coordinateSystem}
          onCancel={() => sendToHost({ type: 'close' })}
        />
      ) : (
        <LoadingView progress={progress} />
      )}
      </div>

      {errorMessage && (
        <ErrorBanner message={errorMessage} onClose={() => setErrorMessage(null)} />
      )}

      {settingsOpen && <SettingsPanel onClose={() => setSettingsOpen(false)} />}
    </div>
  );
}

export default App;
