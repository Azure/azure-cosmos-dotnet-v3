FROM mcr.microsoft.com/dotnet/core/runtime:3.1
COPY artifacts/CTL/. /usr/app/.
COPY run_ctl.sh /usr/app/.
WORKDIR /usr/app
RUN chmod +x run_ctl.sh
CMD /bin/sh ./run_ctl.sh