FROM mcr.microsoft.com/dotnet/core/sdk:3.1
COPY . /usr/src/.
RUN dotnet publish usr/src/Microsoft.Azure.Cosmos.Samples/Tools/CTL/CosmosCTL.csproj -c Release -o /usr/app/
COPY Microsoft.Azure.Cosmos.Samples/Tools/CTL/run_ctl.sh /usr/app
WORKDIR /usr/app
RUN chmod +x run_ctl.sh
CMD /bin/sh ./run_ctl.sh