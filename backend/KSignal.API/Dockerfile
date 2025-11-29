# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies (include shared DTO project)
COPY backend/KSignal.API/KSignal.API.csproj backend/KSignal.API/
COPY backend/Kalshi.Api/Kalshi.Api.csproj backend/Kalshi.Api/
COPY shared/KSignals.DTO/KSignals.DTO.csproj shared/KSignals.DTO/
RUN dotnet restore backend/KSignal.API/KSignal.API.csproj

# Copy source
COPY backend/. backend/
COPY shared/. shared/

# Publish stage
WORKDIR /src/backend/KSignal.API
RUN dotnet publish "KSignal.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 3006
COPY --from=build /app/publish .

# Set environment variables (can be overridden by docker-compose)
ENV ASPNETCORE_URLS=http://+:3006
ENV Redis__ConnectionString=redis:6379

ENTRYPOINT ["dotnet", "KSignal.API.dll"]
