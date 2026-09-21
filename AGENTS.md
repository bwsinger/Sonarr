# Sonarr media-server development fork

This checkout backs a production media server. Root operational rules:
`/home/bradley/code/media-server-v2/AGENTS.md`.
Do not restart/recreate production containers until the prescribed Tautulli
check succeeds with no active streams (unless the user accepts the impact).
Enable and verify maintenance mode before downtime, and disable/verify it
after services and sanity checks pass. Build and test before cutover.

## Upstream and local branch

- Upstream remote: `upstream` → https://github.com/Sonarr/Sonarr
- Personal fork remote: `origin` → https://github.com/bwsinger/Sonarr
- Maintained branch: `media-server-main`, initially based on `v4.0.20.3014`.
- Runtime/UI base is pinned in `dev/Dockerfile` to that same upstream release.

**Prefer upstream implementations.** On every upstream update, look for
equivalent functionality in merged upstream changes. If upstream covers the
behavior and safety requirements below, favor it over the manually maintained
implementation: run/adapt the regression tests, then remove redundant local
code. Do not retain duplicate checks or competing recovery paths. If upstream
only covers part of a feature, keep and document only the remaining difference.

## Custom features to preserve until upstream replaces them

- Enforce the quality profile's minimum custom-format score against actual
  downloaded-file formats during automatic import, including missing content
  and quality-tier upgrades. Preserve explicit manual-import overrides.
- Automatically fail/blocklist and use the existing replacement-search pipeline
  for completed, application-grabbed torrents whose import results are entirely
  safe, mapped below-minimum rejections. Acceptable equal-score/non-upgrade
  downloads, unknown files and mixed results must not be auto-failed.
- Remove only the torrent entry for these custom automatic failures. Retain
  its payload, because another torrent or the media library can share it.
  Retained rejected payloads require separate, deliberate cleanup. Never remove
  an existing library file as a side effect of rejecting a download.

- Numbered episode-title fallback: map a file such as `01. The Hedge Knight.mkv`
  only with a trusted standard-series/single-season context and an unambiguous
  matching episode number AND normalized title. Do not guess on conflicts,
  anime/daily numbering, specials, or unknown/ambiguous season context.
- Contradictory standard-series filenames such as `S01E01 - Episode 10.mkv`
  require manual review during downloaded-file import. Do not guess which
  number is correct. Jentry's malformed pack imported suffix Episode 10 as E01
  upstream; the custom regression must prevent repeating that assignment.
  This guard does not retroactively repair existing library files.
- When all numbered parses fail, try the existing special-episode title matcher
  before rejecting the media file. This enables known titles such as Sherlock's
  `The Abominable Bride`; unknown titles still fail normally.
- Reconcile a punctuation-stripped grabbed title (`S01E07 08`) only when the
  same torrent's explicit adjacent pair (`S01E07-08`), full release title,
  individually numbered filename, and normal database mappings agree. Preserve
  grabbed history and all other import checks. Do not globally interpret a bare
  numeric episode title as a range, or expand unrelated/scene/season packs.

- Partial-season packs sharing a directory remain a separate limitation.
  Do not mark them completed merely to clear the queue: Cleanuparr may delete
  shared files when a torrent disappears from the Sonarr queue.

## Build, test and deployment

- Exact .NET SDK version is in `global.json`. This server's side-by-side SDKs
  are at `$HOME/.local/share/arr-dev-dotnet/dotnet`.
- `dev/build.sh` builds the patched Core assembly and `sonarr-dev:local`.
  Set `DOTNET_BIN` to use another compatible SDK installation.
- `docker compose build` only packages the existing DLL; it does not compile
  source. Always run `dev/build.sh` after source changes before recreating.
- The image overlays only `Sonarr.Core.dll` on the pinned LinuxServer image.
  This preserves the matching official UI/runtime/native components. If a future
  patch changes another assembly or UI, extend the build explicitly.
- Run the relevant existing Core test fixtures for all changed paths, including
  failure recovery, rejection specifications and (Sonarr) episode aggregation.
- All changes require independent critical review of correctness, data safety
  and regression coverage. Fix findings and repeat review until clean.
- Stack integration lives in `media-server-v2/compose/mserver/sonarr-dev.yml`.
  Existing production URL, host port and internal `sonarr` DNS alias remain
  available. Dev configuration is separate under `appdata/sonarr-dev`.
- Back up current SQLite state and config before replacing a live instance.
  Never run two instances against the same writable configuration directory.
- Do not blindly pull a newer major branch or mix a newer Core assembly with
  the old runtime/UI. Update the source release, pinned image and assembly
  version together; test migrations against a backup before production.
- Rollback after any dev imports must use a stopped copy of the latest dev
  database (same upstream schema/release) or reconcile the library. The original
  pre-cutover database can reference files that dev has since replaced.
- Do not commit appdata, credentials, build outputs or production databases.

Push changes to `origin` (the bwsinger fork); `remote.pushDefault=origin`.
Use feature PRs into `media-server-main`. Fetch official releases from
`upstream`, retaining the pinned source/runtime compatibility checks above.
