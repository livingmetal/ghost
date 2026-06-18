# Build verify portability

## Task

Remove machine-specific build script assumptions and add a portable verification baseline.

## Changed files

- `publish.ps1`
- `scripts/verify.ps1`
- `.github/workflows/verify.yml`
- `README.md`

## Changes

- Removed hardcoded personal dotnet fallback paths.
- Added `LIVINGMETAL_DOTNET` as the explicit non-standard dotnet location override.
- Kept `dotnet` from `PATH` as the default resolution path.
- Moved icon-generation exit-code checking inside the icon-generation branch.
- Added a Windows GitHub Actions verification workflow.
- Documented portable dotnet resolution and verification behavior in `README.md`.

## Verification

Not run in this session.

Expected local verification command:

```powershell
.\scripts\verify.ps1 -Configuration Release
```

Optional publish verification:

```powershell
.\scripts\verify.ps1 -Configuration Release -PublishRuntimeIdentifier win-arm64
```

## Remaining risk

- GitHub Actions availability for `.NET 10 SDK` must be confirmed by the workflow run.
- No test project exists yet; this PR only improves restore/build/publish verification portability.
