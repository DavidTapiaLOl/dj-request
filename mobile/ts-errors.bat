@echo off
cd /d "C:\Users\juand\dj-requests\mobile"
npx tsc --noEmit 2>log.txt
findstr /i "provider\|dto\|client\|TS2305\|TS2307\|TS2614" log.txt > filtered.txt
type filtered.txt
echo.
echo.
echo LINE COUNT:
find /c /v "" filtered.txt