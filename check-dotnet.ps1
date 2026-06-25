$ErrorActionPreference = "Stop"

$candidates = @(
    "$env:ProgramFiles\dotnet\dotnet.exe",
    "${env:ProgramFiles(x86)}\dotnet\dotnet.exe"
) | Where-Object { Test-Path $_ }

Write-Host "Detected dotnet executables:" -ForegroundColor Cyan
foreach ($candidate in $candidates) {
    Write-Host " - $candidate"
    & $candidate --version
    & $candidate --list-sdks
    Write-Host ""
}

$commandDotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if ($commandDotnet) {
    Write-Host "PATH dotnet command:" -ForegroundColor Cyan
    Write-Host " - $commandDotnet"
    & $commandDotnet --version
    & $commandDotnet --list-sdks
}
else {
    Write-Error "dotnet command not found in PATH."
    exit 1
}
