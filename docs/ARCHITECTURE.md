# Architecture and operational limits

## Components

- `Policy.cs`: validation and deterministic fixed/curve decisions. Curve temperatures strictly increase, levels never decrease as temperature increases, first point is 0°C. Heating changes immediately; cooling crosses the current-level threshold minus hysteresis and waits the configured delay. CPU ≥90°C wins over either manual mode.
- `Hardware.cs`: exact model/board/BIOS allowlist, signed-module SHA-256 verification, dynamically loaded official PawnIOLib, port protocol and simulated device. Individual read transactions hold `Global\Access_EC`; retry backoff happens after release. Writes are not blindly repeated after an ambiguous timeout.
- `Controller.cs`: background worker, sensor validation, applied-level readback, independent watchdog heartbeat and bounded rotating logs. No continuous reassertion against an external controller. A mismatch latches automatic mode and requires explicit user action.
- `App.cs`: application entry point and logon-task registration.
- `Interface.cs`: DPI-scaled native Windows Forms dashboard, selectable text size, bounded collapsible logs, separate draggable curve editor and tray controls. Real mode is elevated; demo mode is unprivileged and opens no driver. This first version elevates the whole app, not a separately sandboxed privileged service.

## Cadence and display freshness

CPU temperature is read around every 2 seconds. Mode, fan levels and tachometer bytes are read around every 6 seconds. Real elapsed cadence can grow during retries. Only successful fresh temperature reads refresh the watchdog heartbeat. The large status strip refreshes every 20 seconds or immediately after a user mode request; requested settings and actual application are distinct. The detail line reports read times and cumulative timeout counts. Tachometer byte reads are sequential, not an atomic 16-bit snapshot; occasional edge artifacts are possible.

File writes use a temporary file plus replacement. The watchdog retains the last valid heartbeat through a single sharing/read error; it does not interpret one failed file read as an 8-second sensor outage. Cross-process log writes use a separate named mutex. A readiness file must be present before the first manual write; loss of the watchdog process also causes fallback.

## Startup and power

The UI explicitly installs logon startup only when its checkbox is selected. Executable and module are copied to Program Files; the task uses the current user's interactive token and highest available privileges, with `IgnoreNew`, battery execution enabled and no run-time limit. No password is stored. The normal app singleton prevents two UI controllers. Demo has a different singleton and cannot control hardware. The user should also stop unrelated EC control tools; they do not share this app's singleton.

Windows suspend/resume events request manufacturer automatic mode. Suspend waits a bounded time for the worker to relinquish manual ownership. Resume remains automatic. This has not been validated by an actual sleep/wake cycle in the first release; late power notifications, a hung OS, or terminating both processes can defeat cleanup. The app does not call an API to block Windows sleep.

## Fail-safe boundaries

Restore clears only EC control bit 1 and checks the result. If the main process cannot restore, it stops refreshing the heartbeat and leaves the watchdog armed to retry. Both processes depend on the same functioning driver and EC. Neither can fix a disconnected or unresponsive EC, a frozen OS or power loss. Automatic mode is a firmware behavior, not proof of a particular cooling performance.

The 90°C threshold is a user-requested fallback setting, not a manufacturer-specified limit and not a limit for every component. It is immutable in this first UI. A safety fallback saves Auto as the selected mode so startup does not silently restore a prior faulting manual mode.

## Remaining work

Find the root cause of EC read contention and unexpected level changes; evaluate a supported driver-side atomic EC transaction rather than extending polling forever. Validate all 1–7 levels, app duration endurance, battery power, suspend/resume and logon task behavior on real hardware. Code tests and mock temperatures cannot establish hardware support. No internet telemetry is implemented.

## Responsive display

`ResponsiveForm.cs` handles window/screen bounds and WM_DPICHANGED; fonts and spacing are fitted from a baseline layout with a debounced UI timer. This does not change EC sampling cadence. The visible log filters for STATUS only; the controller emits a concise STATUS body, while diagnostic events remain on disk. New settings default to Curve, explicit Auto paths (including smoke-test cleanup) remain Auto.
