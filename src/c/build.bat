@echo off
setlocal

set "VCVARS="
for %%P in (
  "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
  "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
  "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat"
  "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Auxiliary\Build\vcvars64.bat"
  "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
  "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\VC\Auxiliary\Build\vcvars64.bat"
  "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\VC\Auxiliary\Build\vcvars64.bat"
  "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\VC\Auxiliary\Build\vcvars64.bat"
) do (
  if not defined VCVARS if exist "%%~P" set "VCVARS=%%~P"
)

if defined VCVARS (
  call "%VCVARS%" >nul
) else (
  where cl.exe >nul 2>nul
  if errorlevel 1 (
    echo Could not find Visual Studio C++ Build Tools or cl.exe on PATH.
    exit /b 1
  )
)

cl /nologo /W4 /O2 /D_CRT_SECURE_NO_WARNINGS /Fe:xm5ctl.exe xm5ctl.c ws2_32.lib bthprops.lib
exit /b %errorlevel%
