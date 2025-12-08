#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Trap to cleanup background processes on exit
cleanup() {
    echo -e "\n${YELLOW}Shutting down frontend...${NC}"
    if [ ! -z "${FRONTEND_PID:-}" ]; then
        kill $FRONTEND_PID 2>/dev/null
    fi
    if [ ! -z "${TAIL_PID:-}" ]; then
        kill $TAIL_PID 2>/dev/null
    fi
    # Kill any remaining dotnet watch processes
    pkill -f "dotnet watch.*web_asp" 2>/dev/null
    echo -e "${GREEN}Frontend stopped${NC}"
    exit 0
}

trap cleanup SIGINT SIGTERM

# Check if port is already in use
check_port() {
    local port=$1
    if lsof -Pi :$port -sTCP:LISTEN -t >/dev/null 2>&1 ; then
        echo -e "${RED}Error: Port $port is already in use${NC}"
        lsof -i :$port
        return 1
    fi
    return 0
}

echo -e "${BLUE}╔═══════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   Kalshi Signals Frontend (ASP.NET)  ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════╝${NC}"
echo ""

# Check port
echo -e "${YELLOW}Checking port 3011...${NC}"
check_port 3011 || exit 1
echo -e "${GREEN}✓ Port available${NC}"
echo ""

# Set environment variables
export BACKEND_API_BASE_URL="http://localhost:3006"
export JWT_SECRET="vS2d7BbiCp5AKQBaKnKzhuDTamgh1g+Sw0vFkbQ/qKxRnEqUlenrYH4ZCDk5tUoW"

# Workaround for .NET 10 RC file watcher issues on macOS
# Use polling instead of native FSEvents to prevent PAL_SEHException crashes
export DOTNET_USE_POLLING_FILE_WATCHER=1

# Create logs directory if it doesn't exist
mkdir -p "${ROOT_DIR}/logs"

# Start Frontend with watch mode
echo -e "${BLUE}Starting Frontend with hot reload...${NC}"
cd "${ROOT_DIR}/web"
dotnet watch run --non-interactive > "${ROOT_DIR}/logs/frontend.log" 2>&1 &
FRONTEND_PID=$!
cd "${ROOT_DIR}"
echo -e "${GREEN}✓ Frontend starting (PID: $FRONTEND_PID)${NC}"
echo -e "  ${BLUE}→${NC} http://localhost:3011"
echo ""

echo -e "${GREEN}╔═══════════════════════════════════════╗${NC}"
echo -e "${GREEN}║       Frontend Running!               ║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════╝${NC}"
echo ""
echo -e "Frontend:  ${BLUE}http://localhost:3011${NC}"
echo ""
echo -e "${YELLOW}Logs:${NC}"
echo -e "  ${BLUE}tail -f logs/frontend.log${NC}"
echo ""
echo -e "${YELLOW}Press Ctrl+C to stop the frontend${NC}"
echo ""

# Follow logs in the terminal
tail -f "${ROOT_DIR}/logs/frontend.log" 2>/dev/null &
TAIL_PID=$!

# Wait for process
wait $FRONTEND_PID
