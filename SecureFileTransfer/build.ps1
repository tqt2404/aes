#!/bin/bash
# Build script for SecureFileTransfer
# Usage: ./build.ps1 or powershell -ExecutionPolicy Bypass -File build.ps1

Write-Host "================================" -ForegroundColor Cyan
Write-Host "SecureFileTransfer Build Script" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Check .NET SDK
Write-Host "[1/5] Checking .NET SDK..." -ForegroundColor Yellow
dotnet --version
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ .NET SDK not found!" -ForegroundColor Red
    Write-Host "Install from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectDir

# Restore dependencies
Write-Host "`n[2/5] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Restore failed!" -ForegroundColor Red
    exit 1
}

# Build Debug
Write-Host "`n[3/5] Building Debug configuration..." -ForegroundColor Yellow
dotnet build -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Debug build failed!" -ForegroundColor Red
    exit 1
}

# Build Release
Write-Host "`n[4/5] Building Release configuration..." -ForegroundColor Yellow
dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Release build failed!" -ForegroundColor Red
    exit 1
}

# Summary
Write-Host "`n[5/5] Build Summary" -ForegroundColor Yellow
Write-Host "✅ Build completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Output locations:" -ForegroundColor Cyan
Write-Host "  Debug:   bin/Debug/net8.0-windows/" -ForegroundColor Gray
Write-Host "  Release: bin/Release/net8.0-windows/" -ForegroundColor Gray
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Run:     dotnet run" -ForegroundColor Gray
Write-Host "  2. Publish: dotnet publish -c Release -r win-x64 --self-contained" -ForegroundColor Gray
Write-Host "  3. Deploy:  Copy publish/ folder to target machine" -ForegroundColor Gray
Write-Host ""
