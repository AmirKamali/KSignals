#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Starting ASP.NET frontend from ${ROOT_DIR}/web ..."
exec dotnet run --project "${ROOT_DIR}/web/web_asp.csproj" --urls "http://localhost:3011" "$@"
