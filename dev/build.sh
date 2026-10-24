#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet_bin="${DOTNET_BIN:-$HOME/.local/share/arr-dev-dotnet/dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_PROCESSOR_COUNT=2
rm -rf .dev-build
publish_args=(
  -c Release -f net10.0 -r linux-musl-x64 --self-contained true
  -p:UseSharedCompilation=false -p:SolutionDir="$PWD/src/"
  -p:RuntimeIdentifiers=linux-musl-x64 -p:AssemblyVersion=4.0.20.3014
  -p:AssemblyConfiguration=media-server-dev --nologo
)
"$dotnet_bin" publish src/NzbDrone.Console/Sonarr.Console.csproj "${publish_args[@]}" -o .dev-build
"$dotnet_bin" publish src/NzbDrone.Update/Sonarr.Update.csproj "${publish_args[@]}" -o .dev-build/Sonarr.Update
docker build -f dev/Dockerfile \
  --build-arg DEV_REVISION="$(git rev-parse HEAD)" \
  -t sonarr-dev:local .
