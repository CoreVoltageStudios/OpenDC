param(
 [Parameter(Mandatory=$true)][string]$Destination,
 [Parameter(Mandatory=$true)][string]$Godot,
 [string]$Comparison='',
 [string]$DisplayVersion='',
 [string]$SemanticVersion='',
 [string]$NumericVersion=''
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$target=[IO.Path]::GetFullPath($Destination)
if($target.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Use a fresh directory outside the repository.'}
if(Test-Path -LiteralPath $target){throw 'Destination already exists; previous exports are never overwritten.'}
if($Comparison -and $Comparison -notmatch '^[a-z0-9-]{1,32}$'){throw 'Invalid isolated comparison profile.'}
$godotPath=(Resolve-Path -LiteralPath $Godot).Path
if($godotPath -match '_console\.exe$'){throw 'Pass the full Godot editor executable, not its console wrapper.'}
$stage=Join-Path $target 'BuildSource'
$engine=Join-Path $target 'Engine'
$evidence=Join-Path $target 'Evidence'
New-Item -ItemType Directory -Path $stage,$engine,$evidence | Out-Null
$copied=[Collections.Generic.List[object]]::new()
function Copy-Source([string]$relative){
 $from=Join-Path $repo $relative
 $to=Join-Path $stage $relative
 if((Get-Item -LiteralPath $from).Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Linked build input: $relative"}
 New-Item -ItemType Directory -Force -Path (Split-Path $to -Parent) | Out-Null
 Copy-Item -LiteralPath $from -Destination $to
 $copied.Add([pscustomobject]@{path=$relative;sha256=(Get-FileHash -LiteralPath $from -Algorithm SHA256).Hash.ToLowerInvariant();bytes=(Get-Item -LiteralPath $from).Length})
}
# Only tracked engine code, explicitly named metadata, and approved bootstrap assets
# enter this staging project. The repository's private export preset is never used.
$projects='DarkColony.Viewer|DarkColony.Assets|DarkColony.Original|DarkColony.Simulation|DarkColony.Import|DarkColony.Media'
$tracked=@(& git -C $repo ls-files)
if($LASTEXITCODE -ne 0){throw 'Cannot enumerate tracked build sources.'}
foreach($path in $tracked){
 if($path -match "^viewer/($projects)/.*\.(cs|csproj)$" -or $path -match '^tools/(Interference|Legacy|Breakthrough|Roswell|Puppet|CivilWar)Route.cs$'){Copy-Source $path}
}
foreach($path in @('viewer/Directory.Build.props','viewer/DarkColony.Viewer/DarkColony.Viewer.sln','viewer/DarkColony.Viewer/project.godot','viewer/DarkColony.Viewer/Main.tscn','viewer/DarkColony.Import/content-contract.json','viewer/DarkColony.Import/council-native-map.json','viewer/DarkColony.Viewer/Branding/Icon/odc-icon.ico','viewer/DarkColony.Viewer/Branding/Icon/png/odc-icon-512.png','viewer/DarkColony.Viewer/Branding/CoreVoltage/core-voltage-intro.wav')){Copy-Source $path}
foreach($path in @('opendc-wordmark.svg','cue-residual-hold.wav','cue-charge-transfer.wav','cue-milestone.wav','cue-first-contact.wav','cue-awakening.wav')){Copy-Source ('viewer/DarkColony.Viewer/Branding/Landfall/'+$path)}
$fonts=Get-Content -LiteralPath (Join-Path $repo 'viewer/DarkColony.Viewer/Fonts/provenance.json') -Raw | ConvertFrom-Json
foreach($font in $fonts){
 $relative='viewer/DarkColony.Viewer/Fonts/'+$font.file
 if((Get-FileHash -LiteralPath (Join-Path $repo $relative)).Hash.ToLowerInvariant() -ne $font.sha256){throw "Unverified font input $relative"}
 Copy-Source $relative
}
$project=Join-Path $stage 'viewer/DarkColony.Viewer'
$checkpoint=(& git -C $repo rev-parse HEAD).Trim()
if($DisplayVersion -or $SemanticVersion -or $NumericVersion){
 if($DisplayVersion -notmatch '^\d+\.\d+\.\d+( RC[1-9][0-9]*)?$' -or $SemanticVersion -notmatch '^\d+\.\d+\.\d+(-rc\.[1-9][0-9]*)?$' -or $NumericVersion -notmatch '^\d+\.\d+\.\d+\.\d+$'){throw 'Supply validated display, semantic and numeric versions together.'}
 if(@($NumericVersion.Split('.') | Where-Object {[int]$_ -gt 65535}).Count){throw 'Windows version fields exceed 65535.'}
 # Metadata applies to the isolated staged build only; no source/output/profile rewrite.
 $propsPath=Join-Path $stage 'viewer/Directory.Build.props'
 [xml]$props=Get-Content -LiteralPath $propsPath
 $group=$props.CreateElement('PropertyGroup');$null=$props.Project.AppendChild($group)
 foreach($pair in @(@('Version',$SemanticVersion),@('InformationalVersion',$SemanticVersion),@('FileVersion',$NumericVersion),@('AssemblyVersion',(($NumericVersion.Split('.')[0..2] -join '.')+'.0')))){$element=$props.CreateElement($pair[0]);$element.InnerText=$pair[1];$null=$group.AppendChild($element)}
 $props.Save($propsPath)
}else{$DisplayVersion='Source-portability-'+$checkpoint.Substring(0,7);$NumericVersion='0.2.0.1';$SemanticVersion='0.2.0-dev'}
$build=@{version=$DisplayVersion;semanticVersion=$SemanticVersion;windowsVersion=$NumericVersion;checkpoint=$checkpoint;sourceDirty=!!(& git -C $repo diff --name-only);content='engine-only: player import required';contentContract=1;baseMissions=30;councilMissions=16;trainingLessons=14;warMaps=56;runtime='Godot 4.7.2 .NET / self-contained win-x64';private=$false}
[IO.File]::WriteAllText((Join-Path $project 'build.json'),($build | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
$preset=@'
[preset.0]
name="Engine Only"
platform="Windows Desktop"
runnable=true
export_filter="scenes"
export_files=PackedStringArray("res://Main.tscn")
include_filter="build.json,Branding/CoreVoltage/*.wav,Branding/Landfall/*.wav,Branding/Landfall/*.svg,Branding/Icon/odc-icon.ico,Fonts/*.ttf,Fonts/*.txt"
exclude_filter="*.csproj,*.sln,*.pdb,*.json.tmp"
export_path=""
[preset.0.options]
binary_format/architecture="x86_64"
binary_format/embed_pck=false
dotnet/include_scripts_content=false
dotnet/include_debug_symbols=false
application/modify_resources=true
application/icon="res://Branding/Icon/odc-icon.ico"
application/file_version="0.2.0.1"
application/product_version="0.2.0.1"
application/company_name="Core Voltage Studios"
application/product_name="OpenDC"
application/file_description="OpenDC source validation"
'@
$preset=$preset.Replace('application/file_version="0.2.0.1"','application/file_version="'+$NumericVersion+'"').Replace('application/product_version="0.2.0.1"','application/product_version="'+$NumericVersion+'"').Replace('application/file_description="OpenDC source validation"','application/file_description="OpenDC '+$DisplayVersion+'"')
[IO.File]::WriteAllText((Join-Path $project 'export_presets.cfg'),$preset,[Text.UTF8Encoding]::new($false))
$copied | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $evidence 'build-inputs.json')
function Run-Godot([string[]]$Extra,[string]$Name){
 $arguments=@('--headless','--path',('"'+$project+'"'))+$Extra
 $info=[Diagnostics.ProcessStartInfo]::new($godotPath,($arguments -join ' '))
 $info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
 $process=[Diagnostics.Process]::Start($info)
 $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 if(!$process.WaitForExit(600000)){Stop-Process -Id $process.Id;throw "Godot timed out during $Name; partial output is not accepted."}
 [IO.File]::WriteAllText((Join-Path $evidence ($Name+'.stdout.log')),$stdout.Result)
 [IO.File]::WriteAllText((Join-Path $evidence ($Name+'.stderr.log')),$stderr.Result)
 if($process.ExitCode -ne 0){throw "Godot $Name failed: $($process.ExitCode)"}
}
Run-Godot @('--editor','--import') 'import'
Run-Godot @('--export-release','"Engine Only"',('"'+(Join-Path $engine 'OpenDC.exe')+'"')) 'export'
foreach($required in @('OpenDC.exe','OpenDC.pck','data_DarkColony.Viewer_windows_x86_64/DarkColony.Viewer.dll','data_DarkColony.Viewer_windows_x86_64/coreclr.dll','data_DarkColony.Viewer_windows_x86_64/DarkColony.Media.dll')){if(!(Test-Path -LiteralPath (Join-Path $engine $required))){throw "Missing export output $required"}}
Copy-Item -LiteralPath (Join-Path $project 'Branding/Icon/odc-icon.ico') -Destination $engine
Copy-Item -LiteralPath (Join-Path $project 'build.json') -Destination $engine
if($Comparison){@{comparison=$Comparison} | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $engine 'install-profile.json')}
$notices=Join-Path $engine 'Notices'
New-Item -ItemType Directory -Path $notices | Out-Null
# Project/asset terms accompany the runtime and its third-party notices.
foreach($notice in @('LICENSE','ASSET-LICENSES.md','docs/PRESENTATION-LICENSE-MAP.md','docs/DEPENDENCIES.md')){
 $noticePath=Join-Path $repo $notice
 $noticeText=Get-Content -LiteralPath $noticePath -Raw
 if($noticeText -match 'awaiting confirmation before publication|Pre-publication draft'){throw "Finalize $notice before exporting licensed content."}
 $noticeTarget=Join-Path $notices $(if($notice -eq 'LICENSE'){'OPENDC-LICENSE.txt'}else{$notice})
 New-Item -ItemType Directory -Force -Path (Split-Path $noticeTarget -Parent) | Out-Null
 Copy-Item -LiteralPath $noticePath -Destination $noticeTarget
}
$runtime=Get-Content -LiteralPath (Join-Path $engine 'data_DarkColony.Viewer_windows_x86_64/DarkColony.Viewer.runtimeconfig.json') -Raw | ConvertFrom-Json
$runtimeVersion=($runtime.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.NETCore.App').version
if(!$runtimeVersion){throw 'Export is not self-contained or runtime version cannot be established.'}
$nuget=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
$runtimePack=Join-Path $nuget ('microsoft.netcore.app.runtime.win-x64/'+$runtimeVersion)
Copy-Item -LiteralPath (Join-Path $runtimePack 'LICENSE.TXT') -Destination (Join-Path $notices 'DOTNET-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $runtimePack 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $notices 'DOTNET-THIRD-PARTY-NOTICES.txt')
foreach($font in $fonts | Where-Object file -like '*.txt'){Copy-Item -LiteralPath (Join-Path $project ('Fonts/'+$font.file)) -Destination $notices}
Copy-Item -LiteralPath (Join-Path $repo 'viewer/DarkColony.Media/NOTICE.txt') -Destination (Join-Path $notices 'MEDIA-NOTICE.txt')
Copy-Item -LiteralPath (Join-Path $repo 'viewer/DarkColony.Media/COPYING.LGPLv2.1') -Destination $notices
# Complete modified LGPL library source, build parameters and replacement instructions.
$mediaSource=Join-Path $engine 'Source/Media'
New-Item -ItemType Directory -Path $mediaSource | Out-Null
Get-ChildItem -LiteralPath (Join-Path $stage 'viewer/DarkColony.Media') -File | Where-Object { $_.Extension -in '.cs','.csproj' } | Copy-Item -Destination $mediaSource
Copy-Item -LiteralPath (Join-Path $stage 'viewer/Directory.Build.props') -Destination $mediaSource
Copy-Item -LiteralPath (Join-Path $repo 'packaging/ENGINE-ONLY-NOTICES.txt') -Destination (Join-Path $engine 'NOTICES.txt')
Copy-Item -LiteralPath (Join-Path $repo 'packaging/MEDIA-SOURCE-README.txt') -Destination (Join-Path $mediaSource 'README.txt')
# Godot's published COPYRIGHT contains the engine and third-party license texts;
# the running-template diagnostic also emits its exact compiled component list.
Copy-Item -LiteralPath (Join-Path $repo 'packaging/GODOT-COPYRIGHT.txt') -Destination $notices
$noticeInfo=[Diagnostics.ProcessStartInfo]::new((Join-Path $engine 'OpenDC.exe'),('--headless --log-file "'+(Join-Path $evidence 'notices.log')+'" -- --engine-notices="'+$notices+'"'))
$noticeInfo.UseShellExecute=$false;$noticeInfo.CreateNoWindow=$true;$noticeInfo.RedirectStandardOutput=$true;$noticeInfo.RedirectStandardError=$true
$noticeProcess=[Diagnostics.Process]::Start($noticeInfo)
$noticeOut=$noticeProcess.StandardOutput.ReadToEndAsync();$noticeErr=$noticeProcess.StandardError.ReadToEndAsync()
if(!$noticeProcess.WaitForExit(30000)){Stop-Process -Id $noticeProcess.Id;throw 'Compiled-template license extraction timed out'}
[IO.File]::WriteAllText((Join-Path $evidence 'notices.stdout.log'),$noticeOut.Result)
[IO.File]::WriteAllText((Join-Path $evidence 'notices.stderr.log'),$noticeErr.Result)
if($noticeProcess.ExitCode -ne 0 -or $noticeOut.Result -notmatch 'ENGINE_NOTICES_EXTRACTED'){throw 'Compiled-template license extraction failed'}
$templateVersion=Get-Content -LiteralPath (Join-Path $notices 'GODOT-VERSION.json') -Raw | ConvertFrom-Json
if($templateVersion.major -ne 4 -or $templateVersion.minor -ne 7 -or $templateVersion.patch -ne 2){throw 'Unreviewed Godot template version; this project/export contract is pinned to 4.7.2.'}
$inventory=Get-ChildItem -LiteralPath $engine -Recurse -File | ForEach-Object {
 if($_.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked export output'}
 $relative=$_.FullName.Substring($engine.Length+1).Replace('\','/')
 if($relative -match '(^|/)(private|generated|profiles|saves|research|evidence|content)(/|$)' -or $_.Extension -in '.avi','.iso','.bin','.mdf','.mds','.cue','.zip','.odcv','.tro','.spr','.pdb'){throw "Forbidden distribution file: $relative"}
 [pscustomobject]@{path=$relative;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant();purpose=$(if($relative -match '^Notices/|^NOTICES'){'Redistribution notices'}elseif($relative -match '^Source/'){'Replaceable media-library corresponding source'}elseif($relative -match '^data_'){'Self-contained managed application and .NET runtime'}elseif($relative -eq 'OpenDC.pck'){'Allowlisted bootstrap resources; inspect runtime PCK index'}else{'Engine executable, icon or build/profile identity'})}
}
$inventory | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath (Join-Path $evidence 'engine-files.csv')
@{checkpoint=$checkpoint;runtimeVersion=$runtimeVersion;files=$inventory.Count;bytes=($inventory | Measure-Object bytes -Sum).Sum;engine=$engine;godot=$godotPath;godotSha256=(Get-FileHash -LiteralPath $godotPath).Hash;sourceStaging=$stage} | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $evidence 'export.json')
Write-Output "Engine-only validation export: $engine"
