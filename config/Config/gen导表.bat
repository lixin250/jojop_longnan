@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set WORKSPACE=%~dp0..
set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_ROOT=%~dp0
set LOG=%TEMP%\jojop_luban_gen.log
set RESULT=0

echo [JojoP] gen start...
echo.

if not exist "%LUBAN_DLL%" (
  echo [JojoP] Luban.dll missing: %LUBAN_DLL%
  set RESULT=1
  goto :done
)

dotnet "%LUBAN_DLL%" -t client -c cs-simple-json -d json --conf "%CONF_ROOT%luban.conf" >"%LOG%" 2>&1
set ERR=%ERRORLEVEL%
type "%LOG%"
echo.

findstr /C:"|ERROR|" "%LOG%" >nul
if not errorlevel 1 (
  echo [JojoP] FAILED - log contains ERROR
  echo tip: comment row first cell must be exactly ## ; put section title in column B
  set RESULT=1
  goto :done
)

if not "%ERR%"=="0" (
  echo [JojoP] FAILED exit=%ERR%
  set RESULT=%ERR%
  goto :done
)

echo [JojoP] OK code -^> Assets/Script/LubanCode/Gen
echo [JojoP] OK data -^> Assets/Bundle/LubanConfig
echo [JojoP] SUCCESS
echo bye~

:done
echo.
echo ----------------------------------------
if "%RESULT%"=="0" (
  echo Result: SUCCESS - press any key to close
) else (
  echo Result: FAILED code=%RESULT% - press any key to close
)
pause >nul
endlocal & exit /b %RESULT%
