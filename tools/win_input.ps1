$ErrorActionPreference = 'SilentlyContinue'
$log = 'C:\SuperSqij\input.log'
"start $(Get-Date -Format o)" | Set-Content $log
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class SqijWin {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    public static IntPtr Find(int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid && IsWindowVisible(h)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
    public static void Tap(byte vk) {
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        System.Threading.Thread.Sleep(60);
        keybd_event(vk, 0, 2, UIntPtr.Zero);
        System.Threading.Thread.Sleep(70);
    }
}
"@
$proc = Get-Process SuperSqij | Select-Object -First 1
$h = [SqijWin]::Find($proc.Id)
"pid=$($proc.Id) hwnd=$h" | Add-Content $log
[SqijWin]::ShowWindow($h, 9) | Out-Null
[SqijWin]::BringWindowToTop($h) | Out-Null
[SqijWin]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 600
"fg=$([SqijWin]::GetForegroundWindow()) target=$h" | Add-Content $log
# ENTER to start
[SqijWin]::Tap(0x0D)
"sent enter" | Add-Content $log
Start-Sleep -Milliseconds 2000
# walk right
for ($i = 0; $i -lt 18; $i++) { [SqijWin]::Tap(0x27) }
"sent right" | Add-Content $log
[SqijWin]::Tap(0x20)
Start-Sleep -Milliseconds 150
[SqijWin]::Tap(0x20)
"sent flap" | Add-Content $log
for ($i = 0; $i -lt 4; $i++) { [SqijWin]::Tap(0x58) }
"sent fire" | Add-Content $log
Start-Sleep -Milliseconds 1200
"done $(Get-Date -Format o)" | Add-Content $log
