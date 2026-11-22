param(
 [Parameter(Mandatory=$true)][string]$Executable,
 [Parameter(Mandatory=$true)][string]$EvidenceDirectory,
 [Parameter(Mandatory=$true)][ValidateSet('Launch','Click','Key','Shot','Quit','Wait')][string]$Step,
 [string]$Name = '',
 [double]$X = 0, [double]$Y = 0,
 [string]$KeyName = '',
 [int]$Seconds = 0
)
# Drives the normal Windows player for REPLAY-02 acceptance with Windows SendInput (real OS mouse and
# keyboard events to the focused game window) and saves client-area screenshots. Coordinates are in the
# game's 720p reference layout; the window runs at 1280x720 so one reference pixel is one client pixel.
# No smoke flags or test APIs are used: the game is started with -logFile and window-size arguments only.
$ErrorActionPreference = 'Stop'
if (-not ('AcceptanceInput' -as [type])) { Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public static class AcceptanceInput {
 [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
 [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy, mouseData, dwFlags, time; public IntPtr extra; }
 [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public short vk, scan; public int flags, time; public IntPtr extra; public int pad1, pad2; }
 [StructLayout(LayoutKind.Explicit)] struct INPUT { [FieldOffset(0)] public int type; [FieldOffset(8)] public MOUSEINPUT mi; [FieldOffset(8)] public KEYBDINPUT ki; }
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
 [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 [DllImport("user32.dll")] static extern uint SendInput(uint count, INPUT[] inputs, int size);
 public static void Click() {
  var inputs = new INPUT[2];
  inputs[0].type = 0; inputs[0].mi.dwFlags = 0x0002;
  inputs[1].type = 0; inputs[1].mi.dwFlags = 0x0004;
  SendInput(1, new[] { inputs[0] }, Marshal.SizeOf(typeof(INPUT)));
  System.Threading.Thread.Sleep(60);
  SendInput(1, new[] { inputs[1] }, Marshal.SizeOf(typeof(INPUT)));
 }
 public static void Key(short vk) {
  var down = new INPUT(); down.type = 1; down.ki.vk = vk;
  var up = new INPUT(); up.type = 1; up.ki.vk = vk; up.ki.flags = 0x0002;
  SendInput(1, new[] { down }, Marshal.SizeOf(typeof(INPUT)));
  System.Threading.Thread.Sleep(80);
  SendInput(1, new[] { up }, Marshal.SizeOf(typeof(INPUT)));
 }
 public static void Shot(IntPtr hWnd, string path) {
  POINT origin = new POINT(); ClientToScreen(hWnd, ref origin);
  RECT rect; GetClientRect(hWnd, out rect);
  using (var bitmap = new Bitmap(rect.Right, rect.Bottom))
  using (var graphics = Graphics.FromImage(bitmap)) {
   graphics.CopyFromScreen(origin.X, origin.Y, 0, 0, bitmap.Size);
   bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
  }
 }
}
'@ }
[AcceptanceInput]::SetProcessDPIAware() | Out-Null
New-Item $EvidenceDirectory -ItemType Directory -Force | Out-Null
$log = Join-Path $EvidenceDirectory 'acceptance-steps.log'
function Write-Step([string]$text) { $line = "{0:o} {1}" -f [DateTime]::UtcNow, $text; Add-Content $log $line; Write-Output $line }
function Get-GameWindow {
 $process = Get-Process RobotSavaslari -ErrorAction SilentlyContinue | Select-Object -First 1
 if (-not $process) { throw 'RobotSavaslari is not running.' }
 if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'RobotSavaslari has no window yet.' }
 return $process
}

switch ($Step) {
 'Launch' {
  if (Get-Process RobotSavaslari -ErrorAction SilentlyContinue) { throw 'RobotSavaslari is already running.' }
  $playerLog = Join-Path $EvidenceDirectory ("player-" + $Name + ".log")
  $process = Start-Process $Executable -ArgumentList @('-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',"`"$playerLog`"") -PassThru
  for ($i = 0; $i -lt 120 -and $process.MainWindowHandle -eq [IntPtr]::Zero; $i++) { Start-Sleep -Milliseconds 250; $process.Refresh() }
  Start-Sleep -Seconds 6
  Write-Step "launch $Name pid=$($process.Id) log=$playerLog"
 }
 'Click' {
  $process = Get-GameWindow
  [AcceptanceInput]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
  $point = New-Object AcceptanceInput+POINT
  $point.X = [int][Math]::Round($X); $point.Y = [int][Math]::Round($Y)
  [AcceptanceInput]::ClientToScreen($process.MainWindowHandle, [ref]$point) | Out-Null
  [AcceptanceInput]::SetCursorPos($point.X, $point.Y) | Out-Null
  Start-Sleep -Milliseconds 150
  [AcceptanceInput]::Click()
  Start-Sleep -Milliseconds 400
  Write-Step "click '$Name' at reference ($X,$Y) screen ($($point.X),$($point.Y))"
 }
 'Key' {
  $process = Get-GameWindow
  [AcceptanceInput]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
  $codes = @{ Escape = 0x1B; Enter = 0x0D; C = 0x43; H = 0x48 }
  [AcceptanceInput]::Key([int16]$codes[$KeyName])
  Start-Sleep -Milliseconds 400
  Write-Step "key $KeyName"
 }
 'Shot' {
  $process = Get-GameWindow
  $path = Join-Path $EvidenceDirectory ($Name + '.png')
  [AcceptanceInput]::Shot($process.MainWindowHandle, $path)
  Write-Step "screenshot $path"
 }
 'Wait' { Start-Sleep -Seconds $Seconds; Write-Step "waited $Seconds s" }
 'Quit' {
  $process = Get-Process RobotSavaslari -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($process) { $process.WaitForExit(20000) | Out-Null }
  if ($process -and -not $process.HasExited) { throw 'RobotSavaslari did not exit after the quit button.' }
  Write-Step "exit code $($process.ExitCode)"
 }
}
