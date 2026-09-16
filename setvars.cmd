@echo off
set "MYHOTSINFO_ROOT=%~dp0"
set "MYHOTSINFO_ROOT=%MYHOTSINFO_ROOT:~0,-1%"
set "PATH=%PATH%;%MYHOTSINFO_ROOT%\bin;%MYHOTSINFO_ROOT%\bin\pub;c:\program files\cmake\bin;w:\vcpkg"
set "VCPKG_HOME=w:\vcpkg"
set "MYHOTSINFO_DB=%MYHOTSINFO_ROOT%\my.db"
