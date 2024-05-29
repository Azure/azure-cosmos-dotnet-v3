FROM mcr.microsoft.com/dotnet/runtime:6.0
COPY artifacts/CTL/. /usr/app/.
COPY run_ctl.sh /usr/app/.
WORKDIR /usr/app
RUN Icacls run_ctl.sh /grant *S-1-1-0:(RX)
CMD /bin/sh ./run_ctl.sh