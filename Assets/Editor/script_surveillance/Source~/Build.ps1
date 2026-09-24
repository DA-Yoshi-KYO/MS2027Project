$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build ScriptSurveillance.csproj --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Analyzer build failed.' }
    dotnet run --project Tests/Tests.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Analyzer tests failed.' }
    Copy-Item -LiteralPath 'bin/Release/netstandard2.0/MS2027.ScriptSurveillance.dll' -Destination '../MS2027.ScriptSurveillance.dll'
} finally {
    Pop-Location
}
