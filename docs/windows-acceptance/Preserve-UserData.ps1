param(
 [Parameter(Mandatory=$true)][ValidateSet('Save','Restore','Verify')][string]$Action,
 [Parameter(Mandatory=$true)][string]$SnapshotDirectory
)
$ErrorActionPreference='Stop'
if (Get-Process RobotSavaslari -ErrorAction SilentlyContinue) { throw 'Close RobotSavaslari before saving/restoring data.' }
if (Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match '(?i)robot-verify-93b4ded[\\/]RobotArena' }) { throw 'Close this project in Unity first.' }
if (-not ('RawPlayerPrefs' -as [type])) { Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class RawPlayerPrefs {
 [DllImport("advapi32.dll", EntryPoint="RegSetValueExW", CharSet=CharSet.Unicode)]
 public static extern int Set(SafeRegistryHandle key, string name, int reserved, int type, byte[] bytes, int length);
}
'@
}
$backup=[IO.Path]::GetFullPath($SnapshotDirectory)
$data=Join-Path $env:USERPROFILE 'AppData\LocalLow\DefaultCompany\RobotArena'
$keys=@('HKCU:\Software\Unity\UnityEditor\DefaultCompany\RobotArena','HKCU:\Software\DefaultCompany\RobotArena')
if($Action -eq 'Save') {
 New-Item $backup -ItemType Directory -Force | Out-Null
 if(Test-Path (Join-Path $backup 'state.clixml')) {throw 'Backup already exists.'}
 $state=@{created_at_utc=[DateTime]::UtcNow.ToString('o');user=$env:USERNAME;files=@();registry=@()}
 foreach($name in @('driving-personal-best.json','match-history.json','challenge-session.json','challenge-session.json.lock')) {
  $file=Join-Path $data $name;$exists=Test-Path $file
  $state.files+=@{name=$name;existed=$exists;hash=$(if($exists){(Get-FileHash -LiteralPath $file).Hash}else{$null})}
  if($exists){Copy-Item $file (Join-Path $backup $name)}
 }
 foreach($path in $keys){
  $entry=@{path=$path;existed=(Test-Path $path);values=@()}
  if(Test-Path $path){$key=Get-Item $path;foreach($name in $key.GetValueNames()){$entry.values+=@{name=$name;kind=[int]$key.GetValueKind($name);value=$key.GetValue($name,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)}}}
  if($entry.existed){$key.Dispose()}
  $state.registry+=$entry
 }
 $state | Export-Clixml (Join-Path $backup 'state.clixml')
 Write-Output 'Saved RobotArena preferences and personal records.'
} elseif($Action -eq 'Restore') {
 $state=Import-Clixml (Join-Path $backup 'state.clixml')
 if($state.user -ne $env:USERNAME){throw 'Snapshot belongs to a different Windows user.'}
 foreach($entry in $state.files){
  if($entry.existed -and (!(Test-Path (Join-Path $backup $entry.name)) -or (Get-FileHash (Join-Path $backup $entry.name)).Hash -ne $entry.hash)){throw 'Snapshot file missing or changed; restore cancelled.'}
 }
 foreach($entry in $state.registry){
  if(!$entry.existed){if(Test-Path $entry.path){Remove-Item $entry.path -Recurse};continue}
  if(!(Test-Path $entry.path)){New-Item $entry.path -Force | Out-Null}
  $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($entry.path.Substring(6),$true)
  foreach($name in $key.GetValueNames()){if($name -notin @($entry.values | ForEach-Object {$_.name})){$key.DeleteValue($name,$false)}}
  foreach($value in $entry.values){
   $kind=[Microsoft.Win32.RegistryValueKind]$value.kind
   $typed=$value.value
   switch($kind.ToString()){
    'Binary' {$typed=[byte[]]@($value.value)}
    'None' {$typed=[byte[]]@($value.value)}
    'DWord' {if($value.value -is [long]){$typed=$value.value}else{$typed=[int]$value.value}}
    'QWord' {$typed=[long]$value.value}
    'MultiString' {$typed=[string[]]@($value.value)}
    'String' {$typed=[string]$value.value}
    'ExpandString' {$typed=[string]$value.value}
   }
   if($kind -eq [Microsoft.Win32.RegistryValueKind]::DWord -and $value.value -is [long]) {
    $bytes=[BitConverter]::GetBytes([long]$value.value)
    $rc=[RawPlayerPrefs]::Set($key.Handle,$value.name,0,4,$bytes,$bytes.Length)
    if($rc -ne 0){throw "Raw Unity preference restore failed: $rc"}
   } else { $key.SetValue($value.name,$typed,$kind) }
  }
  $key.Dispose()
 }
 foreach($entry in $state.files){$file=Join-Path $data $entry.name;if($entry.existed){New-Item $data -ItemType Directory -Force | Out-Null;Copy-Item (Join-Path $backup $entry.name) $file -Force}elseif(Test-Path $file){Remove-Item $file}}
 Write-Output 'Restored RobotArena preferences and personal records.'
}

$state=Import-Clixml (Join-Path $backup 'state.clixml')
$result=@{action=$Action;checked_at_utc=[DateTime]::UtcNow.ToString('o');registry_matches_snapshot=$true;personal_records_match_snapshot=$true;registry_entries_checked=0;files_checked=0}
foreach($entry in $state.registry){
 if(!$entry.existed){if(Test-Path $entry.path){$result.registry_matches_snapshot=$false};continue}
 if(!(Test-Path $entry.path)){$result.registry_matches_snapshot=$false;continue}
 $key=Get-Item $entry.path
 $wanted=@($entry.values | ForEach-Object {$_.name})
 $got=@($key.GetValueNames())
 if(($wanted | Sort-Object | ConvertTo-Json -Compress) -ne ($got | Sort-Object | ConvertTo-Json -Compress)){$result.registry_matches_snapshot=$false}
 foreach($value in $entry.values){
  $expected=ConvertTo-Json -InputObject $value.value -Compress
  $actual=ConvertTo-Json -InputObject ($key.GetValue($value.name,$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)) -Compress
  if($expected -ne $actual -or [int]$key.GetValueKind($value.name) -ne $value.kind){$result.registry_matches_snapshot=$false}
  $result.registry_entries_checked++
 }
 $key.Dispose()
}
$data=Join-Path $env:USERPROFILE 'AppData\LocalLow\DefaultCompany\RobotArena'
foreach($entry in $state.files){
 $file=Join-Path $data $entry.name
 if($entry.existed){if(!(Test-Path $file) -or (Get-FileHash $file).Hash -ne $entry.hash){$result.personal_records_match_snapshot=$false}}
 elseif(Test-Path $file){$result.personal_records_match_snapshot=$false}
 $result.files_checked++
}
$result | ConvertTo-Json | Set-Content (Join-Path $backup ("user-data-" + $Action + ".json")) -Encoding UTF8
Write-Output ($result | ConvertTo-Json -Compress)
if(!$result.registry_matches_snapshot -or !$result.personal_records_match_snapshot){throw 'Current data does not match snapshot. Keep snapshot and investigate.'}
