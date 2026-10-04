interface ErrorBannerProps {
  message: string;
  onClose: () => void;
}

export function ErrorBanner({ message, onClose }: ErrorBannerProps) {
  return (
    <div className="error-banner">
      <span className="error-banner-message">{message}</span>
      <button
        type="button"
        className="error-banner-close"
        onClick={onClose}
        aria-label="Dismiss error"
        title="Dismiss"
      >
        ✕
      </button>
    </div>
  );
}
