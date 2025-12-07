#!/bin/bash

# Backend Test Runner Script
# This script runs all backend unit tests with various options

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default values
VERBOSITY="normal"
COVERAGE=false
WATCH=false

# Print usage
usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -v, --verbose       Run tests with detailed output"
    echo "  -q, --quiet         Run tests with minimal output"
    echo "  -c, --coverage      Run tests with code coverage"
    echo "  -w, --watch         Run tests in watch mode"
    echo "  -h, --help          Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                  # Run tests with normal output"
    echo "  $0 --verbose        # Run tests with detailed output"
    echo "  $0 --coverage       # Run tests with code coverage"
    echo "  $0 --watch          # Run tests in watch mode"
    exit 0
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--verbose)
            VERBOSITY="detailed"
            shift
            ;;
        -q|--quiet)
            VERBOSITY="quiet"
            shift
            ;;
        -c|--coverage)
            COVERAGE=true
            shift
            ;;
        -w|--watch)
            WATCH=true
            shift
            ;;
        -h|--help)
            usage
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            usage
            ;;
    esac
done

# Print header
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}   KSignal Backend Test Runner${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Change to project root directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

# Verify test project exists
if [ ! -f "backend/KSignal.API.Tests/KSignal.API.Tests.csproj" ]; then
    echo -e "${RED}Error: Test project not found at backend/KSignal.API.Tests/KSignal.API.Tests.csproj${NC}"
    exit 1
fi

echo -e "${YELLOW}Building test project...${NC}"
dotnet build backend/KSignal.API.Tests/KSignal.API.Tests.csproj --configuration Debug

if [ $? -ne 0 ]; then
    echo -e "${RED}Build failed!${NC}"
    exit 1
fi

echo -e "${GREEN}Build successful!${NC}"
echo ""

# Run tests based on options
if [ "$WATCH" = true ]; then
    echo -e "${YELLOW}Running tests in watch mode...${NC}"
    echo -e "${YELLOW}Press Ctrl+C to exit${NC}"
    echo ""
    dotnet watch test backend/KSignal.API.Tests/KSignal.API.Tests.csproj --verbosity $VERBOSITY
elif [ "$COVERAGE" = true ]; then
    echo -e "${YELLOW}Running tests with code coverage...${NC}"
    echo ""
    dotnet test backend/KSignal.API.Tests/KSignal.API.Tests.csproj \
        --configuration Debug \
        --verbosity $VERBOSITY \
        --collect:"XPlat Code Coverage" \
        --results-directory ./TestResults

    if [ $? -eq 0 ]; then
        echo ""
        echo -e "${GREEN}Tests passed with coverage!${NC}"
        echo -e "${BLUE}Coverage reports saved to ./TestResults/${NC}"

        # Find the latest coverage file
        COVERAGE_FILE=$(find ./TestResults -name "coverage.cobertura.xml" -type f -print0 | xargs -0 ls -t | head -n 1)
        if [ -n "$COVERAGE_FILE" ]; then
            echo -e "${BLUE}Latest coverage file: $COVERAGE_FILE${NC}"
        fi
    else
        echo -e "${RED}Tests failed!${NC}"
        exit 1
    fi
else
    echo -e "${YELLOW}Running tests...${NC}"
    echo ""
    dotnet test backend/KSignal.API.Tests/KSignal.API.Tests.csproj \
        --configuration Debug \
        --verbosity $VERBOSITY

    if [ $? -eq 0 ]; then
        echo ""
        echo -e "${GREEN}========================================${NC}"
        echo -e "${GREEN}   All tests passed! ✓${NC}"
        echo -e "${GREEN}========================================${NC}"
    else
        echo ""
        echo -e "${RED}========================================${NC}"
        echo -e "${RED}   Tests failed! ✗${NC}"
        echo -e "${RED}========================================${NC}"
        exit 1
    fi
fi
