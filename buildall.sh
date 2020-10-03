#! /bin/sh

# Build script for mono on Unix-like systems

# BUILDTOOL is used to specify the name of the build program. This was tested
# working in late 2020 with both 'msbuild' and the old mono 'xbuild' programs.

# to build with xbuild, either run `BUILDTOOL=xbuild ./buildall.sh` or
# edit the declaration in this file.
if [ -z "$BUILDTOOL" ]; then # if BUILDTOOL is not already set or is empty
  BUILDTOOL="msbuild"
fi
SLNFILE="noxico.sln"

# some things use the wrong casings, which is not a problem in Windows, but
# can be an issue with (non-Apple) POSIX systems.
# this just works around that issue.
if [ ! -e "Noxico.sln" ]; then
  if [ -e "noxico.sln" ]; then
    ln -s noxico.sln Noxico.sln
  fi
fi

if [ ! -e "Noxico.csproj" ]; then
  if [ -e "noxico.csproj" ]; then
    ln -s noxico.csproj Noxico.csproj
  fi
fi

good()
{
  echo ---------------
  echo BUILD COMPLETED
  echo ---------------
  echo Packing...
  cd bin
  mkdir -p Noxico
  cp Release/Neo.Lua.dll Noxico/
  cp Release/Noxico.nox Noxico/
  cp Release/Noxico.exe Noxico/
  7z u noxico-0.1.6.1.7z Noxico/
  cp Release32/Noxico.exe Noxico/
  7z u noxico-0.1.6.1-32.7z Noxico/
  rm -rf Noxico/
}

nogood()
{
  echo ------------
  echo BUILD FAILED
  echo ------------
  exit 1
}


echo -----------
echo BUILD START
echo -----------
# Configuration strings have to be in quotes, or semicolons otherwise escaped.
"$BUILDTOOL" /nologo /v:m /p:'Configuration=Debug;Platform=x64' noxico.sln
if [ "$?" -ne 0 ]; then
  nogood
fi
"$BUILDTOOL" /nologo /v:m /p:'Configuration=Debug;Platform=x86' noxico.sln
if [ "$?" -ne 0 ]; then
  nogood
fi
"$BUILDTOOL" /nologo /v:m /p:'Configuration=Release;Platform=x64' noxico.sln
if [ "$?" -ne 0 ]; then
  nogood
fi
"$BUILDTOOL" /nologo /v:m /p:'Configuration=Release;Platform=x86' noxico.sln
if [ "$?" -ne 0 ]; then
  nogood
fi
good
exit 0
