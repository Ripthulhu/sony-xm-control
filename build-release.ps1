param(
    [string]$Version = "v1.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$packageName = "SonyXmControl-$Version"
$artifactRoot = Join-Path $root "artifacts"
$packageRoot = Join-Path $artifactRoot $packageName
$zipPath = Join-Path $artifactRoot "$packageName.zip"

if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Push-Location (Join-Path $root "src\c")
try {
    & .\build.bat
    if ($LASTEXITCODE -ne 0) {
        $backendExe = Join-Path $root "src\c\xm5ctl.exe"
        if (Test-Path -LiteralPath $backendExe) {
            Write-Warning "Backend compiler was not available; using existing src\c\xm5ctl.exe."
        } else {
            throw "Backend build failed with exit code $LASTEXITCODE."
        }
    }
}
finally {
    Pop-Location
}

Push-Location (Join-Path $root "src\ui")
try {
    & .\build-ui.bat
    if ($LASTEXITCODE -ne 0) { throw "UI build failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}

New-Item -ItemType Directory -Force (Join-Path $packageRoot "c"), (Join-Path $packageRoot "ui") | Out-Null
Copy-Item -LiteralPath (Join-Path $root "src\c\xm5ctl.exe") -Destination (Join-Path $packageRoot "c\xm5ctl.exe") -Force
Copy-Item -LiteralPath (Join-Path $root "src\ui\xm5ui.exe") -Destination (Join-Path $packageRoot "ui\xm5ui.exe") -Force
Copy-Item -LiteralPath (Join-Path $root "src\ui\assets") -Destination (Join-Path $packageRoot "ui\assets") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root "docs") -Destination (Join-Path $packageRoot "docs") -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination (Join-Path $packageRoot "README.md") -Force

Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -Force
Get-Item -LiteralPath $zipPath
