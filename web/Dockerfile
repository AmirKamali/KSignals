# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src/web_asp

COPY web_asp.csproj .
RUN dotnet restore web_asp.csproj

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:3000
# Backend base URL can be overridden at runtime (e.g., -e BACKEND_API_BASE_URL=http://host.docker.internal:5271)
ENV BACKEND_API_BASE_URL=http://localhost:3006

EXPOSE 3000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "web_asp.dll"]
