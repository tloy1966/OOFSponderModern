param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [string]$OutputDir = ".\artifacts",
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "OOFSponderModern\OOFSponderModern.csproj"
$artifactName = "OOFSponderModern-$Runtime"
$publishRoot = Join-Path $repoRoot $OutputDir
$publishDir = Join-Path $publishRoot "publish\$artifactName"
$zipPath = Join-Path $publishRoot "$artifactName.zip"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

$publishArgs = @(
    "publish", $projectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", "true",
    "--output", $publishDir,
    "/p:PublishSingleFile=true",
    "/p:IncludeNativeLibrariesForSelfExtract=true",
    "/p:EnableCompressionInSingleFile=true"
)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $publishArgs += "/p:Version=$Version"
}

Write-Output "Publishing OOFSponderModern..."
Write-Output "Project: $projectPath"
Write-Output "Runtime: $Runtime"
Write-Output "Output: $publishDir"

dotnet @publishArgs

if (-not $SkipZip) {
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force
    Write-Output "Created release archive: $zipPath"
}
else {
    Write-Output "Skipped zip creation. Published files are in: $publishDir"
}
