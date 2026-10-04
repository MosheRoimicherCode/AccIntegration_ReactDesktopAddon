import type { Coordinates } from '../bridge/types';
import { CoordinateSystemSelect } from './CoordinateSystemSelect';
import { CoordinatesDisplay } from './CoordinatesDisplay';
import { ActionBar } from './ActionBar';

interface ImportViewProps {
  coordinateSystemName: string | null;
  onChooseCoordinateSystem: () => void;
  coordinates: Coordinates;
  onCoordinatesChange: (coordinates: Coordinates) => void;
  onConvert: () => void;
  convertDisabled: boolean;
  onCancel: () => void;
}

export function ImportView({
  coordinateSystemName,
  onChooseCoordinateSystem,
  coordinates,
  onCoordinatesChange,
  onConvert,
  convertDisabled,
  onCancel,
}: ImportViewProps) {
  return (
    <main className="import-view">
      <CoordinateSystemSelect value={coordinateSystemName} onChoose={onChooseCoordinateSystem} />
      <CoordinatesDisplay coordinates={coordinates} onChange={onCoordinatesChange} />
      <ActionBar onConvert={onConvert} onCancel={onCancel} convertDisabled={convertDisabled} />
    </main>
  );
}
