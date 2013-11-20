rem MixMaster mix Noxico.mix
del Noxico.nox
cd mix
..\7za.exe u -r ..\Noxico.zip *.* > nul
cd..
ren Noxico.zip Noxico.nox