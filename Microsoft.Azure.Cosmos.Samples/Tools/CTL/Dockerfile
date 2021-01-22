FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as build
COPY ../../../. /usr/src/.
RUN dotnet publish usr/src/CosmosCTL.csproj -c Release -o /usr/app/
COPY run_ctl.sh /usr/app
WORKDIR /usr/app
RUN chmod +x run_ctl.sh
CMD /bin/sh ./run_ctl.sh