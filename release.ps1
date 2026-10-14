# Usage: .\release.ps1 1.0.1.0
# Bumps the version, builds, updates repo.json, commits, and publishes a GitHub release with latest.zip.
param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$csproj = 'TomeFarmer.csproj'
(Get-Content $csproj -Raw) -replace '<Version>.*?</Version>', "<Version>$Version</Version>" | Set-Content $csproj -NoNewline

dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$repo = Get-Content repo.json -Raw | ConvertFrom-Json
$repo[0].AssemblyVersion = $Version
$json = ConvertTo-Json -InputObject @($repo) -Depth 5
[IO.File]::WriteAllText("$PSScriptRoot\repo.json", $json, (New-Object Text.UTF8Encoding $false))

git add -A
git commit -m "Release $Version"
git tag "v$Version"
git push origin HEAD --tags
gh release create "v$Version" 'bin/Release/TomeFarmer/latest.zip' --title "v$Version" --generate-notes
