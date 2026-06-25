param(
    [string]$SolutionPath = ".\dingoConfig.sln"
)

$ErrorActionPreference = "Stop"

function Resolve-DotnetWithNet10 {
    $candidates = @(
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "${env:ProgramFiles(x86)}\dotnet\dotnet.exe"
    ) | Where-Object { Test-Path $_ }

    foreach ($candidate in $candidates) {
        $sdks = & $candidate --list-sdks 2>$null
        if ($LASTEXITCODE -ne 0) { continue }
        if ($sdks -match '^\s*10\.') {
            return $candidate
        }
    }

    $commandDotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if ($commandDotnet) {
        $sdks = & $commandDotnet --list-sdks 2>$null
        if (($LASTEXITCODE -eq 0) -and ($sdks -match '^\s*10\.')) {
            return $commandDotnet
        }
    }

    return $null
}

$dotnetExe = Resolve-DotnetWithNet10
if (-not $dotnetExe) {
    Write-Error "No .NET 10 SDK found. Install .NET 10 SDK and ensure it is available under Program Files\dotnet."
    exit 1
}

Write-Host "Using dotnet: $dotnetExe" -ForegroundColor Cyan
$sdkVersion = (& $dotnetExe --version).Trim()
Write-Host "Detected SDK: $sdkVersion" -ForegroundColor Cyan

Write-Host "Restoring packages..." -ForegroundColor Cyan
& $dotnetExe restore $SolutionPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet restore failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Building solution..." -ForegroundColor Cyan
& $dotnetExe build $SolutionPath --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Build completed successfully." -ForegroundColor Green
