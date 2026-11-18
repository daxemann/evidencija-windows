# Izgradnja instalacije: testovi → objava (self-contained) → Inno Setup → SHA-256
# Upotreba (PowerShell, u mapi repozitorija):  .\alati\izgradi.ps1
$ErrorActionPreference = 'Stop'
$korijen = Split-Path $PSScriptRoot -Parent
Set-Location $korijen

[xml]$proj = Get-Content "src\Evidencija\Evidencija.csproj" -Encoding UTF8
$verzija = ($proj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
Write-Host "Verzija $verzija"

dotnet test EvidencijaWindows.sln -c Release
if ($LASTEXITCODE -ne 0) { throw "Testovi nisu prošli – instalacija se ne gradi." }

Remove-Item -Recurse -Force izlaz -ErrorAction SilentlyContinue
dotnet publish src\Evidencija\Evidencija.csproj -c Release -o izlaz\program
if ($LASTEXITCODE -ne 0) { throw "Objava nije uspjela." }

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 nije instaliran (winget install JRSoftware.InnoSetup)." }
if (-not (Test-Path "instalacija\MicrosoftEdgeWebview2Setup.exe")) { throw "Nedostaje instalacija\MicrosoftEdgeWebview2Setup.exe (Microsoftov WebView2 bootstrapper)." }

& $iscc "/DVerzija=$verzija" "instalacija\Evidencija.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup nije uspio." }

$setup = "izlaz\Evidencija-Setup-$verzija.exe"
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
"$hash  Evidencija-Setup-$verzija.exe" | Set-Content "izlaz\Evidencija-Setup-$verzija.sha256.txt" -Encoding ascii
Write-Host ("Gotovo: {0} ({1:N1} MB)`nSHA-256: {2}" -f $setup, ((Get-Item $setup).Length / 1MB), $hash)
