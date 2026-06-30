ARG baseimage
FROM $baseimage

# Coverage runs via the cdAgent CLI (SL.DotNet wraps `dotnet Dse.Api.dll` at the ENTRYPOINT),
# not the env-var Profiler-Initiated Collector. cdAgent is the only mode that accepts --proxy, which
# we need to reach *.sealights.co:443 through PNC's egress proxy. cdAgent injects the CORECLR_*
# profiler env into the child dotnet itself, so we must NOT set them container-wide — they would also
# attach the profiler to the SL.DotNet process and collide with cdAgent's own setup.
ENV SL_FEATURES_IDENTIFYMETHODSBYFQN="true"

ARG BUILD_NUMBER
ENV SL_GENERAL_APPNAME="dse_searchapi"
ENV SL_GENERAL_BRANCHNAME="main"
ENV SL_GENERAL_BUILDNAME=$BUILD_NUMBER
# TODO: replace "my_lab" with the real lab ID from the SeaLights dashboard (or drop this line)
ENV SL_LABID="my_lab"
ENV SL_SCAN_BINDIR="/app"
ENV SL_SCAN_INCLUDENAMESPACES_0="Dse.*"
ENV SL_SCAN_INCLUDEASSEMBLIES="Dse.*"

# SL_SESSION_TOKEN is injected at runtime (OpenShift env, sourced from a Secret).
# Do NOT hardcode the token here — this file is committed to git.

USER root

# Proxy needed for Sealights download and runtime connectivity
ARG SEALIGHT_PROXY=http://vz-proxy.pncint.net:8080
ENV http_proxy=${SEALIGHT_PROXY}
ENV HTTP_PROXY=${SEALIGHT_PROXY}
ENV https_proxy=${SEALIGHT_PROXY}
ENV HTTPS_PROXY=${SEALIGHT_PROXY}

# Install the SeaLights .NET Core agent (glibc/RHEL 8 build) into /sealights.
# Provides libSL.DotNet.ProfilerLib.Linux.so referenced by the CORECLR_* paths above.
#
# Proxy: the ADO template passes no proxy build-arg, so it defaults to PNC's egress proxy (the same one
# this pipeline's Sysdig step uses); HTTPS_PROXY still override it via --build-arg.
# -4: agents.sealights.co resolves IPv6-only and the agent has no IPv6 egress (the proxy is reached over IPv4).
# --insecure/--proxy-insecure: the proxy intercepts TLS with a CA the agent doesn't trust and has no cert
# store (or openssl) to add it to — internal download over the trusted VPN. Prefer `--cacert <file>` if a
# corporate CA ever becomes available.
RUN mkdir -p /sealights/logs && \
    proxy=${HTTPS_PROXY} && \
    curl -4 -fsSL --insecure --proxy-insecure ${proxy:+--proxy "$proxy"} -o /tmp/sl-agent.tar.gz \
      https://agents.sealights.co/dotnetcore/latest/sealights-dotnet-agent-linux-self-contained.tar.gz && \
    tar -xzf /tmp/sl-agent.tar.gz --directory /sealights && \
    rm /tmp/sl-agent.tar.gz

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

# uid_entrypoint resolves the (random) OpenShift UID into /etc/passwd, then exec's the agent.
# SL.DotNet cdAgent wraps `dotnet Dse.Api.dll` (--target/--targetArgs), attaches the SeaLights
# profiler to that child, and streams coverage to SeaLights through --proxy. App identity (appName,
# branch, build, lab) comes from the SL_GENERAL_*/SL_LABID env above.
#
# --proxy "system": exec-form ENTRYPOINT does NOT expand ${HTTPS_PROXY} (and unquoted it is invalid
# JSON, which silently degrades to broken shell-form). "system" makes the agent read the HTTPS_PROXY
# env set above at runtime — no hardcoded URL, resolved at start.
# Single ENTRYPOINT — having two silently drops the first.
ENTRYPOINT ["uid_entrypoint", "/sealights/SL.DotNet", "cdAgent", \
    "--target", "dotnet", "--targetArgs", "Dse.Api.dll", "--workingDir", "/app", \
    "--binDir", "/app", "--includeNamespace", "Dse.*", \
    "--proxy", "system"]
