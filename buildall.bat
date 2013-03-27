@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\vcvarsall.bat"
echo Building MIX files...
MixMaster mix Noxico.mix
MixMaster sounds Sounds.mix
MixMaster music Music.mix
echo -----------
echo BUILD START
echo -----------
msbuild /nologo /v:m /p:Configuration=Debug;Platform=x64
if %errorlevel% neq 0 goto nogood
msbuild /nologo /v:m /p:Configuration=Debug;Platform=x86
if not errorlevel 0 goto nogood
msbuild /nologo /v:m /p:Configuration=Release;Platform=x64
if not errorlevel 0 goto nogood
msbuild /nologo /v:m /p:Configuration=Release;Platform=x86
if not errorlevel 0 goto nogood
:good
echo ---------------
echo BUILD COMPLETED
echo ---------------
echo Packing...
cd bin\Release
..\..\7za.exe a ..\noxico-0.1.2.7z fmodex64.dll Jint.dll Antlr3.Runtime.dll Noxico.mix Noxico.exe > nul
..\..\Rar.exe u ..\noxico-0.1.2.rar fmodex64.dll Jint.dll Antlr3.Runtime.dll Noxico.mix Noxico.exe > nul
..\..\Rar.exe u ..\noxico-music.rar Music.mix Sounds.mix > nul
cd ..\Release32
..\..\7za.exe a ..\noxico-0.1.2-32.7z fmodex.dll Jint.dll Antlr3.Runtime.dll Noxico.mix Noxico.exe > nul
..\..\Rar.exe u ..\noxico-0.1.2-32.rar fmodex.dll Jint.dll Antlr3.Runtime.dll Noxico.mix Noxico.exe > nul
cd ..\..
pause
exit /b 0
:nogood
echo ------------
echo BUILD FAILED
echo ------------
pause
exit /b 1
