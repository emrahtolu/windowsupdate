[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot 'ServerUpdatePackager'
$outputDirectory = Join-Path $projectRoot 'artifacts'
$temporaryBuildRoot = Join-Path ([IO.Path]::GetTempPath()) 'ServerUpdatePackager-build-cache'

New-Item -ItemType Directory -Path $temporaryBuildRoot -Force | Out-Null
$env:DOTNET_CLI_HOME = Join-Path $temporaryBuildRoot 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $temporaryBuildRoot 'nuget-packages'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK bulunamadı. .NET SDK ile .NET Framework 4.8 Developer Pack kurun.'
}

$sdkLine = dotnet --list-sdks | Select-Object -Last 1
if ($sdkLine -notmatch '^(?<Version>\S+)\s+\[(?<Root>.+)\]$') {
    throw '.NET SDK dizini belirlenemedi.'
}

$compiler = Join-Path $Matches.Root "$($Matches.Version)\Roslyn\bincore\csc.dll"
$programFilesX86Path = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
$frameworkReferences = Join-Path $programFilesX86Path 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "C# derleyicisi bulunamadı: $compiler"
}
if (-not (Test-Path -LiteralPath $frameworkReferences)) {
    throw '.NET Framework 4.8 Developer Pack başvuru derlemeleri bulunamadı.'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$executable = Join-Path $outputDirectory 'Server-Update-Packager-v2.exe'
$manifest = Join-Path $sourceDirectory 'app.manifest'
$icon = Join-Path $sourceDirectory 'app.ico'

$referenceArguments = Get-ChildItem -LiteralPath $frameworkReferences -Filter '*.dll' |
    Where-Object { $_.Name -notmatch 'Wrapper|Thunk' } |
    ForEach-Object { "/reference:$($_.FullName)" }
$sourceArguments = Get-ChildItem -LiteralPath $sourceDirectory -Filter '*.cs' |
    ForEach-Object { $_.FullName }
$configurationArguments = if ($Configuration -eq 'Release') {
    @('/optimize+', '/debug-')
} else {
    @('/optimize-', '/debug:portable')
}

$compilerArguments = @(
    $compiler,
    '/nologo',
    '/target:winexe',
    '/langversion:latest',
    '/nullable:enable',
    '/define:NETFRAMEWORK',
    '/platform:x64',
    '/deterministic+',
    '/utf8output',
    "/win32manifest:$manifest",
    "/win32icon:$icon",
    "/out:$executable"
) + $configurationArguments + $referenceArguments + $sourceArguments

dotnet @compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "Derleme başarısız oldu. Çıkış kodu: $LASTEXITCODE"
}

$test = Start-Process -FilePath $executable -ArgumentList '--self-test' -Wait -PassThru
if ($test.ExitCode -ne 0) {
    throw "Öz test başarısız oldu. Çıkış kodu: $($test.ExitCode)"
}

$hash = Get-FileHash -LiteralPath $executable -Algorithm SHA256
Write-Host 'Derleme ve öz test başarılı.'
Write-Host "EXE: $executable"
Write-Host "SHA-256: $($hash.Hash)"
