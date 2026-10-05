export interface Coordinates {
  x: number;
  y: number;
  z: number;
}

// A coordinate system as TerraExplorer describes it; wkt is what the conversion uses.
export interface CoordinateSystem {
  name: string;
  wkt: string;
}

// Messages React sends TO the C# host
export type OutgoingMessage =
  | { type: 'close' }
  | { type: 'drag' }
  | { type: 'minimize' }
  | { type: 'cancelCurrent' }
  | { type: 'switchToNext'; payload: { coordinates: Coordinates; coordinateSystem: CoordinateSystem | null } }
  | { type: 'moveQueueItem'; payload: { from: number; to: number } }
  // Start converting the current IFC with these values (as read from the IFC, or edited by the user).
  | { type: 'convert'; payload: { coordinates: Coordinates; coordinateSystem: CoordinateSystem } }
  // Open TerraExplorer's coordinate system dialog, starting from the current selection.
  | { type: 'chooseCoordinateSystem'; payload: { wkt: string | null } }
  | { type: 'ready' };

// IFC handed over by the CLI: convert ifcPath into a 3DML at outputPath.
export interface ConversionRequest {
  ifcPath: string;
  outputPath: string;
}

export interface QueueState {
  current: ConversionRequest | null;
  stage: 'reading' | 'awaitingInput' | 'converting' | null;
  pending: ConversionRequest[];
}

export type ImportResult =
  | (ConversionRequest & { success: true; status: 'uploaded'; message?: string | null })
  | { success: false; status: 'failed' | 'cancelled'; message: string; ifcPath?: string; outputPath?: string };

export interface ModelResult extends ConversionRequest {
  status: 'uploaded' | 'failed' | 'cancelled';
  message?: string;
}

// Messages the C# host pushes INTO React
export type IncomingMessage =
  | { type: 'queueState'; payload: QueueState }
  | { type: 'coordinates'; payload: Coordinates }
  // null when the IFC has no coordinate system: the user must choose one.
  | { type: 'coordinateSystemSelected'; payload: CoordinateSystem | null }
  | { type: 'importProgress'; payload: { percent: number } }
  | { type: 'importResult'; payload: ImportResult }
  | { type: 'conversionRequest'; payload: ConversionRequest }
  // Any host-side error that is not a job result (bad launch arguments, unexpected exceptions...).
  | { type: 'error'; payload: { message: string } };
