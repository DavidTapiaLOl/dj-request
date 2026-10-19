@echo off
cd /d "C:\Users\juand\dj-requests\mobile"
call npx tsc --noEmit
echo Exit code: %ERRORLEVEL%