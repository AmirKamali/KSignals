# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["KSignal.API/KSignal.API.csproj", "KSignal.API/"]
COPY ["Kalshi.Api/Kalshi.Api.csproj", "Kalshi.Api/"]
RUN dotnet restore "KSignal.API/KSignal.API.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/KSignal.API"
RUN dotnet build "KSignal.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "KSignal.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 3006
COPY --from=publish /app/publish .

# Set environment variables (can be overridden by docker-compose)
ENV ASPNETCORE_URLS=http://+:3006
ENV Redis__ConnectionString=redis:6379

ENTRYPOINT ["dotnet", "KSignal.API.dll"]
