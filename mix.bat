del *.nox
cd mix
..\7za.exe u -r ..\Noxico.zip *.* > nul
cd ../music
..\7za.exe u -r ..\Music.zip *.* > nul
cd ../sound
..\7za.exe u -r ..\Sound.zip *.* > nul
cd ..
ren *.zip *.nox
