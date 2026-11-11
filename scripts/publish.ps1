$ErrorActionPreference = 'Stop'
$Project = Join-Path $PSScriptRoot '..\src\AudioMixer.EngineProbe\AudioMixer.EngineProbe.csproj'
$Output = Join-Path $PSScriptRoot '..\artifacts\EngineProbe-win-x64'
dotnet restore $Project
dotnet publish $Project -c Release -r win-x64 --no-self-contained -o $Output
Write-Host "Published to: $Output"
