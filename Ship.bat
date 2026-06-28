@echo off
setlocal

set Project=F:\FRobot\FRobot.csproj
set PubDir=F:\Publish\

if exist %PubDir% (rd %PubDir% /s /q)
md %PubDir%

echo Publishing FRobot
dotnet publish %Project% -c Release -o %PubDir%

echo Zipping resource files
call :Zip N:\Wad\* Nori.wad
call :Zip F:\Wad\* FRobot.wad

echo Running Util tool
dotnet run --project Tools\Installer\VersionInjector

echo Running Inno Setup Compiler
Y:\Tools\Inno\ISCC.exe F:\Tools\Installer\FRobot.iss

REM Cleanup
del F:\Tools\Installer\FRobot.iss
rd %PubDir% /s /q

goto :eof
:Zip
7z a -r -tzip %PubDir%\%2 %1
goto :eof
