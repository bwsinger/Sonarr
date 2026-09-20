# Development patch validation — 2026-09-20

- Focused changed-path tests: 131 passed; no failures.
- Broader import/download filter: 1,063 passed, 9 skipped; no failures.
- Independent correctness, data-safety, and deployment reviews completed;
  findings fixed and reviewed again until clean.
- Image started with an isolated copied database, no network, no published
  ports, and read-only media/download mounts. Ping and authenticated system
  status succeeded with the matching upstream release.
- Live Knight/Jentry payloads disappeared before smoke replay; filename
  behavior is verified by regression tests, not a replay of those payloads.

Run from the repository root (matching SDK from `global.json`):

```sh
DOTNET_PROCESSOR_COUNT=2 "$HOME/.local/share/arr-dev-dotnet/dotnet" test \
  src/NzbDrone.Core.Test/Sonarr.Core.Test.csproj -c Release -f net6.0 \
  -p:SolutionDir="$PWD/src/" -p:RuntimeIdentifiers=linux-x64 \
  -p:UseSharedCompilation=false \
  --filter 'FullyQualifiedName~MediaFiles.EpisodeImport|FullyQualifiedName~Download'
```
