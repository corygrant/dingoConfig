# Extract version from api.csproj
$version = Select-String -Path "api/api.csproj" -Pattern "<Version>(.*?)</Version>" |
    ForEach-Object { $_.Matches.Groups[1].Value }

if (-not $version) {
    Write-Error "Error: Could not extract version from api/api.csproj"
    exit 1
}

Write-Host "Publishing dingoConfig version $version" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# Create output directory
$outputDir = "publish/dingoConfig-$version"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Common publish arguments
$project = "api/api.csproj"
$config = "Release"
$publishArgs = "--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true"

# Publish for Windows (x64)
Write-Host ""
Write-Host "Building for Windows (x64)..." -ForegroundColor Yellow
dotnet publish $project -c $config -r win-x64 @publishArgs -o "$outputDir/win-x64"
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Windows (x64) build complete" -ForegroundColor Green
} else {
    Write-Host "✗ Windows (x64) build failed" -ForegroundColor Red
}

# Publish for Windows (arm64)
Write-Host ""
Write-Host "Building for Windows (arm64)..." -ForegroundColor Yellow
dotnet publish $project -c $config -r win-arm64 @publishArgs -o "$outputDir/win-arm64"
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Windows (arm64) build complete" -ForegroundColor Green
} else {
    Write-Host "✗ Windows (arm64) build failed" -ForegroundColor Red
}

# Publish for Linux (x64)
Write-Host ""
Write-Host "Building for Linux (x64)..." -ForegroundColor Yellow
dotnet publish $project -c $config -r linux-x64 @publishArgs -o "$outputDir/linux-x64"
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Linux (x64) build complete" -ForegroundColor Green
} else {
    Write-Host "✗ Linux (x64) build failed" -ForegroundColor Red
}

# Publish for Linux (arm64)
Write-Host ""
Write-Host "Building for Linux (arm64)..." -ForegroundColor Yellow
dotnet publish $project -c $config -r linux-arm64 @publishArgs -o "$outputDir/linux-arm64"
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Linux (arm64) build complete" -ForegroundColor Green
} else {
    Write-Host "✗ Linux (arm64) build failed" -ForegroundColor Red
}

# Publish for macOS (x64 - Intel)
Write-Host ""
Write-Host "Building for macOS (x64 - Intel)..." -ForegroundColor Yellow
dotnet publish $project -c $config -r osx-x64 @publishArgs -o "$outputDir/osx-x64"
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ macOS Intel build complete" -ForegroundColor Green
} else {
    Write-Host "✗ macOS Intel build failed" -ForegroundColor Red
}

# Publish for macOS (arm64 - Apple Silicon)
Write-Host ""
Write-Host "Building for macOS (arm64 - Apple Silicon)..." -ForegroundColor Yellow
dotnet publish $project -c $config -r osx-arm64 @publishArgs -o "$outputDir/osx-arm64"
if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ macOS Apple Silicon build complete" -ForegroundColor Green
} else {
    Write-Host "✗ macOS Apple Silicon build failed" -ForegroundColor Red
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Creating zip archives..." -ForegroundColor Yellow
Write-Host ""

# Create zip files for each platform
Write-Host "Zipping Windows (x64)..."
Compress-Archive -Path "$outputDir/win-x64/*" -DestinationPath "$outputDir/dingoConfig-$version-win-x64.zip" -Force
Write-Host "✓ dingoConfig-$version-win-x64.zip created" -ForegroundColor Green

Write-Host "Zipping Windows (arm64)..."
Compress-Archive -Path "$outputDir/win-arm64/*" -DestinationPath "$outputDir/dingoConfig-$version-win-arm64.zip" -Force
Write-Host "✓ dingoConfig-$version-win-arm64.zip created" -ForegroundColor Green

Write-Host "Zipping Linux (x64)..."
Compress-Archive -Path "$outputDir/linux-x64/*" -DestinationPath "$outputDir/dingoConfig-$version-linux-x64.zip" -Force
Write-Host "✓ dingoConfig-$version-linux-x64.zip created" -ForegroundColor Green

Write-Host "Zipping Linux (arm64)..."
Compress-Archive -Path "$outputDir/linux-arm64/*" -DestinationPath "$outputDir/dingoConfig-$version-linux-arm64.zip" -Force
Write-Host "✓ dingoConfig-$version-linux-arm64.zip created" -ForegroundColor Green

Write-Host "Zipping macOS (x64)..."
Compress-Archive -Path "$outputDir/osx-x64/*" -DestinationPath "$outputDir/dingoConfig-$version-osx-x64.zip" -Force
Write-Host "✓ dingoConfig-$version-osx-x64.zip created" -ForegroundColor Green

Write-Host "Zipping macOS (arm64)..."
Compress-Archive -Path "$outputDir/osx-arm64/*" -DestinationPath "$outputDir/dingoConfig-$version-osx-arm64.zip" -Force
Write-Host "✓ dingoConfig-$version-osx-arm64.zip created" -ForegroundColor Green

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "All builds complete!" -ForegroundColor Green
Write-Host "Output directory: $outputDir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Zip files:" -ForegroundColor Cyan
Write-Host "  Windows (x64):         $outputDir/dingoConfig-$version-win-x64.zip"
Write-Host "  Windows (arm64):       $outputDir/dingoConfig-$version-win-arm64.zip"
Write-Host "  Linux (x64):           $outputDir/dingoConfig-$version-linux-x64.zip"
Write-Host "  Linux (arm64):         $outputDir/dingoConfig-$version-linux-arm64.zip"
Write-Host "  macOS (Intel):         $outputDir/dingoConfig-$version-osx-x64.zip"
Write-Host "  macOS (Apple Silicon): $outputDir/dingoConfig-$version-osx-arm64.zip"
