# Third-party component

`LpcACPIEC.bin` is an unmodified signed PawnIO.Modules EC access module by namazso, from the official 0.2.11 release package used during this investigation.

- Project: https://github.com/namazso/PawnIO.Modules
- Source file: https://github.com/namazso/PawnIO.Modules/blob/0.2.11/LpcACPIEC.p
- Release binaries: https://github.com/namazso/PawnIO.Modules/releases
- License: LGPL-2.1-or-later; see `COPYING.LESSER` and the preserved header in `LpcACPIEC.p`.
- SHA-256: `C38FD116E7AFF4D1FDB0A494E296BE0A6708E5A22FC72F14587442FB7F8F7906`

The corresponding EC source is supplied as `LpcACPIEC.p`, with the complete upstream 0.2.11 source archive in `PawnIO.Modules-0.2.11-source.zip`; the upstream project contains compiler/include infrastructure and signing information. This app does not embed or modify the module. Replacing a module also requires deliberate verification/update of the source-level module hash; this is a compatibility/integrity check, not a claim that modified LGPL code may not be used. Driver signing rules belong to PawnIO/Windows.

PawnIO.sys and PawnIOLib.dll are not bundled. Install official PawnIO separately: https://pawnio.eu/ . They retain their own upstream licenses.
