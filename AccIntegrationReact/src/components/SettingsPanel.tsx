interface SettingsPanelProps {
  onClose: () => void;
}

export function SettingsPanel({ onClose }: SettingsPanelProps) {
  return (
    <div className="settings-overlay">
      <div className="settings-panel">
        <div className="settings-icon-placeholder">⚙</div>
        <h2>Acc Integration</h2>
        <p className="settings-version">Version 1.0.0</p>
        <p className="settings-text">
          File import add-on for TerraExplorer.
        </p>
        <button type="button" className="btn-primary" onClick={onClose}>
          Close
        </button>
      </div>
    </div>
  );
}
