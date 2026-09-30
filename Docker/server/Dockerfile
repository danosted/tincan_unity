# TinCan dedicated server (Linux, headless). The build context is the Linux server build, not the repo:
#   .\.tools\build-server.ps1 -Image
#   docker build -f Docker/server/Dockerfile -t tincan-server:local Builds/LinuxServer
# Plan: .docs/plans/dedicated-server-container.md
FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --system --create-home --uid 10001 tincan

WORKDIR /opt/tincan
COPY --chown=tincan:tincan . .
RUN chmod +x TinCanServer.x86_64

USER tincan
EXPOSE 7777/udp

# Logs to stdout (docker logs). The argument after -server is the listen endpoint; override it with the command,
# e.g. `docker run ... tincan-server:local 0.0.0.0:9000`.
ENTRYPOINT ["./TinCanServer.x86_64", "-logFile", "-", "-server"]
CMD ["0.0.0.0:7777"]
