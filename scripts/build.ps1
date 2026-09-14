param([switch]$Package)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $repoRoot 'source\Shici.csproj'
$outputFolder = Join-Path $repoRoot 'dist\Shici'
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'assets\dictionary.db'))) {
    throw '请先运行 python scripts/prepare_dictionary.py 准备词典。'
}
dotnet publish $projectFile -c Release --self-contained false -o $outputFolder --nologo
if ($LASTEXITCODE -ne 0) { throw '构建失败。' }
Write-Host "构建完成：$outputFolder"
if ($Package) {
    python (Join-Path $PSScriptRoot 'package_release.py')
    if ($LASTEXITCODE -ne 0) { throw '打包失败。' }
}
