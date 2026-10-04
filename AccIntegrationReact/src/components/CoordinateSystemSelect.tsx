interface CoordinateSystemSelectProps {
  value: string | null;
  onChoose: () => void;
}

export function CoordinateSystemSelect({ value, onChoose }: CoordinateSystemSelectProps) {
  return (
    <div className="field-group">
      <label>Coordinate system</label>
      <div className="row-with-button">
        <span className="value-pill" title={value ?? 'Not selected'}>{value ?? 'Not selected'}</span>
        <button type="button" className="btn-outline" onClick={onChoose}>
          Choose…
        </button>
      </div>
    </div>
  );
}
