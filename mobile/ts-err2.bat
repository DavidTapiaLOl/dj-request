@echo off
cd /d "C:\Users\juand\dj-requests\mobile"
npx tsc --noEmit 2>log.txt
findstr /i "exported member\|cannot find module\|TS2305\|TS2307" log.txt > log2.txt
type log2.txt