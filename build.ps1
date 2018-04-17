Param(
    [string]$Target = "Default",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Verbose",
    [string]$PublishUrl,
    [string]$PublishKey
)

$rootDir        = Split-Path -parent $MyInvocation.MyCommand.Definition;
$toolsDir       = Join-Path $rootDir "tools"
$nugetExeBoot   = Join-Path $toolsDir "nuget-bootstrap.ps1"
$nugetExe       = Join-Path $toolsDir "nuget.exe"
$packagesDir    = Join-Path $toolsDir "packages"

$cakeExe        = Join-Path $packagesDir "cake/cake.exe"

# Ensure nuget.exe
& $nugetExeBoot -nugetExePath $nugetExe
if ($lastExitCode -ne 0) {
    exit $lastExitCode
}

# Restore tools from NuGet
& $nugetExe install "Codestellation.Nova.CakeBuild" -version 1.0.3 -excludeVersion -outputDirectory $packagesDir
if ($lastExitCode -ne 0) {
    exit $lastExitCode
}

# Start Cake
& $cakeExe "build.cake" -target="$Target" -configuration="$Configuration" -verbosity="$Verbosity" -publishUrl="$PublishUrl" -publishKey="$PublishKey" -experimental
exit $lastExitCode
