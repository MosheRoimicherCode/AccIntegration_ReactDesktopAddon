interface ActionBarProps {
  onConvert: () => void;
  onCancel: () => void;
  convertDisabled?: boolean;
}

export function ActionBar({ onConvert, onCancel, convertDisabled }: ActionBarProps) {
  return (
    <div className="action-bar">
      <button type="button" className="btn-outline" onClick={onCancel}>
        Cancel model
      </button>
      <button
        type="button"
        className="btn-primary"
        onClick={onConvert}
        disabled={convertDisabled}
      >
        Convert
      </button>
    </div>
  );
}
