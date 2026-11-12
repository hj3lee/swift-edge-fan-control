# Architecture and operational limits

## Control policy

Only a selected `Auto` mode clears EC manual-control bit 1. Fixed and curve modes do not switch to manufacturer control at high temperature, on sensor/control failures, suspend, resume, exit or process failure. No independent watchdog is launched. Legacy `--watchdog` invocations exit without hardware access. Firmware thermal override has not been verified.

The worker reads the actual mode and both levels before applying the requested level. Hardware drift is corrected on the next successful sample. Fixed control does not depend on temperature reads; curve control requires a valid temperature (10–110 C). Sensor errors retain the request and hardware state, report an error and retry after one second. Errors never save Auto to settings.

## Startup, resume and cadence

The UI loads the saved selection and starts one worker. Startup does not temporarily clear manual control. Failed initial reads are retried automatically. Resume requests the current selection again and resets curve hysteresis state. Suspend and exit leave the EC state unchanged. To release control, select Manufacturer default before exiting.

Successful samples wait about ten seconds before the next iteration; requests interrupt that wait. Curve heating raises the level immediately; cooling uses configured hysteresis and delay. The final curve point remains applicable above 90 C. Tachometer bytes are sequential reads and may show edge artifacts; they do not trigger automatic mode changes.

## Hardware and persistence

PawnIO/module hash verification and the global EC transaction mutex remain in place. Read transactions have three attempts with backoff; ambiguous write failures are checked against actual hardware on retry. Settings are saved only by user settings actions. Atomic file replacement and bounded rotating logs are retained. Logon registration uses the current user's elevated interactive task and Program Files executable. The global app singleton prevents multiple UI controllers.

EC communication contention remains unresolved. A failed sensor cannot supply a new curve target, and a crashed process cannot update cooling. Earlier validation documents describe historical automatic-fallback behavior, which is removed in this revision.
