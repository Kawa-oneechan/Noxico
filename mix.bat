rem MixMaster mix Noxico.mix
rem MixMaster sounds Sounds.mix
rem MixMaster music Music.mix
del Noxico.nox
del Music.nox
del Sound.nox
cd mix
..\7za.exe u -r ..\Noxico.zip *.* > nul
cd ../music
..\7za.exe u -r ..\Music.zip *.* > nul
cd ../sound
..\7za.exe u -r ..\Sound.zip *.* > nul
cd ..
ren Noxico.zip Noxico.nox
ren Music.zip Music.nox
ren Sound.zip Sound.nox