param(
    [Parameter(Mandatory=$true)][string]$CommandsFile,
    [Parameter(Mandatory=$true)][string]$Name
)
$ErrorActionPreference = 'Stop'
$run = Get-Content $CommandsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$step = @($run.steps | Where-Object name -eq $Name)
if ($step.Count -ne 1) { throw "Expected one step named $Name" }
$step = $step[0]
if (!(Test-Path $step.executable)) { throw "Executable missing: $($step.executable)" }
if ($step.test_results -and (Test-Path $step.test_results)) { throw 'Results already exist; use a fresh run rather than overwriting evidence.' }
$resultPath = Join-Path $run.evidence ($Name+'.result.json')
if (Test-Path $resultPath) { throw "Step already recorded: $Name" }
# Quote each argument for Windows Start-Process; paths cannot contain double quotes.
$arguments = @($step.arguments | ForEach-Object {
    if ([string]$_ -match '"') { throw 'Double quotes are not allowed inside an argument.' }
    '"' + [string]$_ + '"'
}) -join ' '
$env:RS_TEST_TRACE_DIR = Join-Path $run.evidence ($Name+".trace")
$started = [DateTimeOffset]::UtcNow
$process = Start-Process $step.executable -ArgumentList $arguments -PassThru
$processHandle = $process.Handle
$timedOut = $false
while (!$process.WaitForExit(1000)) {
    if (([DateTimeOffset]::UtcNow - $started).TotalSeconds -gt $step.timeout_seconds) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit()
        $timedOut = $true
        break
    }
}
$process.Refresh()
$result = [ordered]@{ name=$Name; test_sha=$run.test_sha; started_utc=$started.ToString('O'); ended_utc=[DateTimeOffset]::UtcNow.ToString('O'); exit_code=$process.ExitCode; timeout=$timedOut; status='failed'; xml=$step.test_results; manual_acceptance='not_run' }
if (!$timedOut -and $process.ExitCode -eq 0) { $result.status='passed' }
if ($timedOut) { $result.status='timed_out' }
if ($step.test_results) {
    if (Test-Path $step.test_results) {
        [xml]$xml = Get-Content $step.test_results -Raw
        $root = $xml.DocumentElement
        foreach ($field in @('total','passed','failed','skipped','inconclusive')) { $result[$field]=[int]$root.GetAttribute($field) }
        $result['test_failures'] = @($root.SelectNodes('.//test-case[@result="Failed"]') | ForEach-Object { [ordered]@{ name=$_.GetAttribute('fullname'); message=$_.SelectSingleNode('failure/message').InnerText } })
        if ($result.total -eq 0 -or $result.failed -gt 0) { $result.status='failed' }
        elseif ($result.skipped -gt 0 -or $result.inconclusive -gt 0) { $result.status='incomplete' }
    } else { if (!$timedOut) { $result.status='missing_results' } }
}
$result | ConvertTo-Json -Depth 10 | Set-Content $resultPath -Encoding UTF8
Write-Output ($result | ConvertTo-Json -Depth 10 -Compress)
if ($result.status -ne 'passed') { exit 1 }
