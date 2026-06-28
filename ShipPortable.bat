@echo off
setlocal

set Project=F:\FRobot\FRobot.csproj
set PubDir=F:\Binportable

if exist %PubDir% (rd %PubDir% /s /q)
md %PubDir%

echo Publishing FRobot (self-contained portable)
dotnet publish %Project% -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o %PubDir% -p:OutDir=%TEMP%\FRobotPortableBuild\
if errorlevel 1 goto :err

echo Copying native dependencies
copy N:\bin\freetype.dll %PubDir%
copy N:\bin\glfw3.dll %PubDir%

echo Copying robot mechanism
xcopy /E /I /Y N:\Wad\FanucX %PubDir%\FanucX

echo Copying GL fonts
xcopy /I /Y "N:\Wad\GL\Fonts\RobotoMono-Regular.ttf" "%PubDir%\GL\Fonts\"

echo Zipping resource files
powershell -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; if (Test-Path '%PubDir%\Nori.wad') { Remove-Item '%PubDir%\Nori.wad' }; $z=[IO.Compression.ZipFile]::Open('%PubDir%\Nori.wad','Create'); Get-ChildItem 'N:\Wad' -Recurse -File | ForEach-Object { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,$_.FullName,$_.FullName.Substring('N:\Wad\'.Length).Replace('\','/')) | Out-Null }; $z.Dispose()"
powershell -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; if (Test-Path '%PubDir%\FRobot.wad') { Remove-Item '%PubDir%\FRobot.wad' }; $z=[IO.Compression.ZipFile]::Open('%PubDir%\FRobot.wad','Create'); Get-ChildItem 'F:\Wad' -Recurse -File | ForEach-Object { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z,$_.FullName,$_.FullName.Substring('F:\Wad\'.Length).Replace('\','/')) | Out-Null }; $z.Dispose()"

echo Done. Portable build in %PubDir%
goto :eof

:err
echo Build failed.
exit /b 1
