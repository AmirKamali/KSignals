#!/usr/bin/env bash

# Kill any running dotnet processes hosting the ASP.NET frontend (web_asp.dll) or holding port 3011

pids=""

# Try to find by process name first
if command -v pgrep >/dev/null 2>&1; then
  pids=$(pgrep -f "dotnet .*web_asp.dll" || true)
fi

# If none found, try by port 3011 (macOS lsof)
if [ -z "$pids" ] && command -v lsof >/dev/null 2>&1; then
  pids=$(lsof -ti tcp:3011 2>/dev/null | tr '\n' ' ')
fi

if [ -z "$pids" ]; then
  echo "No running web_asp processes found."
  exit 0
fi

echo "Killing web_asp processes: $pids"
# Try graceful first, then force if still present
kill $pids 2>/dev/null || true

if command -v lsof >/dev/null 2>&1; then
  remaining=$(lsof -ti tcp:3011 2>/dev/null | tr '\n' ' ')
  if [ -n "$remaining" ]; then
    echo "Force killing remaining processes on port 3011: $remaining"
    kill -9 $remaining 2>/dev/null || true
  fi
fi
