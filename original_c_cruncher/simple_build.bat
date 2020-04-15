@echo off

cls

echo   Compilation suggestions for Win7/10:
echo   .
echo   - The OpenWatcom compiler is zero-fuss and lightweight.
echo   - Vis Studio 2017 developer command line (via start menu)
echo   - Cygwin if you have to, lol.
echo   .

cl bytekiller.c

echo   .
echo   .
echo   Assuming that worked:
echo     bytekiller.exe test.txt crunchedText.bin
echo   .
echo   .

pause
