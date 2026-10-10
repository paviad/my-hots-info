@echo off
rem Starts the MCP server from a copy of its build, so rebuilding while a Claude Code
rem session runs the server isn't blocked by locked files. Restart the server (/mcp) to
rem pick up a new build. stdout carries the MCP protocol: nothing else may write to it.
rem .mcp.json runs this as `cmd /c "set NoDefaultCurrentDirectoryInExePath=&& cd MyHotsObserver && run.cmd"`:
rem a backslash path there gets its \r read as a carriage return, and with that variable set
rem cmd skips the current folder and runs another run.cmd from PATH (nvm has one).
setlocal
set "SRC=%~dp0bin\Debug\net10.0"
set "ROOT=%~dp0runs"
if not defined MYHOTSINFO_TESSDATA set "MYHOTSINFO_TESSDATA=%~dp0..\Ocr\tessdata"

rem Remove copies whose server has exited; a running one's folder can't be renamed.
if exist "%ROOT%" for /d %%D in ("%ROOT%\*") do (
    ren "%%D" "%%~nxD.old" 2>nul && rd /s /q "%%D.old" 2>nul
)

set "DST=%ROOT%\%RANDOM%%RANDOM%"
robocopy "%SRC%" "%DST%" /e /njh /njs /nfl /ndl /np >nul 2>&1
if errorlevel 8 (
    echo Couldn't copy %SRC% to %DST% 1>&2
    exit /b 1
)
"%DST%\MyHotsObserver.exe" %*
