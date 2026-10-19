@echo off
cd /d "C:\Users\juand\dj-requests\mobile"
npx expo export --platform android > export.log 2>&1
echo.
echo Exit code: %ERRORLEVEL%
type export.log