@echo off
setlocal

set "VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
set "FRAMEWORK_CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if exist "%VCVARS%" (
  call "%VCVARS%" >nul
  csc /nologo /target:winexe /optimize+ /out:xm5ui.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Xm5ControlApp.cs
  exit /b %errorlevel%
)

if not exist "%FRAMEWORK_CSC%" (
  echo Could not find vcvars64.bat or .NET Framework csc.exe.
  exit /b 1
)

"%FRAMEWORK_CSC%" /nologo /target:winexe /optimize+ /out:xm5ui.exe /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Xm5ControlApp.cs
exit /b %errorlevel%
