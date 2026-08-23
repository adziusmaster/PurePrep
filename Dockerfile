# PurePrep AI backend — multi-stage build for a Linux VPS (Hetzner).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/PurePrep.Core/ ./PurePrep.Core/
COPY src/PurePrep.Server/ ./PurePrep.Server/
RUN dotnet restore PurePrep.Server/PurePrep.Server.csproj \
      --source https://api.nuget.org/v3/index.json \
 && dotnet publish PurePrep.Server/PurePrep.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# SQLite database persists on a mounted volume.
VOLUME /data
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Db="Data Source=/data/pureprep.server.db"
EXPOSE 8080
# Secrets (Gemini__ApiKey, Dev__Secret, Play creds) are supplied at runtime via env vars.
ENTRYPOINT ["dotnet", "PurePrep.Server.dll"]
