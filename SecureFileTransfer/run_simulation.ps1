# Secure File Transfer - Dual Instance Simulation
# Mô phỏng 2 máy tính trên cùng 1 PC
# Machine A (Receiver): Listen on 127.0.0.1:9000
# Machine B (Sender): Connect to 127.0.0.1:9000

param(
    [switch]$Release = $false,
    [int]$ReceiverPort = 9000,
    [int]$delay = 2
)

$config = if ($Release) { "Release" } else { "Debug" }
$appPath = ".\src\bin\$config\net8.0-windows\SecureFileTransfer.exe"

if (-not (Test-Path $appPath)) {
    Write-Host "❌ App not found: $appPath" -ForegroundColor Red
    Write-Host "Building project first..." -ForegroundColor Yellow
    dotnet build --configuration $config
}

Write-Host "Starting SecureFileTransfer Dual Instance Simulation..." -ForegroundColor Green
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🔧 CONFIGURATION" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Build Type:       $config" -ForegroundColor Yellow
Write-Host "Receiver Port:    $ReceiverPort" -ForegroundColor Yellow
Write-Host "Sender connects:  127.0.0.1:$ReceiverPort" -ForegroundColor Yellow
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "📋 INSTRUCTIONS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "WINDOW 1 (Receiver - Machine B):" -ForegroundColor Magenta
Write-Host "  1. Select 'Receiver View'"
Write-Host "  2. Port: $ReceiverPort (already set)"
Write-Host "  3. Click 'Start Listening'"
Write-Host ""
Write-Host "WINDOW 2 (Sender - Machine A):" -ForegroundColor Magenta
Write-Host "  1. Select 'Sender View'"
Write-Host "  2. IP Address: 127.0.0.1"
Write-Host "  3. Port: $ReceiverPort"
Write-Host "  4. Select file to send"
Write-Host "  5. Enter password"
Write-Host "  6. Click 'Send'"
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan

# Start Receiver (Machine B)
Write-Host ""
Write-Host "🚀 Starting RECEIVER (Machine B)..." -ForegroundColor Green
Start-Process -FilePath $appPath -ArgumentList "--port=$ReceiverPort" -WindowStyle Normal

Write-Host "⏳ Waiting $delay seconds before starting Sender..." -ForegroundColor Yellow
Start-Sleep -Seconds $delay

# Start Sender (Machine A)  
Write-Host "🚀 Starting SENDER (Machine A)..." -ForegroundColor Green
Start-Process -FilePath $appPath -WindowStyle Normal

Write-Host ""
Write-Host "✅ Both instances started!" -ForegroundColor Green
Write-Host "Check the taskbar for 2 SecureFileTransfer windows" -ForegroundColor Cyan
