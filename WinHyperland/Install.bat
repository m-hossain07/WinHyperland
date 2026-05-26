@echo off
title WinHyperland Installer
echo ===================================================
echo Welcome to the WinHyperland Installer
echo ===================================================
echo.
echo This will install WinHyperland to your Program Files.
echo Please grant Administrator permissions when prompted.
echo.
pause

powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -Wait -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -Command \"$src = ''%~dp0Publish\x64''; $dst = ''$env:ProgramFiles\WinHyperland''; Write-Host ''Stopping any running instances...''; Stop-Process -Name ''WinHyperland'' -Force -ErrorAction SilentlyContinue; Write-Host ''Uninstalling old version...''; if (Test-Path $dst) { Remove-Item -Path $dst -Recurse -Force }; Write-Host ''Copying new single-file app...''; New-Item -ItemType Directory -Force -Path $dst | Out-Null; Copy-Item -Path $src\* -Destination $dst -Recurse -Force; Copy-Item -Path ''%~dp0logo.ico'' -Destination ''$dst\logo.ico'' -Force; Write-Host ''Creating shortcuts...''; $wsh = New-Object -ComObject WScript.Shell; $sc = $wsh.CreateShortcut(''$env:ProgramData\Microsoft\Windows\Start Menu\Programs\WinHyperland.lnk''); $sc.TargetPath = ''$dst\WinHyperland.exe''; $sc.IconLocation = ''$dst\logo.ico''; $sc.Save(); $dsc = $wsh.CreateShortcut(''$env:USERPROFILE\Desktop\WinHyperland.lnk''); $dsc.TargetPath = ''$dst\WinHyperland.exe''; $dsc.IconLocation = ''$dst\logo.ico''; $dsc.Save(); Write-Host ''Starting WinHyperland...''; Start-Process ''$dst\WinHyperland.exe''; Write-Host ''''; Write-Host ''==========================================''; Write-Host ''SUCCESS! New Single-File WinHyperland Installed!''; Write-Host ''==========================================''; Write-Host ''Press any key to exit.''; [Console]::ReadKey()\"'"
