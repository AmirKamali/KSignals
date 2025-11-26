#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Starting ASP.NET frontend from ${ROOT_DIR}/web_asp ..."
exec dotnet run --project "${ROOT_DIR}/web_asp/web_asp.csproj" --urls "http://localhost:5000" "$@"
