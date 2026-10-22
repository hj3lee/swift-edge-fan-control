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
