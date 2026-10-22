# Validation — 2026-10-22 KST

## Current native app

| Check | Result | What it establishes |
|---|---|---|
| Windows x64 .NET Framework compilation | Passed | Executable builds without an external SDK |
| Policy/controller mock suite | 37 checks passed | Fixed 1–7 decisions, 90°C override, curve hysteresis and delay, invalid settings rejection, manual readback, thermal fallback, no silent rearm, external change fallback, sensor failure restoration |
| UI bitmap preview | Inspected and corrected | DPI-scaled text, flowing buttons and separate curve editor render correctly; preview uses simulated data; main, log-expanded, 130% text and separate curve layouts inspected |
| Actual 30-second level-1 smoke test | Passed after fixes | Both levels stayed 1; settled RPM about 3,360 / 3,530; CPU 46–47°C; requested Auto and verified automatic restoration |
| Read timeouts in that smoke test | Still present | Final recorded status had 76 read attempts / 9 timeouts; recovered. This is not a claim of better per-call reliability |
| Actual forced process exit | Passed | Main app exited deliberately during level 1; independent watchdog verified automatic mode at heartbeat age 8.30 seconds |
| Logon-task registration/removal | Passed | Task was created, queried and removed. It is left disabled; an actual reboot/login was not performed |
| 90°C physical-temperature event | Not performed | Thermal fallback was tested with fake sensor values, not by heating the machine to 90°C |
| Sleep/resume, long app endurance, battery power | Not yet validated | Implementation exists; no hardware validation claim |
| Levels 6 and 7 | Policy/UI implemented; hardware not validated | Do not infer their actual RPM or sustained behavior |

The first native-app tests exposed two implementation defects: an overly strict model string check rejected `Swift SFE16-43`, and a transient heartbeat-file read failure caused premature watchdog restoration. Both were fixed before the successful checks above. Initial model rejection did not perform control writes. Short spin polling was also added to reduce scheduler delays; its power cost and long-term reliability have not been measured.

Real-mode tests ended in manufacturer automatic mode. The startup task was removed after its registration test. Automatic mode was subsequently checked independently via the existing EC reader.

## Earlier Python prototype — separate evidence

- Level 1 completed a 10-minute hold: CPU 50–63°C; actual fresh fan measurements 3,076–3,353 / 3,506–3,546 RPM. 1,053 reads / 98 timeouts; three sample failures recovered; no observed level mismatch.
- A subsequent one-hour target run aborted at about 400 seconds: CPU 45–54°C, manual bit still set, both levels changed externally from 1 to 4. The cause remains unresolved. Main and watchdog automatic restoration were verified.
- Short tests verified level selection and actual RPM response for levels 1, 2, 3 and 5. These are not a complete mapping of every level, workload or firmware.
- Earlier fault-injection tests covered high-temperature alarm, sensor failure and process crash with automatic restoration.

These results establish an experimental control path on one SFE16-43 / Ray_PEU / V1.71. They do not establish broad compatibility, app endurance for hours, battery savings, or a 2,500 RPM capability.

## UI revision

Main dashboard reduced to temperature/mode/RPM/quick controls; bounded collapsed logs; separate draggable curve graph and table; Windows DPI-aware sizing and selectable text zoom. Added boundary tests for 59°C/60°C and migration/preservation tests for old factory/custom curves. No thermal or EC protocol change in this revision. The existing app was replaced only after its watchdog restored automatic mode and an independent EC read verified it.

## Mode layout and responsive text revision

Curve / fixed 1–7 / Off now share one row, with a plain-language mode description below. The three secondary actions remain below the description. New settings default to Curve. STATUS UI lines contain only date, temperature, mode, levels and RPM; diagnostics remain in the log files. Runtime text/spacing adapts to window and screen size, with Per-Monitor DPI notifications. Mock tests cover default mode, descriptions, exact STATUS format and text-fit factors. Render checks verify four visible STATUS lines, a single-line temperature and shrinking text in a narrower window. Actual cross-monitor switching has not been physically exercised.

## Font-change performance revision

Recursive layout suspension now batches font/spacing changes across nested containers. Visible native controls defer painting until layout and button text fitting finish. Generated fonts are reused, unused cached fonts are pruned above 256 entries, and generated fonts/timers are disposed with the form. Disposed forms ignore delayed responsive work.

On this device, a hardware-free MainForm benchmark with logs expanded changed between 115%, 130%, 150%, and 100% 20 times after four warm-up changes. Both versions used an off-screen window and included Application.DoEvents/Update. Baseline median/max were 657.67/857.27 ms with 6440 layout events; the optimized run measured 96.95/168.43 ms with 2150 layout events. This measures the test UI update path, not end-to-end display/compositor latency, and is not a guarantee for every resolution. The ordinary on-screen render checks passed (four STATUS lines, single-line temperature, responsive font shrink). All 37 policy/fake-controller checks passed. The EC protocol, sampling rate and thermal protection were not modified.

## Rounded dashboard and 10-second sampling revision

The title bar now includes SFE16-43. The redundant dashboard heading/stripe was removed. White space, a pale blue rounded status card, blue mode controls, pale yellow level controls, a yellow settings action and muted secondary text establish visual hierarchy. The logon startup checkbox remains. Font sizing and a GitHub link are in the footer. Rounded controls use native keyboard/button behavior and antialiased custom painting.

Temperature, levels and both RPMs are now acquired together every approximately 10 seconds, and each successful sample is logged and published to the UI. Mode requests wake the worker immediately. Heartbeats continue during idle waits without EC reads; the 8-second watchdog threshold is unchanged. The 90 C thermal fallback is now evaluated at the 10-second sample cadence, rather than the previous 2-second cadence.

Render checks passed for four visible STATUS lines, a single-line temperature and responsive shrinking. Fake-controller tests include an assertion that an idle controller does not poll temperature again within 2.2 seconds. A short live check after replacement recorded five fresh STATUS samples with timestamp gaps 10/10/9/10 seconds and no fallback, watchdog or critical event in that interval. This verifies coexistence of the 10-second cadence and the watchdog over that short interval; it does not establish long-term endurance or thermally stress the laptop.

## Opaque corners, yellow card and six languages

RoundedButton now clears an opaque ancestor surface in both OnPaintBackground and OnPaint. RoundedTable also establishes its background during OnPaint. Rendering each of ten buttons and the status card in isolation verified corner pixel ARGB FFFFFFFF, including transparent parent layouts; background-paint-only handling had failed this isolated check. This is a bitmap paint-path regression check, not a claim that every Windows compositor/DPI configuration was physically inspected.

The yellow status card, uniform sky-blue/selected-blue mode buttons, matching settings action, gray log action and removed footer sentence were rendered with six UI languages. English is the default; Korean, Japanese, Simplified Chinese, Spanish and German are selectable. Main and curve-editor previews were generated for every language; German and CJK layouts were visually inspected. Input hints, ordinary curve validation messages, editor headers and tray commands are localized; raw diagnostic messages and STATUS bodies remain unchanged. Exit remains available as a muted action.

49 policy, localization and fake-controller checks passed, including six complete catalog rows per key, default/fallback English and settings serialization of a chosen language. Existing four-line-log, single-line-temperature and responsive text render checks passed. UI-only paint/localization changes do not alter the existing 10-second sensor cadence or the 8-second watchdog threshold.
# Release slimming (2026-10-22)

The production build excludes fake EC hardware and preview rendering. Preview code lives in `tests/Preview.cs` and is compiled only with `build.ps1 -Development`. Removed actual-device smoke/crash/startup test command handlers. The controller, watchdog, EC access, policy and responsive drawing logic remain unchanged. Removed duplicate model probing in MainForm; PawnEc still validates model/board/BIOS before loading the module or opening the driver.

The user ZIP excludes screenshots, development documentation and test binaries. It retains the module license and corresponding source archive. The repository retains tests, validation evidence and porting documentation. Old local staging builds were moved outside the repository. The currently running app was not replaced during this cleanup.

## Hardware allowlist removal

Removed the model/board/BIOS rejection and unused CIM identity code at the user's request. The build no longer references System.Management. Module verification, EC register mapping, sensor checks, 90°C fallback and watchdog behavior are unchanged. This removes an execution restriction; it does not establish compatibility with additional laptops. Historical allowlist descriptions above refer to earlier builds.

## Startup recovery — 2026-10-23

After a reboot, a read on this device found manual mode set (0x00=2), both levels 4, and no current app sensor log. The old controller would terminate initialization on an existing manual bit. Independently clearing that bit and verifying automatic mode allowed the diagnostic app to publish fresh readings again. What set the manual bit during boot is not established.

The worker now arms its watchdog, restores and verifies manufacturer automatic mode when it finds manual mode at startup, and continues sensor monitoring. It does not silently reapply a saved manual setting after this recovery; the user can explicitly select it again. Initialization errors publish status to the UI, an explicit mode request can restart a failed unowned worker, and bounded startup tracing is stored in startup.log. Mock regression tests cover stale manual startup, continued monitoring, explicit rearming and retry after a failed baseline read. A further full reboot of the patched build remains to be tested.

## Saved-mode restoration and file sharing — 2026-10-23

Supersedes the startup Auto latch described above: clearing a pre-existing manual EC state no longer changes the requested or saved mode. The next temperature sample applies the saved fixed level or curve. Suspend uses temporary Auto without saving it; resume reloads the saved settings. High-temperature and sensor/control-error fallbacks still persist Auto.

A live log also recorded an IOException from File.InternalReplace, followed by worker exit and automatic restoration. Atomic writes now use unique temporary files, serialized in-process writers and bounded retries. Settings and watchdog reads allow FileShare.Delete, so readers do not block atomic replacement. Regression checks cover saved fixed level 3 over an initial EC level 4, saved curve at 60°C, transient sharing violations, a reader held open during replacement and concurrent JSON writers. Full reboot and sleep/wake of this revision remain untested.

