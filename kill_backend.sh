#!/bin/bash

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

PORT=3006

echo -e "${YELLOW}Checking for processes on port ${PORT}...${NC}"

# Get PIDs listening on the port
PIDS=$(lsof -ti :${PORT})

if [ -z "$PIDS" ]; then
    echo -e "${GREEN}No processes found listening on port ${PORT}${NC}"
    exit 0
fi

echo -e "${RED}Found processes listening on port ${PORT}:${NC}"
lsof -i :${PORT}
echo ""

# Kill the processes
echo -e "${YELLOW}Killing processes...${NC}"
for PID in $PIDS; do
    echo -e "  Killing PID: ${PID}"
    kill -9 $PID 2>/dev/null
done

echo ""
echo -e "${GREEN}✓ All processes on port ${PORT} have been killed${NC}"

# Verify
sleep 1
REMAINING=$(lsof -ti :${PORT})
if [ -z "$REMAINING" ]; then
    echo -e "${GREEN}✓ Port ${PORT} is now free${NC}"
else
    echo -e "${RED}⚠ Warning: Some processes may still be running on port ${PORT}${NC}"
    lsof -i :${PORT}
fi
