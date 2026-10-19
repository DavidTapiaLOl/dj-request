@echo off
cd /d "C:\Users\juand\dj-requests\mobile"
npx tsc --noEmit 2>ts-err.txt
echo.
echo Exit code: %ERRORLEVEL%
findstr /i "Cannot find module.*../../src" ts-err.txt >nul
if errorlevel 1 (
  echo No "Cannot find module ../../src" errors found in TS output
) else (
  echo "Cannot find module ../../src" errors found
  findstr /i "Cannot find module.*../../src" ts-err.txt
)