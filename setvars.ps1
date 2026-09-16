$root = $PSScriptRoot
$env:PATH = "$env:PATH;$root\bin;$root\bin\pub;C:\Program Files\cmake\bin;w:\vcpkg"
$env:VCPKG_HOME = "w:\vcpkg"
# Database used by MyHotsCli (and the MAUI app before a db file is picked). See README "Configuration".
$env:MYHOTSINFO_DB = "$root\my.db"
