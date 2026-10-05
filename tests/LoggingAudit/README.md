# Logging audit

Run from the repository root:

```powershell
dotnet run --project tests/LoggingAudit/LoggingAudit.csproj
```

This dependency-free executable compiles the current production CLI logging and
reporting sources. It uses real temporary log files and test doubles only for the
command/license boundaries. It does not launch TerraExplorer, download files,
modify application data, or touch existing logs. Each run prints its evidence
directory under the system temporary folder; fixtures remain there for inspection.
Exit code 1 means an audit expectation failed.

## Results: 2026-10-05

**After logging fixes: 28 CLI checks passed, 0 failed; 8 WPF checks passed, 0 failed.**
This verifies the exercised logging boundaries, not every possible native/process failure.

The overlap defect was reproduced before the fix: three real worker processes
retained only 102/300 entries (103/300 when crossing the rotation boundary).
With `Local\AccIntegration.SharedLog` protecting file selection, rotation, append,
flush and close, both cases retain all 300 complete entries exactly once. The
same-process overlap regression and abandoned-owner recovery test also pass.
The mutex is shared within one Windows session and waits up to five seconds.
All writers must run the updated logger; old binaries do not participate.
Timeout and I/O failure now emit a structured `logging.failed` diagnostic to
stderr containing the sanitized failed entry. They also set `LastWriteFailure`
and raise `WriteFailed`, which the WPF bridge displays without attempting to log
again. Logging remains non-throwing even if stderr or a notification handler fails.
If all output channels fail, the in-memory health state remains; persistence
cannot be guaranteed when storage is unavailable.

Passing checks: structured errors flush to disk and stderr; unknown commands,
invalid arguments, license failures, command exceptions, direct import failures,
IFC failures and cancellations, and license-query failures are documented; 10 MiB
rotation works; explicit critical entries retain exception details.

| Fixed finding | Current behavior | Evidence |
| --- | --- | --- |
| Command errors omit stack and inner exception | EventWriter accepts file-only exception diagnostics for command, license and direct import failures; local IFC launcher exceptions are retained but excluded from pipe JSON | Command/import/IFC tests retain inner causes and stack information without adding them to UI events |
| Import-result status missing from file logs | File log includes Type and Payload | Uploaded, failed and cancelled payload checks pass |
| Log write failures are silent | Structured stderr fallback plus health state and WPF notification | Blocked-directory, broken-stderr/handler, and mutex-timeout/recovery checks pass |
| Signed URL credentials are not redacted | Shared sink removes HTTP(S) userinfo, complete query strings and fragments before file/fallback output | Activation, nested metadata, exception and WPF tests use synthetic credentials; wire data remains unchanged |

URL redaction applies to new log entries, not existing logs. It is intentionally
conservative about query/fragment fields and preserves the host/path. It is not
a general-purpose scrubber for arbitrary secrets outside URLs.

WPF logging follow-up:

- FileLogger now lives in `AccIntegration/Infra/Logging`, shared by CLI and WPF.
  Both tag entries with source and process ID and use the same named mutex.
- WPF writes directly, including before WebView is ready or without a CLI caller.
  UI exceptions retain full exception diagnostics. Startup/shutdown, conversion
  outcomes, managed unhandled errors, malformed messages and pipe failures are
  logged. Routine progress/UI-state messages are not logged. Cancellations use INFO.
- Each executable uses `AppContext.BaseDirectory/logs`. When published side by
  side they share the same files; separate Debug output directories have separate
  log folders. The final IFC outcome still goes back to the CLI as before.
- `dotnet run --project tests/WpfLoggingAudit/WpfLoggingAudit.csproj` passes all
  eight focused checks: pre-ready UI error, complete exception details, URL
  redaction, failed-logger notification, standalone preparation failure, delayed
  success, import failure and cancellation. It uses
  the real bridge/queue with a controlled conversion boundary, without starting
  WebView or TerraExplorer. Actual startup/crash/pipe failure hooks were inspected
  and built, not exercised in a live WPF process.

Remaining source-review observations:
- Some ignored failures are deliberate (temporary-file cleanup, file-type probing,
  shutdown/cancellation). They need context-sensitive diagnostics, not necessarily
  an ERROR entry for every caught exception.
- CLI unhandled-exception hooks include full exception text. Their actual crash
  delivery, native crashes, disk-full conditions, process termination, and live
  TerraExplorer/WebView failures were not executed. The critical test checks the
  file writer, not an actual process crash.

## Usefulness and volume

The available application log is
`AccIntegrationWPF/AccIntegrationCli/bin/Debug/net8.0/logs/2026/09/2026-09-24.log`:
30,921 bytes, 29 lines, including 8 ERROR lines. One `metadata.list.completed`
line occupies 27,261 bytes (about 88%). This is a small historical sample, not
evidence of production disk usage. WebView2's diagnostic and LevelDB `.log` files
are browser-managed files, not this application's FileLogger output.

- Keep errors, useful exception details, file/job identity, final outcomes, and
  durations. Logging itself is necessary for troubleshooting.
- Log metadata result counts/identifiers instead of entire lists at normal level;
  reserve complete payloads for an explicit diagnostic mode.
- Current successful imports log `layer.imported`, `workflow.completed`, and
  `terra.import.completed`, plus command/license lifecycle lines. Preserve stdout
  contracts; reduce duplicate information in the file sink if volume warrants it.
- Add a retention age or total-size budget. The current 10 MiB rotation limits
  individual ordinary files, not total disk usage, and deletes no old files.
- Add job/process identifiers before troubleshooting overlapping CLI/WPF work.

The initial audit changed tests/reporting only. The authorized mutex fix and WPF
integration subsequently moved the logger to the shared project and extended
verification. The four remaining CLI findings were subsequently fixed with
expanded regression coverage. Production CLI and WPF builds also pass.
