param(
    [Parameter(Mandatory = $true)][string]$Package,
    [Parameter(Mandatory = $true)][string]$Pfx,
    [string]$Password = ""
)

# MSIX signatures are only valid when the signing certificate subject matches the
# Publisher in AppxManifest.xml ("CN=all-the-johnson").
$ErrorActionPreference = "Stop"

$signTool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if (-not $signTool) {
    throw "signtool.exe not found. Install the Windows SDK (Signing Tools) first."
}

$args = @("sign", "/fd", "SHA256", "/a", "/f", $Pfx)
if ($Password) { $args += @("/p", $Password) }
$args += $Package

& $signTool.Source @args
if ($LASTEXITCODE -ne 0) { throw "Signing failed with exit code $LASTEXITCODE" }

Write-Host "Signed $Package"
