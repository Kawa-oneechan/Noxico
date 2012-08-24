@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\vcvarsall.bat"
msbuild /property:Configuration=Debug;Platform=x64
msbuild /property:Configuration=Debug;Platform=x86
msbuild /property:Configuration=Release;Platform=x64
msbuild /property:Configuration=Release;Platform=x86

cd bin\Release
..\..\7za.exe a ..\noxico-0.1.10.7z fonts fmodex64.dll Noxico.exe
..\..\Rar.exe u ..\noxico-0.1.10.rar fonts fmodex64.dll Noxico.exe
..\..\Rar.exe u ..\noxico-music.rar music sounds
cd ..\Release32
..\..\7za.exe a ..\noxico-0.1.10-32.7z fonts fmodex.dll Noxico.exe
..\..\Rar.exe u ..\noxico-0.1.10-32.rar fonts fmodex.dll Noxico.exe
pause
