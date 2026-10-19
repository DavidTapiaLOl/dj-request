@echo off
cd /d "C:\Users\juand\dj-requests\mobile"
npx tsc --noEmit 2>log.txt
findstr /i "Cannot find module.*../../src" log.txt >nul
if errorlevel 1 (
  echo No "Cannot find module ../../src" errors
) else (
  findstr /i "Cannot find module.*../../src" log.txt
)