@echo off
title WinHyperisland Installer
echo ===================================================
echo Welcome to the WinHyperisland Installer
echo ===================================================
echo.
echo This will install WinHyperisland to your Program Files.
echo Please grant Administrator permissions when prompted.
echo.
pause

powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -Wait -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -Command \"$src = ''%~dp0Publish\x64''; $dst = ''$env:ProgramFiles\WinHyperisland''; Write-Host ''Stopping any running instances...''; Stop-Process -Name ''WinHyperisland'' -Force -ErrorAction SilentlyContinue; Write-Host ''Uninstalling old version...''; if (Test-Path $dst) { Remove-Item -Path $dst -Recurse -Force }; Write-Host ''Copying new single-file app...''; New-Item -ItemType Directory -Force -Path $dst | Out-Null; Copy-Item -Path $src\* -Destination $dst -Recurse -Force; Copy-Item -Path ''%~dp0logo.ico'' -Destination ''$dst\logo.ico'' -Force; Write-Host ''Creating shortcuts...''; $wsh = New-Object -ComObject WScript.Shell; $sc = $wsh.CreateShortcut(''$env:ProgramData\Microsoft\Windows\Start Menu\Programs\WinHyperisland.lnk''); $sc.TargetPath = ''$dst\WinHyperisland.exe''; $sc.IconLocation = ''$dst\logo.ico''; $sc.Save(); $dsc = $wsh.CreateShortcut(''$env:USERPROFILE\Desktop\WinHyperisland.lnk''); $dsc.TargetPath = ''$dst\WinHyperisland.exe''; $dsc.IconLocation = ''$dst\logo.ico''; $dsc.Save(); Write-Host ''Starting WinHyperisland...''; Start-Process ''$dst\WinHyperisland.exe''; Write-Host ''''; Write-Host ''==========================================''; Write-Host ''SUCCESS! New Single-File WinHyperisland Installed!''; Write-Host ''==========================================''; Write-Host ''Press any key to exit.''; [Console]::ReadKey()\"'"
