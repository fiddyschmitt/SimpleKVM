<#
.SYNOPSIS
    Packs a published Linux build of SimpleKVM into <OutDir>\simplekvm-<Rid>.tar.gz.

.DESCRIPTION
    The archive holds one folder, simplekvm/, with the program (named simplekvm, in lower
    case as Linux programs are), install.sh, uninstall.sh, a README and the licence.

    A tar archive records each file's Unix permissions, which a file on a Windows disk
    doesn't have, so an archive made here the ordinary way would unpack a program that
    isn't executable. The permissions are therefore written down in an mtree specification
    (one line per entry: its path in the archive, its mode and owner, and the file its
    content comes from), which the tar that ships with Windows (bsdtar) takes as its input.

.PARAMETER Binary
    The published single-file executable (dotnet publish names it SimpleKVM).

.PARAMETER Rid
    The runtime identifier it was published for: linux-x64 or linux-arm64.

.PARAMETER OutDir
    Where the .tar.gz goes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Binary,
    [Parameter(Mandatory)][string]$Rid,
    [Parameter(Mandatory)][string]$OutDir
)

$ErrorActionPreference = "Stop"

$repoRoot  = Split-Path -Parent $PSScriptRoot
$packaging = Join-Path $repoRoot "packaging\linux"
$tar       = Join-Path $env:SystemRoot "System32\tar.exe"
$archive   = Join-Path $OutDir "simplekvm-$Rid.tar.gz"

if (-not (Test-Path $Binary)) { throw "no published binary at $Binary" }
if (-not (Test-Path $tar))    { throw "this script needs the tar that ships with Windows 10 and later ($tar)" }

# path in the archive, mode, source file
$entries = @(
    @("simplekvm/simplekvm",    "0755", $Binary),
    @("simplekvm/install.sh",   "0755", (Join-Path $packaging "install.sh")),
    @("simplekvm/uninstall.sh", "0755", (Join-Path $packaging "uninstall.sh")),
    @("simplekvm/README.txt",   "0644", (Join-Path $packaging "README.txt")),
    @("simplekvm/LICENSE",      "0644", (Join-Path $repoRoot "LICENSE"))
)

$stamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$owner = "uid=0 gid=0 uname=root gname=root"
$spec  = @("#mtree", "simplekvm type=dir mode=0755 $owner time=$stamp")

foreach ($entry in $entries) {
    $path, $mode, $source = $entry
    $source = (Resolve-Path $source).Path

    # A script that reached the working copy with Windows line endings would not run on Linux
    if ($path -match "\.(sh|txt)$" -and [IO.File]::ReadAllText($source).Contains("`r")) {
        throw "$source has Windows line endings; it must be checked out with LF (see .gitattributes)"
    }

    # mtree separates fields with spaces, so a space inside a path is written as its octal escape
    $spec += "$path type=file mode=$mode $owner contents=$($source.Replace('\', '/').Replace(' ', '\040'))"
}

$specFile = Join-Path $OutDir "_simplekvm-$Rid.mtree"
[IO.File]::WriteAllText($specFile, ($spec -join "`n") + "`n", (New-Object System.Text.UTF8Encoding $false))

if (Test-Path $archive) { Remove-Item $archive -Force }
& $tar -czf $archive --format=ustar "@$specFile"
if ($LASTEXITCODE -ne 0) { throw "tar failed for $Rid" }
Remove-Item $specFile -Force

# The point of the exercise: the program and the scripts must come out executable
$listing = & $tar -tvzf $archive
foreach ($executable in "simplekvm/simplekvm", "simplekvm/install.sh", "simplekvm/uninstall.sh") {
    if (-not ($listing | Where-Object { $_ -match "^-rwxr-xr-x .* $([regex]::Escape($executable))$" })) {
        throw "${archive}: $executable is not marked executable"
    }
}

$archive
