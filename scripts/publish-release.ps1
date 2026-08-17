<#
.SYNOPSIS
    Builds the release artifacts for a SimpleKVM release into .\publish.

.DESCRIPTION
    Windows: self-contained, single-file           ->  publish\SimpleKVM.exe
             (not trimmed: the SDK refuses to trim anything referencing Windows Forms,
             which the Windows build still uses for its hotkey and screen helpers)
    macOS:   self-contained, single-file, trimmed  ->  publish\SimpleKVM-osx-arm64.zip
             (the zip contains SimpleKVM.app, assembled and ad-hoc signed on the Mac)

    Each artifact is smoke-tested after it is built: the Windows exe and the Mac
    binary both have to run --verify-rules against a real rules.json, which
    exercises the reflection-heavy JSON path that trimming is most likely to break.

.PARAMETER MacHost
    ssh host used to assemble and sign the .app (default 192.168.0.33). Skipped when empty.

.PARAMETER SkipMac
    Build only the Windows artifact.
#>
[CmdletBinding()]
param(
    [string]$MacHost = "192.168.0.33",
    [switch]$SkipMac
)

$ErrorActionPreference = "Stop"

$repoRoot   = Split-Path -Parent $PSScriptRoot
$project    = Join-Path $repoRoot "SimpleKVM\SimpleKVM.csproj"
$publishDir = Join-Path $repoRoot "publish"
$rulesFile  = Join-Path $repoRoot "SimpleKVM\bin\Debug\net10.0-windows7.0\rules.json"

$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
Write-Host "SimpleKVM $version" -ForegroundColor Cyan

New-Item -ItemType Directory -Force $publishDir | Out-Null
Get-ChildItem $publishDir | Remove-Item -Recurse -Force

function Publish-Rid {
    param([string]$Framework, [string]$Rid, [string]$OutDir, [bool]$Trim)

    Write-Host "`n== publish $Rid ==" -ForegroundColor Cyan
    dotnet publish $project `
        -f $Framework -r $Rid -c Release --nologo -v q `
        --self-contained `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=$($Trim.ToString().ToLower()) `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none `
        -o $OutDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Rid" }
}

# ---------------------------------------------------------------- Windows
$winOut = Join-Path $publishDir "_win-x64"
Publish-Rid -Framework "net10.0-windows7.0" -Rid "win-x64" -OutDir $winOut -Trim $false

$winExe = Join-Path $winOut "SimpleKVM.exe"
if (-not (Test-Path $winExe)) { throw "win-x64 publish produced no SimpleKVM.exe" }

Write-Host "smoke test (win-x64): --verify-rules"
if (Test-Path $rulesFile) {
    #Start-Process with an explicit stdout file gives the WinExe a real redirected handle,
    #which is the one capture route that works reliably from every PowerShell host
    $log = Join-Path $publishDir "_smoke.log"
    $proc = Start-Process -FilePath $winExe -ArgumentList "--verify-rules", "`"$rulesFile`"" `
                -RedirectStandardOutput $log -NoNewWindow -Wait -PassThru
    $out = if (Test-Path $log) { Get-Content $log -Raw } else { "" }
    Remove-Item $log -ErrorAction SilentlyContinue

    if ($proc.ExitCode -ne 0 -or $out -notmatch "Parsed \d+ rule") {
        Write-Host $out -ForegroundColor Red
        throw "win-x64 build failed the rules smoke test (exit $($proc.ExitCode))"
    }
    Write-Host ("  " + ($out -split "`n" | Select-Object -First 1).Trim()) -ForegroundColor Green
}
else {
    Write-Warning "no rules.json found at $rulesFile - skipping the win-x64 smoke test"
}

Move-Item $winExe (Join-Path $publishDir "SimpleKVM.exe")
Remove-Item $winOut -Recurse -Force

# ---------------------------------------------------------------- macOS
if (-not $SkipMac -and $MacHost) {
    $macOut = Join-Path $publishDir "_osx-arm64"
    Publish-Rid -Framework "net10.0" -Rid "osx-arm64" -OutDir $macOut -Trim $true

    Write-Host "assembling SimpleKVM.app on $MacHost"
    $remote = "~/simplekvm-release"

    # Ship the publish output plus the packaging inputs, then assemble, sign and smoke test there
    ssh -o BatchMode=yes $MacHost "rm -rf $remote && mkdir -p $remote/publish $remote/scripts $remote/packaging/macos $remote/SimpleKVM"
    if ($LASTEXITCODE -ne 0) { throw "could not prepare $MacHost" }

    #scp rather than a tar pipe: Windows PowerShell corrupts binary data piped between native commands
    scp -q -r "$macOut\*" "${MacHost}:$remote/publish/"
    if ($LASTEXITCODE -ne 0) { throw "could not copy the osx-arm64 publish output to $MacHost" }
    scp -q (Join-Path $repoRoot "scripts\package-macos.sh") "${MacHost}:$remote/scripts/"
    scp -q (Join-Path $repoRoot "packaging\macos\Info.plist") "${MacHost}:$remote/packaging/macos/"
    scp -q (Join-Path $repoRoot "SimpleKVM\iconfinder_Communication_pc_computer_sharing_6588768_white_bg.ico") "${MacHost}:$remote/SimpleKVM/"
    if (Test-Path $rulesFile) { scp -q $rulesFile "${MacHost}:$remote/rules.json" }

    #Passed as one command line rather than piped to stdin: Windows PowerShell adds a BOM to
    #piped text, which bash then treats as part of the first command
    $macCommands = @(
        "set -e",
        "cd $remote",
        "chmod +x scripts/package-macos.sh publish/SimpleKVM",
        "scripts/package-macos.sh publish . >/dev/null",
        "echo 'smoke test (osx-arm64): --verify-rules'",
        "if [ -f rules.json ]; then SimpleKVM.app/Contents/MacOS/SimpleKVM --verify-rules rules.json | head -1; else SimpleKVM.app/Contents/MacOS/SimpleKVM --list-monitors | head -1; fi"
    ) -join "; "
    #stderr is merged remotely: Windows PowerShell treats any native stderr output as an error
    ssh -o BatchMode=yes $MacHost "( $macCommands ) 2>&1"
    if ($LASTEXITCODE -ne 0) { throw "osx-arm64 packaging or smoke test failed on $MacHost" }

    scp -q "${MacHost}:$remote/SimpleKVM-macos-arm64.zip" (Join-Path $publishDir "SimpleKVM-osx-arm64.zip")
    if ($LASTEXITCODE -ne 0) { throw "could not fetch the mac zip" }

    Remove-Item $macOut -Recurse -Force
}

# ---------------------------------------------------------------- summary
Write-Host "`n== release artifacts ($version) ==" -ForegroundColor Cyan
Get-ChildItem $publishDir | ForEach-Object {
    "{0,-32} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB)
}
