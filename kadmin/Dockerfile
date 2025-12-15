FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Cache restore
COPY kadmin/kadmin.csproj kadmin/
COPY backend/KSignal.API/KSignal.API.csproj backend/KSignal.API/
COPY backend/Kalshi.Api/Kalshi.Api.csproj backend/Kalshi.Api/
COPY shared/KSignals.DTO/KSignals.DTO.csproj shared/KSignals.DTO/
RUN dotnet restore kadmin/kadmin.csproj

# Copy full source
COPY backend backend
COPY shared shared
COPY kadmin kadmin

WORKDIR /app/kadmin
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 3012
ENV ASPNETCORE_URLS=http://+:3012

COPY --from=build /out .
ENTRYPOINT ["dotnet", "kadmin.dll"]
