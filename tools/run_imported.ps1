param(
 [string]$ContentStore='',
 [string]$PlayerData='generated/importer/playtest-profile',
 [switch]$Setup,
 [switch]$NoBuild,
 [string[]]$GameArgs=@('--main-menu'),
 [Parameter(Mandatory=$true)][string]$Godot
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path "$PSScriptRoot/..").Path
function Absolute([string]$p) { if([IO.Path]::IsPathRooted($p)){[IO.Path]::GetFullPath($p)}else{[IO.Path]::GetFullPath((Join-Path $repo $p))} }
if(-not $NoBuild){
 & dotnet build "$repo/viewer/DarkColony.Viewer" --nologo /p:Optimize=true
 if($LASTEXITCODE -ne 0){throw 'Optimized source build failed; no older binary was launched.'}
}
if(-not (Test-Path -LiteralPath $Godot)){throw 'Select the installed Godot .NET console executable with -Godot.'}
$env:ODC_USER_DATA=Absolute $PlayerData
$launch=@('--path',"$repo/viewer/DarkColony.Viewer",'--')
if($ContentStore){$launch+=('--content-store='+(Absolute $ContentStore))}
if($Setup){$launch+='--content-setup'}
$launch+=$GameArgs
# Deliberately no --development-content: missing imports cannot borrow repository assets.
& $Godot @launch
exit $LASTEXITCODE
