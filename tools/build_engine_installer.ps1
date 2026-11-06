param(
 [Parameter(Mandatory=$true)][string]$Godot,
 [Parameter(Mandatory=$true)][string]$ISCC,
 [Parameter(Mandatory=$true)][string]$ExportRoot,
 [Parameter(Mandatory=$true)][string]$Destination
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$target=[IO.Path]::GetFullPath($Destination)
$export=[IO.Path]::GetFullPath($ExportRoot)
if(Test-Path -LiteralPath $target){throw 'Use a fresh candidate destination; earlier artifacts are preserved.'}
if(Test-Path -LiteralPath $export){throw 'Use a fresh engine export directory.'}
Push-Location $repo
try {
 if(git diff --name-only HEAD){throw 'Checkpoint tracked changes before building a candidate.'}
 $checkpoint=(git rev-parse HEAD).Trim()
 $version=Get-Content packaging/rc1-version.json -Raw | ConvertFrom-Json
 $compiler=(Resolve-Path -LiteralPath $ISCC).Path
 $compilerVersion=(& $compiler --version).Trim()
 if($LASTEXITCODE -ne 0 -or $compilerVersion -ne '7.1.1'){throw 'This installer pipeline is verified with the preserved Inno Setup 7.1.1 toolchain.'}
 New-Item -ItemType Directory -Path $target | Out-Null
 & ./tools/export_engine_only.ps1 -Destination $export -Godot $Godot -DisplayVersion $version.displayVersion -SemanticVersion $version.semanticVersion -NumericVersion $version.windowsVersion
 $engine=Join-Path $export 'Engine';$evidence=Join-Path $export 'Evidence'
 if(!(Test-Path -LiteralPath (Join-Path $evidence 'export.json'))){throw 'Export did not complete.'}
 if(Test-Path -LiteralPath (Join-Path $engine 'install-profile.json')){throw 'Candidate payload must not select a test profile.'}
 python tools/audit_engine_export.py $engine $evidence
 if($LASTEXITCODE -ne 0){throw 'Actual engine asset/dependency audit failed.'}
 $inventory=Get-Content -LiteralPath (Join-Path $evidence 'final-engine-inventory.json') -Raw | ConvertFrom-Json
 if($inventory.Count -lt 100 -or @($inventory | Where-Object {!($_.path -is [string])}).Count){throw 'Expected individual audited payload records.'}
 $manifest=Join-Path $evidence 'opendc-payload.manifest'
 $lines=@($inventory | ForEach-Object {$_.sha256+'|'+$_.path.Replace('/','\')})
 [IO.File]::WriteAllLines($manifest,$lines,[Text.UTF8Encoding]::new($false))
 $include=Join-Path $evidence 'payload.iss'
 $entries=@($inventory | ForEach-Object {
  $relative=$_.path.Replace('/','\')
  if($relative -match '[";{}\r\n]' -or $relative.Contains('..')){throw "Unsafe payload path $relative"}
  $parent=Split-Path $relative -Parent
  'Source: "{#Stage}\'+$relative+'"; DestDir: "{app}'+$(if($parent){'\'+$parent}else{''})+'"; Flags: ignoreversion'
 })
 $entries+='Source: "'+$manifest+'"; DestDir: "{app}"; Flags: ignoreversion'
 [IO.File]::WriteAllLines($include,$entries,[Text.UTF8Encoding]::new($false))
 & $compiler --no-ide-signtools "/DStage=$engine" "/DPayloadList=$include" "/DOutput=$target" "/DDisplayVersion=$($version.displayVersion)" "/DSemanticVersion=$($version.semanticVersion)" "/DNumericVersion=$($version.windowsVersion)" packaging/OpenDC-Engine.iss *> (Join-Path $evidence 'installer-build.log')
 if($LASTEXITCODE -ne 0){throw "Installer compilation failed; inspect $evidence/installer-build.log"}
 $installer=Join-Path $target ('OpenDC-'+$version.semanticVersion+'-Windows-x64-Setup.exe')
 if(!(Test-Path -LiteralPath $installer)){throw 'Compiler produced no expected installer.'}
 $signature=Get-AuthenticodeSignature -LiteralPath $installer
 if($signature.Status -notin @('NotSigned','Valid')){throw 'Unexpected installer signature state.'}
 Copy-Item -LiteralPath (Join-Path $evidence 'final-engine-inventory.json') -Destination (Join-Path $target 'payload-inventory.json')
 Copy-Item -LiteralPath (Join-Path $evidence 'pck-inventory.json') -Destination $target
 Copy-Item -LiteralPath (Join-Path $evidence 'engine-files.csv') -Destination $target
 Copy-Item -LiteralPath (Join-Path $engine 'build.json') -Destination $target
 Copy-Item packaging/RC1-TESTER-README.txt $target
 $hash=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
 [IO.File]::WriteAllText((Join-Path $target 'SHA256SUMS.txt'),$hash+'  '+[IO.Path]::GetFileName($installer)+"`r`n",[Text.Encoding]::ASCII)
 @{checkpoint=$checkpoint;version=$version;installer=$installer;installerSha256=$hash;bytes=(Get-Item -LiteralPath $installer).Length;signing=$signature.Status.ToString();engine=$engine;exportEvidence=$evidence;compiler=$compiler;compilerVersion=$compilerVersion;compilerSha256=(Get-FileHash -LiteralPath $compiler).Hash;payloadFiles=$inventory.Count;payloadBytes=($inventory | Measure-Object bytes -Sum).Sum;classification='Local RC1 candidate; not published or acceptance approval'} | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $target 'candidate.json')
 Write-Output "LOCAL_RC1_CANDIDATE $installer"
} finally {Pop-Location}
