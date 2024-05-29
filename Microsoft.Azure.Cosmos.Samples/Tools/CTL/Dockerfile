FROM mcr.microsoft.com/dotnet/runtime:6.0
COPY artifacts/CTL/. /usr/app/.
COPY --chmod=0755 run_ctl.sh /usr/app/.
WORKDIR /usr/app
CMD /bin/sh ./run_ctl.sh