#!/bin/bash

# Script to generate C# API client library from OpenAPI specification
# This script regenerates the Kalshi.Api library from openapi.yaml

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OPENAPI_SPEC="${SCRIPT_DIR}/openapi.yaml"
OUTPUT_DIR="${SCRIPT_DIR}/backend/Kalshi.Api/Generated"
TARGET_DIR="${SCRIPT_DIR}/backend/Kalshi.Api"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Generating C# API client library from OpenAPI specification...${NC}"

# Check if OpenAPI spec exists
if [ ! -f "$OPENAPI_SPEC" ]; then
    echo -e "${RED}Error: OpenAPI specification not found at $OPENAPI_SPEC${NC}"
    exit 1
fi

# Check if openapi-generator-cli is installed
if ! command -v openapi-generator-cli &> /dev/null; then
    echo -e "${YELLOW}openapi-generator-cli not found. Installing...${NC}"
    npm install -g @openapitools/openapi-generator-cli
fi

# Create temporary directory for generation
TEMP_DIR=$(mktemp -d)
trap "rm -rf $TEMP_DIR" EXIT

echo -e "${GREEN}Generating code to temporary directory...${NC}"

# Generate the C# client
openapi-generator-cli generate \
    -i "$OPENAPI_SPEC" \
    -g csharp \
    -o "$TEMP_DIR" \
    --additional-properties=packageName=Kalshi.Api,library=restsharp,netCoreProjectFile=true,optionalAssemblyInfo=false,optionalProjectFile=false,packageVersion=1.0.0 \
    --skip-validate-spec

# Check if generation was successful
if [ ! -d "$TEMP_DIR/src/Kalshi.Api" ]; then
    echo -e "${RED}Error: Code generation failed. Generated files not found.${NC}"
    exit 1
fi

echo -e "${GREEN}Moving generated files to project directory...${NC}"

# Backup existing files (except our custom files)
if [ -d "$TARGET_DIR" ]; then
    echo -e "${YELLOW}Backing up custom files...${NC}"
    
    # Backup custom files
    mkdir -p "$TEMP_DIR/backup"
    [ -f "$TARGET_DIR/KalshiClient.cs" ] && cp "$TARGET_DIR/KalshiClient.cs" "$TEMP_DIR/backup/"
    [ -d "$TARGET_DIR/Authentication" ] && cp -r "$TARGET_DIR/Authentication" "$TEMP_DIR/backup/"
    [ -d "$TARGET_DIR/Configuration" ] && cp -r "$TARGET_DIR/Configuration" "$TEMP_DIR/backup/"
    [ -f "$TARGET_DIR/Kalshi.Api.csproj" ] && cp "$TARGET_DIR/Kalshi.Api.csproj" "$TEMP_DIR/backup/"
fi

# Remove old generated files (keep custom directories)
if [ -d "$TARGET_DIR" ]; then
    echo -e "${YELLOW}Removing old generated files...${NC}"
    rm -rf "$TARGET_DIR/Api"
    rm -rf "$TARGET_DIR/Client"
    rm -rf "$TARGET_DIR/Model"
fi

# Move generated files
echo -e "${GREEN}Copying generated files...${NC}"
cp -r "$TEMP_DIR/src/Kalshi.Api/Api" "$TARGET_DIR/"
cp -r "$TEMP_DIR/src/Kalshi.Api/Client" "$TARGET_DIR/"
cp -r "$TEMP_DIR/src/Kalshi.Api/Model" "$TARGET_DIR/"

# Restore custom files
if [ -d "$TEMP_DIR/backup" ]; then
    echo -e "${GREEN}Restoring custom files...${NC}"
    [ -f "$TEMP_DIR/backup/KalshiClient.cs" ] && cp "$TEMP_DIR/backup/KalshiClient.cs" "$TARGET_DIR/"
    [ -d "$TEMP_DIR/backup/Authentication" ] && cp -r "$TEMP_DIR/backup/Authentication" "$TARGET_DIR/"
    [ -d "$TEMP_DIR/backup/Configuration" ] && cp -r "$TEMP_DIR/backup/Configuration" "$TARGET_DIR/"
    [ -f "$TEMP_DIR/backup/Kalshi.Api.csproj" ] && cp "$TEMP_DIR/backup/Kalshi.Api.csproj" "$TARGET_DIR/"
fi

echo -e "${GREEN}Code generation completed successfully!${NC}"
echo -e "${YELLOW}Note: You may need to rebuild the project: cd backend/Kalshi.Api && dotnet build${NC}"

