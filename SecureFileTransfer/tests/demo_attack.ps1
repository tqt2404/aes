# Security Demo: HMAC Integrity Protection (Bit-flipping Attack)
$DemoDir = Join-Path $PSScriptRoot "attack_demo"
if (Test-Path $DemoDir) { Remove-Item $DemoDir -Recurse -Force }
New-Item -ItemType Directory -Path $DemoDir | Out-Null

$OriginalFile = Join-Path $DemoDir "top_secret.txt"
$EncryptedFile = Join-Path $DemoDir "top_secret.enc"
$DecryptedFile = Join-Path $DemoDir "recovered.txt"
$Password = "DemoPass123!"

# 1. Prepare Data
"THONG TIN MAT QUOC GIA: Ngay 31.03.2026 thuc hien kiem tra bao mat." | Out-File -FilePath $OriginalFile -Encoding utf8
Write-Host "`n[1] Du lieu goc:" -ForegroundColor Cyan
Get-Content $OriginalFile

# 2. Create C# Runner
$Program = @"
using SecureFileTransfer.Security;
using System;
using System.IO;

var aes = new AesService();
string cmd = args[0];
try {
    if (cmd == \"encrypt\") {
        aes.EncryptFile(\"$($OriginalFile.Replace('\','\\'))\", \"$($EncryptedFile.Replace('\','\\'))\", \"$Password\");
        Console.WriteLine(\"MA HOA THANH CONG.\");
    } else {
        aes.DecryptFile(\"$($EncryptedFile.Replace('\','\\'))\", \"$($DecryptedFile.Replace('\','\\'))\", \"$Password\");
        Console.WriteLine(\"GIAI MA THANH CONG.\");
    }
} catch (Exception ex) {
    Console.WriteLine(\"LOI BAO MAT: \" + ex.Message);
}
"@

# 3. Setup Temp Project
dotnet new console -n DemoRunner -o $DemoDir --force > $null
$Program | Out-File -FilePath (Join-Path $DemoDir "Program.cs") -Encoding utf8
dotnet add "$DemoDir/DemoRunner.csproj" reference "$PSScriptRoot/../src/SecureFileTransfer.csproj" > $null

# 4. Run Encryption
Write-Host "`n[2] Dang ma hoa tep tin..." -ForegroundColor Yellow
dotnet run --project "$DemoDir/DemoRunner.csproj" -- encrypt

# 5. Simulate Attack (Bit-flip)
Write-Host "`n[3] Can thiep vat ly: Tan cong Bit-flipping tai offset 40..." -ForegroundColor Red
$bytes = [System.IO.File]::ReadAllBytes($EncryptedFile)
$bytes[40] = $bytes[40] -bxor 0xFF
[System.IO.File]::WriteAllBytes($EncryptedFile, $bytes)

# 6. Attempt Decryption
Write-Host "`n[4] Thu giai ma tep tin da bi thau tom..." -ForegroundColor Yellow
dotnet run --project "$DemoDir/DemoRunner.csproj" -- decrypt

# Final Cleanup
if (Test-Path $DemoDir) { Remove-Item $DemoDir -Recurse -Force }
