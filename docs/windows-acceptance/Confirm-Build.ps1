param([string]$Repository=(Split-Path $PSScriptRoot -Parent | Split-Path -Parent))
$ErrorActionPreference='Stop'
$build=Join-Path $Repository 'RobotArena\Builds\Windows'
$manifest=Get-Content (Join-Path $Repository 'docs\evidence\validation-20261118\final-build-hashes.json') -Raw | ConvertFrom-Json
$identity=Get-Content (Join-Path $Repository 'docs\evidence\windows-acceptance-20261118\build-verification.json') -Raw | ConvertFrom-Json
if($manifest.source_sha -ne $identity.source_sha){throw 'Manifest/source mismatch.'}
if(@(Get-ChildItem -LiteralPath $build -File -Recurse).Count -ne $manifest.files.Count){throw 'Build file count changed.'}
foreach($file in $manifest.files){
 $path=Join-Path $build $file.path
 if(!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -ne $file.length -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256){throw ('Build mismatch: '+$file.path)}
}
[ordered]@{checked_at_utc=[DateTime]::UtcNow.ToString('o');source_sha=$manifest.source_sha;build_id=$identity.build_id;files_verified=$manifest.files.Count;exe=(Join-Path $build 'RobotSavaslari.exe')} | ConvertTo-Json
