# GRC2

GRC2 is a MelonLoader/Harmony mod project for GUNVOLT RECORDS Cychronicle custom chart, BGM, BGA, artwork, and metadata injection.

## Current Documentation

Start here:

- [Documentation Index](docs/README.md)
- [Current Hook Map](docs/maintenance/HOOK_MAP.md)
- [Harmony Layer README](GRC2/Harmony/README.md)

Older generated or pre-cleanup notes are kept under [docs/archive](docs/archive). Treat archive documents as historical context, not the current source of truth.

## Current Source Snapshot

- Main mod source: `GRC2/`
- Tests: `GRC2.Tests/`
- Current managed C# source count, excluding `bin/obj`: 113 files in `GRC2`, 1 file in `GRC2.Tests`
- Current hook ownership is documented in [docs/maintenance/HOOK_MAP.md](docs/maintenance/HOOK_MAP.md)

## Verification

Use:

```powershell
dotnet test GRC2.Tests\GRC2.Tests.csproj --no-restore --logger "console;verbosity=normal"
```

