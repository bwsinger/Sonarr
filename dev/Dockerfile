# Keep runtime, UI, native dependencies and Core source on the same upstream release.
FROM lscr.io/linuxserver/sonarr@sha256:a5c1a5fecbef946927ab90ad68df319ac5fe644057e5fc18cd993f01ac07b2b2
ARG DEV_REVISION
LABEL org.opencontainers.image.source="https://github.com/bwsinger/Sonarr" \
      org.opencontainers.image.revision="${DEV_REVISION}" \
      media-server.custom-features="import-minimum-score,numbered-episode-title"
COPY .dev-build/Sonarr.Core.dll /app/sonarr/bin/Sonarr.Core.dll
