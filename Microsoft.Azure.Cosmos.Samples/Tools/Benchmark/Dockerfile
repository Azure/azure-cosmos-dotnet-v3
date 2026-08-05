# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
#
# Prebuilt CosmosBenchmark image, following the CTL Dockerfile + ACR-publish pattern
# (Tools/CTL/Dockerfile, azure-pipelines-ctl-publishing.yml). Built and pushed to ACR on
# every main merge by azure-pipelines-benchmark-publishing.yml so the perf / DR-drill infra
# can pull a published image instead of building the benchmark at startup.
#
# linux/amd64 is a HARD constraint (the perf deploy host pairs this with the Pyroscope .NET
# profiler, which supports linux/amd64 only). The base runtime tag matches the benchmark's
# net6.0 target framework.
#
# The pipeline publishes the benchmark to <context>/artifacts and this image copies it in.
# The ENTRYPOINT forwards all CLI args to the benchmark, e.g.:
#   docker run <image> -e <endpoint> -w ReadItem --duration PT8H \
#       --metrics-sink adx --adx-metrics-uri https://ingest-<cluster>.kusto.windows.net \
#       --sdk-version 3.46.0
ARG DOTNET_TAG=6.0

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/runtime:${DOTNET_TAG}

COPY artifacts/. /usr/app/.
WORKDIR /usr/app

ENTRYPOINT ["dotnet", "CosmosBenchmark.dll"]
