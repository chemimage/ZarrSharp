param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectDir,

    [Parameter(Mandatory = $true)]
    [string]$Configuration
)

$ErrorActionPreference = 'Stop'

if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows))
{
    throw "Zarr.Codecs.Blosc currently supports native builds only on Windows hosts."
}

$projectRoot = (Resolve-Path $ProjectDir).Path
$repoRoot = Split-Path -Parent $projectRoot
$repoRoot = Split-Path -Parent $repoRoot
$cBloscRoot = Join-Path $projectRoot 'native\c-blosc'
if (-not (Test-Path (Join-Path $cBloscRoot 'blosc\blosc.h')))
{
    throw "The c-blosc submodule is missing. Run 'git submodule update --init --recursive'."
}

$toolsDir = Join-Path $repoRoot '.tools'
$zigVersion = '0.13.0'
$zigFolder = "zig-windows-x86_64-$zigVersion"
$zigDir = Join-Path $toolsDir $zigFolder
$zigExe = Join-Path $zigDir 'zig.exe'

if (-not (Test-Path $zigExe))
{
    New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
    $zipPath = Join-Path $toolsDir "$zigFolder.zip"
    if (-not (Test-Path $zipPath))
    {
        Invoke-WebRequest -Uri "https://ziglang.org/download/$zigVersion/$zigFolder.zip" -OutFile $zipPath
    }

    Expand-Archive -Path $zipPath -DestinationPath $toolsDir -Force
}

$artifactDir = Join-Path $projectRoot "artifacts\$Configuration\win-x64\native"
$intermediateDir = Join-Path $projectRoot "artifacts\$Configuration\build"
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
New-Item -ItemType Directory -Path $intermediateDir -Force | Out-Null

$configHeader = @'
#ifndef _CONFIGURATION_HEADER_GUARD_H_
#define _CONFIGURATION_HEADER_GUARD_H_

#define HAVE_LZ4 1
#define HAVE_ZLIB 1
#define HAVE_ZSTD 1

#endif
'@
Set-Content -Path (Join-Path $intermediateDir 'config.h') -Value $configHeader -Encoding ascii

$sources = @()
$sources += Get-ChildItem (Join-Path $cBloscRoot 'blosc') -Filter '*.c' |
    Where-Object { $_.Name -notmatch 'shuffle-(sse2|avx2)\.c$|bitshuffle-(sse2|avx2)\.c$' } |
    Select-Object -ExpandProperty FullName
$sources += Get-ChildItem (Join-Path $cBloscRoot 'internal-complibs\lz4-1.10.0') -Filter '*.c' |
    Select-Object -ExpandProperty FullName
$sources += Get-ChildItem (Join-Path $cBloscRoot 'internal-complibs\zlib-1.3.1') -Filter '*.c' |
    Select-Object -ExpandProperty FullName
$sources += Get-ChildItem (Join-Path $cBloscRoot 'internal-complibs\zstd-1.5.6\common') -Filter '*.c' |
    Select-Object -ExpandProperty FullName
$sources += Get-ChildItem (Join-Path $cBloscRoot 'internal-complibs\zstd-1.5.6\compress') -Filter '*.c' |
    Select-Object -ExpandProperty FullName
$sources += Get-ChildItem (Join-Path $cBloscRoot 'internal-complibs\zstd-1.5.6\decompress') -Filter '*.c' |
    Select-Object -ExpandProperty FullName
$sources += Get-ChildItem (Join-Path $cBloscRoot 'internal-complibs\zstd-1.5.6\dictBuilder') -Filter '*.c' |
    Select-Object -ExpandProperty FullName

$arguments = @(
    'cc',
    '-shared',
    '-O2',
    '-target', 'x86_64-windows-gnu',
    '-DBLOSC_SHARED_LIBRARY',
    '-DHAVE_LZ4',
    '-DHAVE_ZLIB',
    '-DHAVE_ZSTD',
    '-DZSTD_DISABLE_ASM',
    '-I', (Join-Path $cBloscRoot 'blosc'),
    '-I', $intermediateDir,
    '-I', (Join-Path $cBloscRoot 'internal-complibs\lz4-1.10.0'),
    '-I', (Join-Path $cBloscRoot 'internal-complibs\zlib-1.3.1'),
    '-I', (Join-Path $cBloscRoot 'internal-complibs\zstd-1.5.6'),
    '-I', (Join-Path $cBloscRoot 'internal-complibs\zstd-1.5.6\common'),
    '-o', (Join-Path $artifactDir 'blosc.dll')
) + $sources

& $zigExe @arguments
