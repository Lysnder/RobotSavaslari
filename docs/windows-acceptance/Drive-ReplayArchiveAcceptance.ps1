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
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int command);
 [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
 [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, int flags, IntPtr extra);
 // Windows only lets a process take the foreground after input; a lone Alt tap satisfies that rule.
 public static bool Focus(IntPtr hWnd) {
  if (GetForegroundWindow() == hWnd) return true;
  keybd_event(0x12, 0, 0, IntPtr.Zero); keybd_event(0x12, 0, 2, IntPtr.Zero);
  ShowWindow(hWnd, 9);
  SetForegroundWindow(hWnd);
  System.Threading.Thread.Sleep(250);
  return GetForegroundWindow() == hWnd;
 }
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
 [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
 // Unity's Input System reads keys by scan code, so the scan code is sent (KEYEVENTF_SCANCODE).
 public static void Key(short vk) {
  short scan = (short)MapVirtualKey((uint)vk, 0);
  var down = new INPUT(); down.type = 1; down.ki.scan = scan; down.ki.flags = 0x0008;
  var up = new INPUT(); up.type = 1; up.ki.scan = scan; up.ki.flags = 0x0008 | 0x0002;
  SendInput(1, new[] { down }, Marshal.SizeOf(typeof(INPUT)));
  System.Threading.Thread.Sleep(80);
  SendInput(1, new[] { up }, Marshal.SizeOf(typeof(INPUT)));
 }
 public static void Shot(IntPtr hWnd, string path) {
  // The window's own rendering (PW_RENDERFULLCONTENT), never the screen: other windows cannot appear.
  RECT rect; GetClientRect(hWnd, out rect);
  using (var bitmap = new Bitmap(rect.Right, rect.Bottom))
  using (var graphics = Graphics.FromImage(bitmap)) {
   IntPtr hdc = graphics.GetHdc();
   try { PrintWindow(hWnd, hdc, 3); } finally { graphics.ReleaseHdc(hdc); }
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
  [AcceptanceInput]::Focus($process.MainWindowHandle) | Out-Null
  Write-Step "launch $Name pid=$($process.Id) log=$playerLog"
 }
 'Click' {
  $process = Get-GameWindow
  if (-not [AcceptanceInput]::Focus($process.MainWindowHandle)) { throw 'The game window is not in front; no input was sent.' }
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
  if (-not [AcceptanceInput]::Focus($process.MainWindowHandle)) { throw 'The game window is not in front; no input was sent.' }
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
