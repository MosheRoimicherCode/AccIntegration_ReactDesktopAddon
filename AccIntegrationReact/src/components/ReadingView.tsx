export function ReadingView() {
  return (
    <main className="reading-view" aria-busy="true">
      <div className="reading-spinner" aria-hidden="true" />
      <div role="status" aria-live="polite">
        <p className="reading-title">Reading IFC coordinates…</p>
        <p className="reading-description">Checking the model’s position and coordinate system.</p>
      </div>
    </main>
  );
}
