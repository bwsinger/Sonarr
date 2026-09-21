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

## Queue-derived regressions — 2026-09-21

- Audited every queued import failure and all five completed qBittorrent
  torrents. Three G66 adjacent episode pairs had punctuation-stripped indexer
  titles; actual torrent ranges and individual filenames were intact.
- Added guarded pair reconciliation, retaining grab history and upgrade checks.
  Trek S04E07's inferior score remains a valid rejection.
- Enabled the existing known-special title lookup before all-null aggregation
  rejection, with Sherlock's actual filename as a regression example.
- Added a real EpisodeService title-matching case alongside aggregation tests.
- Two independent critical reviews finished clean.
- Import/parser/title-matching tests: 1,803 passed, 3 skipped, zero failures.
  Filter: `FullyQualifiedName~MediaFiles.EpisodeImport|FullyQualifiedName~ParserTests|FullyQualifiedName~FindEpisodeByTitleFixture`.
