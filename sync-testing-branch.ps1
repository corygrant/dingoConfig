param(
    [string]$RepoUrl = "https://github.com/corygrant/dingoConfig.git",
    [string]$Branch = "testing",
    [string]$TargetPath = "$HOME\dingoConfig"
)

$ErrorActionPreference = "Stop"

Write-Host "Syncing repository from $RepoUrl ($Branch) to $TargetPath" -ForegroundColor Cyan

if (-not (Test-Path $TargetPath)) {
    git clone --branch $Branch --single-branch $RepoUrl $TargetPath
    Write-Host "Clone complete." -ForegroundColor Green
    exit 0
}

Push-Location $TargetPath
try {
    if (-not (Test-Path ".git")) {
        throw "Target path exists but is not a git repository: $TargetPath"
    }

    $remoteUrl = git remote get-url origin
    if ($remoteUrl -ne $RepoUrl) {
        throw "Origin remote mismatch. Expected '$RepoUrl' but got '$remoteUrl'"
    }

    git fetch origin
    git checkout $Branch
    git pull origin $Branch

    Write-Host "Repository synced to latest origin/$Branch." -ForegroundColor Green
}
finally {
    Pop-Location
}
