FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get -y update && \
    apt-get install -y --no-install-recommends \
    apt-utils libgdiplus libc6-dev lsof net-tools \
    wget sqlite3 iputils-ping inetutils-tools \
    curl libfontconfig1 && \
    apt-get autoclean && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app

EXPOSE 8080
EXPOSE 8081
EXPOSE 1883
EXPOSE 8883
EXPOSE 5683/udp
EXPOSE 5684/udp
EXPOSE 502

COPY publish/ .

VOLUME ["/app/security/"]
VOLUME ["/app/.data/"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/api/HealthChecks || exit 1

ENTRYPOINT ["dotnet", "IoTSharp.dll"]