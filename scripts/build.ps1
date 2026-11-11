$ErrorActionPreference = 'Stop'
$Project = Join-Path $PSScriptRoot '..\src\AudioMixer.EngineProbe\AudioMixer.EngineProbe.csproj'
dotnet restore $Project
dotnet build $Project -c Release --no-restore
