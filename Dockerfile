ARG baseimage
FROM $baseimage



# SL_SESSION_TOKEN is injected at runtime (OpenShift env, sourced from a Secret).
# Do NOT hardcode the token here — this file is committed to git.

USER root

# Install the SeaLights .NET Core agent (glibc/RHEL 8 build) into /sealights.
# Provides libSL.DotNet.ProfilerLib.Linux.so referenced by the CORECLR_* paths above.
#
# vz-proxy is SeaLights' egress proxy, used only here and on the ENTRYPOINT --proxy — never as a
# runtime ENV, which would also push the app's own traffic (Elasticsearch, etc.) through it.
# -4: agents.sealights.co resolves IPv6-only and the agent has no IPv6 egress (the proxy is reached over IPv4).
# --insecure/--proxy-insecure: the proxy intercepts TLS with a CA the agent doesn't trust and has no cert
# store (or openssl) to add it to — internal download over the trusted VPN. Prefer `--cacert <file>` if a
# corporate CA ever becomes available.
RUN mkdir -p /sealights/logs && \
    curl -4 -fsSL --insecure --proxy-insecure --proxy "http://vz-proxy.pncint.net:8080" -o /tmp/sl-agent.tar.gz \
      https://agents.sealights.co/dotnetcore/latest/sealights-dotnet-agent-linux-self-contained.tar.gz && \
    tar -xzf /tmp/sl-agent.tar.gz --directory /sealights && \
    rm /tmp/sl-agent.tar.gz

# Coverage runs via the cdAgent CLI (uid_entrypoint runs `SL.DotNet cdAgent` wrapping the app),
# not the env-var Profiler-Initiated Collector. cdAgent is the only mode that accepts --proxy, which
# we need to reach *.sealights.co:443 through PNC's egress proxy. cdAgent injects the CORECLR_*
# profiler env into the child dotnet itself, so we must NOT set them container-wide — they would also
# attach the profiler to the SL.DotNet process and collide with cdAgent's own setup.
ENV SL_FEATURES_IDENTIFYMETHODSBYFQN="true"

ENV SL_GENERAL_APPNAME="dse_searchapi"
ENV SL_GENERAL_BRANCHNAME="main"
ENV SL_LABID="my_lab"

# Per-project knobs read by uid_entrypoint to build the cdAgent command — together with
# SL_GENERAL_APPNAME above, these are the only lines to change when reusing this in another service.
ENV SL_TARGET_DLL="Dse.Api.dll"
ENV SL_NAMESPACE="Dse.*"
ENV SL_APP_DIR="/app"
# Assembly-level scan filter (cdAgent reads this natively; no CLI flag is passed). Reuses the namespace.
ENV SL_SCAN_INCLUDEASSEMBLIES="${SL_NAMESPACE}"

# Higher-level port mapped to port 80/443 in OpenShift
EXPOSE 8080
ENV ASPNETCORE_URLS=http://*:8080
ENV ASPNETCORE_HTTP_PORTS=8080

# Flush stdout/stderr line-by-line so startup crashes actually surface in `oc logs`.
ENV DOTNET_CONSOLE_DISABLE_BUFFERING=1
ENV DOTNET_RUNNING_IN_CONTAINER=true

WORKDIR /app
ENV PATH=/app:${PATH} HOME=/app

COPY uid_entrypoint .
COPY app .

RUN chmod -R u+x /app && \
    chmod u+x /sealights/SL.DotNet && \
    chgrp -R 0 /app /sealights && \
    chmod -R g=u /app /sealights /etc/passwd

USER 10001

# uid_entrypoint registers the random OpenShift UID in /etc/passwd, then builds and exec's the
# SeaLights cdAgent command — kept in the shell so it can read runtime env (e.g. IMAGE_TAG -> build
# name). See uid_entrypoint for the full invocation. App identity comes from the SL_GENERAL_*/SL_LABID
# env above; --proxy routes only the agent's *.sealights.co traffic through vz-proxy, not the app's.
# Single ENTRYPOINT — having two silently drops the first.
ENTRYPOINT ["uid_entrypoint"]
