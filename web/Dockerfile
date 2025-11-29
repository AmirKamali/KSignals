# Build stage
# NOTE: Build with the repo root as context so the shared DTO project is available:
#   docker build -f web/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies (include shared DTO project)
COPY web/web_asp.csproj web/
COPY shared/KSignals.DTO/KSignals.DTO.csproj shared/KSignals.DTO/
RUN dotnet restore web/web_asp.csproj

# Copy source
COPY web/. web/
COPY shared/. shared/

# Publish
WORKDIR /src/web
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:3000
# Backend base URL can be overridden at runtime (e.g., -e BACKEND_API_BASE_URL=http://host.docker.internal:3006)
ENV BACKEND_API_BASE_URL=http://backend:3006

EXPOSE 3000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "web_asp.dll"]
