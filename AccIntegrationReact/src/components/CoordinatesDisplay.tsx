import type { Coordinates } from '../bridge/types';

interface CoordinatesDisplayProps {
  coordinates: Coordinates;
  onChange: (coordinates: Coordinates) => void;
}

export function CoordinatesDisplay({ coordinates, onChange }: CoordinatesDisplayProps) {
  const handleAxisChange = (axis: keyof Coordinates, value: string) => {
    const parsed = Number(value);
    onChange({ ...coordinates, [axis]: Number.isNaN(parsed) ? 0 : parsed });
  };

  const handleFisrtFocus = (target: any) =>{
    if (target.value.startsWith('0')){
        target.select()
    }
  }

  return (
    <div className="field-group">
      <label>Coordinates</label>
      <div className="coordinates-row">
        {(['x', 'y', 'z'] as const).map((axis) => (
          <div className="coordinate-field" key={axis}>
            <span>{axis.toUpperCase()}</span>
            <input
              type="number"
              value={coordinates[axis]}
              onFocus={(e) => handleFisrtFocus(e.target)}
              onChange={(e) => handleAxisChange(axis, e.target.value)}
            />
          </div>
        ))}
      </div>
    </div>
  );
}
