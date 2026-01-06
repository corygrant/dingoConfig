#!/bin/bash

# Extract version from api.csproj
VERSION=$(grep -oP '(?<=<Version>)[^<]+' api/api.csproj)

if [ -z "$VERSION" ]; then
    echo "Error: Could not extract version from api/api.csproj"
    exit 1
fi

echo "Publishing dingoConfig version $VERSION"
echo "========================================="

# Create output directory
OUTPUT_DIR="publish/dingoConfig-$VERSION"
mkdir -p "$OUTPUT_DIR"

# Common publish arguments
PROJECT="api/api.csproj"
CONFIG="Release"
PUBLISH_ARGS="--self-contained true"

# Publish for Windows (x64)
echo ""
echo "Building for Windows (x64)..."
dotnet publish "$PROJECT" -c "$CONFIG" -r win-x64 $PUBLISH_ARGS -o "$OUTPUT_DIR/win-x64"
if [ $? -eq 0 ]; then
    echo "[OK] Windows (x64) build complete"
else
    echo "[FAILED] Windows (x64) build failed"
fi

# Publish for Windows (arm64)
echo ""
echo "Building for Windows (arm64)..."
dotnet publish "$PROJECT" -c "$CONFIG" -r win-arm64 $PUBLISH_ARGS -o "$OUTPUT_DIR/win-arm64"
if [ $? -eq 0 ]; then
    echo "[OK] Windows (arm64) build complete"
else
    echo "[FAILED] Windows (arm64) build failed"
fi

# Publish for Linux (x64)
echo ""
echo "Building for Linux (x64)..."
dotnet publish "$PROJECT" -c "$CONFIG" -r linux-x64 $PUBLISH_ARGS -o "$OUTPUT_DIR/linux-x64"
if [ $? -eq 0 ]; then
    echo "[OK] Linux (x64) build complete"
else
    echo "[FAILED] Linux (x64) build failed"
fi

# Publish for Linux (arm64)
echo ""
echo "Building for Linux (arm64)..."
dotnet publish "$PROJECT" -c "$CONFIG" -r linux-arm64 $PUBLISH_ARGS -o "$OUTPUT_DIR/linux-arm64"
if [ $? -eq 0 ]; then
    echo "[OK] Linux (arm64) build complete"
else
    echo "[FAILED] Linux (arm64) build failed"
fi

# Publish for macOS (x64 - Intel)
echo ""
echo "Building for macOS (x64 - Intel)..."
dotnet publish "$PROJECT" -c "$CONFIG" -r osx-x64 $PUBLISH_ARGS -o "$OUTPUT_DIR/osx-x64"
if [ $? -eq 0 ]; then
    echo "[OK] macOS Intel build complete"
else
    echo "[FAILED] macOS Intel build failed"
fi

# Publish for macOS (arm64 - Apple Silicon)
echo ""
echo "Building for macOS (arm64 - Apple Silicon)..."
dotnet publish "$PROJECT" -c "$CONFIG" -r osx-arm64 $PUBLISH_ARGS -o "$OUTPUT_DIR/osx-arm64"
if [ $? -eq 0 ]; then
    echo "[OK] macOS Apple Silicon build complete"
else
    echo "[FAILED] macOS Apple Silicon build failed"
fi

echo ""
echo "========================================="
echo "Creating zip archives..."
echo ""

# Create zip files for each platform
cd "$OUTPUT_DIR"

echo "Zipping Windows (x64)..."
zip -r "dingoConfig-$VERSION-win-x64.zip" win-x64/ > /dev/null
echo "[OK] dingoConfig-$VERSION-win-x64.zip created"

echo "Zipping Windows (arm64)..."
zip -r "dingoConfig-$VERSION-win-arm64.zip" win-arm64/ > /dev/null
echo "[OK] dingoConfig-$VERSION-win-arm64.zip created"

echo "Zipping Linux (x64)..."
zip -r "dingoConfig-$VERSION-linux-x64.zip" linux-x64/ > /dev/null
echo "[OK] dingoConfig-$VERSION-linux-x64.zip created"

echo "Zipping Linux (arm64)..."
zip -r "dingoConfig-$VERSION-linux-arm64.zip" linux-arm64/ > /dev/null
echo "[OK] dingoConfig-$VERSION-linux-arm64.zip created"

echo "Zipping macOS (x64)..."
zip -r "dingoConfig-$VERSION-osx-x64.zip" osx-x64/ > /dev/null
echo "[OK] dingoConfig-$VERSION-osx-x64.zip created"

echo "Zipping macOS (arm64)..."
zip -r "dingoConfig-$VERSION-osx-arm64.zip" osx-arm64/ > /dev/null
echo "[OK] dingoConfig-$VERSION-osx-arm64.zip created"

cd - > /dev/null

echo ""
echo "========================================="
echo "All builds complete!"
echo "Output directory: $OUTPUT_DIR"
echo ""
echo "Zip files:"
echo "  Windows (x64):         $OUTPUT_DIR/dingoConfig-$VERSION-win-x64.zip"
echo "  Windows (arm64):       $OUTPUT_DIR/dingoConfig-$VERSION-win-arm64.zip"
echo "  Linux (x64):           $OUTPUT_DIR/dingoConfig-$VERSION-linux-x64.zip"
echo "  Linux (arm64):         $OUTPUT_DIR/dingoConfig-$VERSION-linux-arm64.zip"
echo "  macOS (Intel):         $OUTPUT_DIR/dingoConfig-$VERSION-osx-x64.zip"
echo "  macOS (Apple Silicon): $OUTPUT_DIR/dingoConfig-$VERSION-osx-arm64.zip"
