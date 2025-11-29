#!/bin/bash

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Trap to cleanup background processes on exit
cleanup() {
    echo -e "\n${YELLOW}Shutting down services...${NC}"
    if [ ! -z "$BACKEND_PID" ]; then
        kill $BACKEND_PID 2>/dev/null
    fi
    if [ ! -z "$FRONTEND_PID" ]; then
        kill $FRONTEND_PID 2>/dev/null
    fi
    # Kill any remaining dotnet watch processes
    pkill -f "dotnet watch" 2>/dev/null
    echo -e "${GREEN}Services stopped${NC}"
    exit 0
}

trap cleanup SIGINT SIGTERM

# Check if ports are already in use
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
echo -e "${BLUE}║   Kalshi Signals Development Mode    ║${NC}"
echo -e "${BLUE}╔═══════════════════════════════════════╗${NC}"
echo ""

# Check ports
echo -e "${YELLOW}Checking ports...${NC}"
check_port 3006 || exit 1
check_port 3011 || exit 1
echo -e "${GREEN}✓ Ports available${NC}"
echo ""

# Set environment variables
export BACKEND_API_BASE_URL="http://localhost:3006"
export JWT_SECRET="vS2d7BbiCp5AKQBaKnKzhuDTamgh1g+Sw0vFkbQ/qKxRnEqUlenrYH4ZCDk5tUoW"

# Create logs directory if it doesn't exist
mkdir -p logs

# Get absolute path to project root
PROJECT_ROOT="$(pwd)"

# Start Backend API
echo -e "${BLUE}Starting Backend API...${NC}"
cd backend/KSignal.API
dotnet watch run --non-interactive --urls "http://localhost:3006" > "$PROJECT_ROOT/logs/backend.log" 2>&1 &
BACKEND_PID=$!
cd "$PROJECT_ROOT"
echo -e "${GREEN}✓ Backend API starting (PID: $BACKEND_PID)${NC}"
echo -e "  ${BLUE}→${NC} http://localhost:3006"
echo -e "  ${BLUE}→${NC} Swagger: http://localhost:3006/swagger"
echo ""

# Wait a bit for backend to start
sleep 3

# Start Frontend
echo -e "${BLUE}Starting Frontend...${NC}"
cd web
dotnet watch run --non-interactive > "$PROJECT_ROOT/logs/frontend.log" 2>&1 &
FRONTEND_PID=$!
cd "$PROJECT_ROOT"
echo -e "${GREEN}✓ Frontend starting (PID: $FRONTEND_PID)${NC}"
echo -e "  ${BLUE}→${NC} http://localhost:3011"
echo ""

echo -e "${GREEN}╔═══════════════════════════════════════╗${NC}"
echo -e "${GREEN}║       Services Running!               ║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════╝${NC}"
echo ""
echo -e "Frontend:  ${BLUE}http://localhost:3011${NC}"
echo -e "Backend:   ${BLUE}http://localhost:3006${NC}"
echo -e "Swagger:   ${BLUE}http://localhost:3006/swagger${NC}"
echo ""
echo -e "${YELLOW}Logs:${NC}"
echo -e "  Backend:  ${BLUE}tail -f logs/backend.log${NC}"
echo -e "  Frontend: ${BLUE}tail -f logs/frontend.log${NC}"
echo ""
echo -e "${YELLOW}Press Ctrl+C to stop all services${NC}"
echo ""

# Follow logs in the terminal (combined output)
tail -f logs/backend.log logs/frontend.log 2>/dev/null &
TAIL_PID=$!

# Wait for processes
wait $BACKEND_PID $FRONTEND_PID
