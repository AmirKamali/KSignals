FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Restore with only project files to keep cache hot
COPY backend/KSignal.API/KSignal.API.csproj backend/KSignal.API/
COPY backend/Kalshi.Api/Kalshi.Api.csproj backend/Kalshi.Api/
COPY shared/KSignals.DTO/KSignals.DTO.csproj shared/KSignals.DTO/
RUN dotnet restore backend/KSignal.API/KSignal.API.csproj

# Copy the rest of the source
COPY backend backend
COPY shared shared

# Publish directly to an output folder
WORKDIR /app/backend/KSignal.API
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 3006
ENV ASPNETCORE_URLS=http://+:3006
ENV Redis__ConnectionString=redis:6379

COPY --from=build /out .
ENTRYPOINT ["dotnet", "KSignal.API.dll"]
