param([string]$Stage = 'final')
Add-Type -AssemblyName System.Drawing
$taskRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$taskReference = Join-Path $PSScriptRoot 'Reference/selected-option-3.png'
$taskCapture = Join-Path $taskRoot '.codex-tmp/UIVerificationProject/.codex-tmp/UIReferenceAlignmentVerification/00b-introduction.png'
$taskOutput = Join-Path $PSScriptRoot 'Comparisons'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskRef = [System.Drawing.Image]::FromFile($taskReference)
$taskActual = [System.Drawing.Image]::FromFile($taskCapture)
try {
    $taskWidth = $taskRef.Width; $taskHeight = $taskRef.Height
    $taskNormalized = [System.Drawing.Bitmap]::new($taskWidth, $taskHeight)
    $taskG = [System.Drawing.Graphics]::FromImage($taskNormalized)
    $taskG.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $taskG.DrawImage($taskActual, [System.Drawing.Rectangle]::new(0, 0, $taskWidth, $taskHeight))
    $taskG.Dispose()
    $taskSections = @(
        @{Name='full'; Rect=[System.Drawing.Rectangle]::new(0,0,$taskWidth,$taskHeight)},
        @{Name='navigation'; Rect=[System.Drawing.Rectangle]::new(0,0,$taskWidth,100)},
        @{Name='hero-copy'; Rect=[System.Drawing.Rectangle]::new(0,85,620,460)},
        @{Name='blueprint-actions'; Rect=[System.Drawing.Rectangle]::new(0,540,$taskWidth,401)}
    )
    foreach ($taskSection in $taskSections) {
        $taskR = $taskSection.Rect
        $taskBoard = [System.Drawing.Bitmap]::new($taskR.Width * 2 + 24, $taskR.Height + 32)
        $taskCanvas = [System.Drawing.Graphics]::FromImage($taskBoard)
        $taskCanvas.Clear([System.Drawing.Color]::FromArgb(235,240,243))
        $taskFont = [System.Drawing.Font]::new('Segoe UI', 12)
        $taskCanvas.DrawString('SELECTED OPTION 3', $taskFont, [System.Drawing.Brushes]::Black, 5, 5)
        $taskCanvas.DrawString('UNITY / '+$Stage, $taskFont, [System.Drawing.Brushes]::Black, $taskR.Width+29, 5)
        $taskCanvas.DrawImage($taskRef, [System.Drawing.Rectangle]::new(0,32,$taskR.Width,$taskR.Height), $taskR, [System.Drawing.GraphicsUnit]::Pixel)
        $taskCanvas.DrawImage($taskNormalized, [System.Drawing.Rectangle]::new($taskR.Width+24,32,$taskR.Width,$taskR.Height), $taskR, [System.Drawing.GraphicsUnit]::Pixel)
        $taskFile = Join-Path $taskOutput ($Stage+'-'+$taskSection.Name+'.png')
        $taskBoard.Save($taskFile, [System.Drawing.Imaging.ImageFormat]::Png)
        $taskCanvas.Dispose(); $taskBoard.Dispose(); $taskFont.Dispose()
        Write-Output $taskFile
    }
    $taskNormalized.Dispose()
} finally { $taskRef.Dispose(); $taskActual.Dispose() }
