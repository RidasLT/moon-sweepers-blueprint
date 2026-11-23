@echo off
chcp 65001 >nul
echo ==========================================
echo  Moon Sweepers Blueprint - Push to GitHub
echo ==========================================
echo.

cd /d "E:\AI-Projects\Moon Sweepers Blueprint\repo"

echo [1/5] Setting git config...
git config user.name "RidasLT"
git config user.email "RidasLT@users.noreply.github.com"

echo [2/5] Initializing repository...
git init
git branch -m main

echo [3/5] Adding files...
git add -A
git status

echo [4/5] Creating commit...
git commit -m "Initial commit: Moon Sweepers Blueprint mod source code"

echo [5/5] Pushing to GitHub...
git remote add origin https://github.com/RidasLT/moon-sweepers-blueprint.git
git push -u origin main

echo.
echo ==========================================
echo  Done! Check https://github.com/RidasLT/moon-sweepers-blueprint
echo ==========================================
pause
