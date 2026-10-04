import logo from '../assets/acc-logo.svg';
import { sendToHost } from '../bridge/webviewBridge';

interface HeaderProps {
  onSettingsClick: () => void;
}

export function Header({ onSettingsClick }: HeaderProps) {
  return (
    <header className="app-header">
      <img src={logo} alt="Acc Integration" className="app-logo" draggable={false} />
      <div className="header-actions" onMouseDown={(e) => e.stopPropagation()}>
        <button
          type="button"
          className="icon-button"
          onClick={onSettingsClick}
          aria-label="Settings"
          title="Settings"
        >
          <svg className="header-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="m9 3-.6 2.4-2.1 1.2L4 6l-2 3.5 1.7 1.8v2.4L2 15.5 4 19l2.3-.6 2.1 1.2L9 22h4l.6-2.4 2.1-1.2 2.3.6 2-3.5-1.7-1.8v-2.4L20 9.5 18 6l-2.3.6-2.1-1.2L13 3Z" transform="translate(1 -.5)" />
            <circle cx="12" cy="12" r="3" />
          </svg>
        </button>
        <button
          type="button"
          className="icon-button"
          onClick={() => sendToHost({ type: 'minimize' })}
          aria-label="Minimize window"
          title="Minimize"
        >
          <svg className="header-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="M4 12h16" />
          </svg>
        </button>
        <button
          type="button"
          className="icon-button"
          onClick={() => sendToHost({ type: 'close' })}
          aria-label="Close window"
          title="Close"
        >
          <svg className="header-icon" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
            <path d="m5 5 14 14M19 5 5 19" />
          </svg>
        </button>
      </div>
    </header>
  );
}
