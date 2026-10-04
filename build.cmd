@echo off
rem Rebuilds BrowserSwitch.exe using the C# compiler that ships inside Windows.
rem Nothing is downloaded and nothing needs to be installed.
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ ^
  /out:"%~dp0BrowserSwitch.exe" /win32icon:"%~dp0BrowserSwitch.ico" ^
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ^
  "%~dp0BrowserSwitch.cs" "%~dp0SwitchForm.cs" "%~dp0Tray.cs" "%~dp0Shortcuts.cs" ^
  "%~dp0Rules.cs" "%~dp0Ui.cs" "%~dp0FileIcon.cs" "%~dp0Setup.cs" "%~dp0Cleaner.cs" "%~dp0LinkLog.cs" "%~dp0Updater.cs" "%~dp0Ask.cs" "%~dp0Guide.cs" "%~dp0Sites.cs"
if errorlevel 1 (echo BUILD FAILED & pause & exit /b 1)
rem LinkPilotSetup.exe - the installer, which the app's own updater downloads: installer\*.cs and Ui.cs,
rem with the program files above inside it. setup.manifest keeps Windows from asking for admin rights.
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:"%~dp0LinkPilotSetup.exe" /win32icon:"%~dp0BrowserSwitch.ico" /win32manifest:"%~dp0installer\setup.manifest" ^
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ^
  "/resource:%~dp0BrowserSwitch.exe,BrowserSwitch.exe" "/resource:%~dp0install.ps1,install.ps1" ^
  "/resource:%~dp0uninstall.ps1,uninstall.ps1" "/resource:%~dp0Install.cmd,Install.cmd" ^
  "/resource:%~dp0Back to normal.cmd,Back to normal.cmd" "/resource:%~dp0README.md,README.md" "/resource:%~dp0LICENSE,LICENSE" ^
  "%~dp0installer\LinkPilotSetup.cs" "%~dp0installer\SetupSteps.cs" "%~dp0Ui.cs"
if errorlevel 1 (echo BUILD FAILED & pause & exit /b 1)
echo Built BrowserSwitch.exe and LinkPilotSetup.exe
