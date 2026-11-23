# Build context: the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

# Restore first, so the package layer is cached while only source files change.
COPY Directory.Build.props ./
COPY src/LegacyLens.Core/LegacyLens.Core.csproj src/LegacyLens.Core/
COPY src/LegacyLens.Infrastructure/LegacyLens.Infrastructure.csproj src/LegacyLens.Infrastructure/
COPY src/LegacyLens.Api/LegacyLens.Api.csproj src/LegacyLens.Api/
RUN dotnet restore src/LegacyLens.Api/LegacyLens.Api.csproj

COPY src/ src/
COPY knowledge/ knowledge/
RUN dotnet publish src/LegacyLens.Api/LegacyLens.Api.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Run as the non-root user that the .NET images provide.
USER app
ENTRYPOINT ["dotnet", "LegacyLens.Api.dll"]
