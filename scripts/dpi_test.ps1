param(
    [string]$OutDir = ".\artifacts\dpi-tests",
    [array]$Rects = $null
)

Set-StrictMode -Version Latest

function Ensure-OutputDir {
    param([string]$d)
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d | Out-Null }
}

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

# Add native DPI helper via C# to call GetDpiForMonitor
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
namespace Shuyu.Tests {
    public static class DpiUtil {
        private const int MDT_EFFECTIVE_DPI = 0;
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int x; public int y; public POINT(int x,int y){this.x=x;this.y=y;} }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

        public static int[] GetDpiForPoint(int x, int y) {
            try {
                var pt = new POINT(x,y);
                var mon = MonitorFromPoint(pt, 2); // MONITOR_DEFAULTTONEAREST
                if (mon == IntPtr.Zero) return new int[]{96,96};
                if (GetDpiForMonitor(mon, MDT_EFFECTIVE_DPI, out uint dx, out uint dy) == 0) {
                    return new int[]{(int)dx, (int)dy};
                }
            } catch { }
            return new int[]{96,96};
        }
    }
}
"@ -Language CSharp

function Capture-Region {
    param(
        [int]$left,
        [int]$top,
        [int]$width,
        [int]$height,
        [string]$outPath
    )

    $bmp = New-Object System.Drawing.Bitmap($width, $height)
    $g = $null
    try {
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($left, $top, 0, 0, [System.Drawing.Size]::new($width, $height), [System.Drawing.CopyPixelOperation]::SourceCopy)
        $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $info = @{ Path = $outPath; Width = $bmp.Width; Height = $bmp.Height }
        return $info
    }
    catch {
        Write-Warning "Capture-Region failed for $outPath : $($_.Exception.Message)"
        return @{ Path = $outPath; Width = 0; Height = 0; Error = $_.Exception.Message }
    }
    finally {
        if ($null -ne $g) { $g.Dispose() }
        if ($null -ne $bmp) { $bmp.Dispose() }
    }
}

function Get-VirtualScreenBounds {
    $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
    return @{ Left = $vs.Left; Top = $vs.Top; Width = $vs.Width; Height = $vs.Height }
}

Ensure-OutputDir -d $OutDir

$vs = Get-VirtualScreenBounds
Write-Output "VirtualScreen: Left=$($vs.Left), Top=$($vs.Top), Width=$($vs.Width), Height=$($vs.Height)"

# Capture full virtual screen
$fullOut = Join-Path $OutDir "virtual_full.png"
$fullInfo = Capture-Region -left $vs.Left -top $vs.Top -width $vs.Width -height $vs.Height -outPath $fullOut
if ($null -ne $fullInfo -and $fullInfo.ContainsKey('Width') -and $fullInfo.ContainsKey('Height')) {
    Write-Output "Captured full screen -> $($fullInfo['Path']) ($($fullInfo['Width'])x$($fullInfo['Height']))"
} else {
    Write-Warning "Captured full screen but result is invalid: $($fullInfo | Out-String)"
}

# Default test rects if none provided: center and near edges (DIP assumptions)
if (-not $Rects) {
    $centerX = [int]($vs.Left + $vs.Width / 2)
    $centerY = [int]($vs.Top + $vs.Height / 2)
    $Rects = @(
        @{ L = $centerX - 100; T = $centerY - 50; W = 200; H = 100; Name = 'center_200x100' },
        @{ L = $vs.Left + 10; T = $vs.Top + 10; W = 300; H = 150; Name = 'top_left_300x150' },
        @{ L = $vs.Left + $vs.Width - 310; T = $vs.Top + $vs.Height - 160; W = 300; H = 150; Name = 'bottom_right_300x150' }
    )
}

$results = @()
foreach ($r in $Rects) {
    $name = $r.Name
    if (-not $name) { $name = "rect_${($r.L)}_${($r.T)}" }
    $out = Join-Path $OutDir ("$name.png")

    # Treat W/H as DIP (device-independent pixels). Get monitor DPI at rect center and compute expected pixel size.
    $centerX = [int]($r.L + ($r.W / 2))
    $centerY = [int]($r.T + ($r.H / 2))
    $dpis = [Shuyu.Tests.DpiUtil]::GetDpiForPoint($centerX, $centerY)
    $dpiX = $dpis[0]
    $dpiY = $dpis[1]
    $scaleX = $dpiX / 96.0
    $scaleY = $dpiY / 96.0

    $expectedW = [int][Math]::Round($r.W * $scaleX)
    $expectedH = [int][Math]::Round($r.H * $scaleY)

    # Guard: if DPI helper failed and produced 0, fall back to DIP values to avoid Bitmap(0,0)
    if ($expectedW -lt 1 -or $expectedH -lt 1) {
        Write-Warning "Computed expected size is ${expectedW}x${expectedH}, falling back to DIP sizes ${($r.W)}x${($r.H)}"
        $expectedW = [int]$r.W
        $expectedH = [int]$r.H
    }

    Write-Output "[$name] center=($centerX,$centerY) dpi=($dpiX,$dpiY) scale=($([math]::Round($scaleX,2)),$([math]::Round($scaleY,2))) expected=${expectedW}x${expectedH}"

    $info = Capture-Region -left $r.L -top $r.T -width $expectedW -height $expectedH -outPath $out
    if ($null -ne $info -and $info.ContainsKey('Width') -and $info.ContainsKey('Height')) {
        $pass = ($info['Width'] -eq $expectedW) -and ($info['Height'] -eq $expectedH)
        if ($pass) {
            Write-Output "Captured $name -> $($info['Path']) ($($info['Width'])x$($info['Height'])) [OK]"
        } else {
            Write-Warning "Captured $name -> $($info['Path']) ($($info['Width'])x$($info['Height'])) [NG expected ${expectedW}x${expectedH}]"
        }
    }
    else {
        Write-Warning "Captured $name but result is invalid: $($info | Out-String)"
    }
    $results += $info
}

Write-Output "Summary:"
foreach ($i in $results) {
    if ($null -ne $i -and $i.ContainsKey('Width') -and $i.ContainsKey('Height')) {
        Write-Output " - $($i['Path']): $($i['Width'])x$($i['Height'])"
    } else {
        Write-Output " - $($i['Path']): invalid result -> $($i | Out-String)"
    }
}

Write-Output "DPI test captures saved to: $OutDir"
