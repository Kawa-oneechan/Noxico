@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio 10.0\VC\vcvarsall.bat"
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
cd bin
md Noxico
copy Release\Jint.dll Noxico > nul
copy Release\Antlr3.Runtime.dll Noxico > nul
copy Release\Noxico.nox Noxico > nul
copy Release\Noxico.exe Noxico > nul
..\7za.exe u noxico-0.1.5.7z Noxico > nul
copy /y Release32\Noxico.exe Noxico > nul
..\7za.exe u  noxico-0.1.5-32.7z Noxico > nul
rd Noxico /s /q
cd ..
pause
exit /b 0
:nogood
echo ------------
echo BUILD FAILED
echo ------------
pause
exit /b 1
