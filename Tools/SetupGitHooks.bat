@echo off
rem Enable Git hooks (.githooks). For designers who do not open Unity.
rem Run once (double-click) after cloning.
cd /d "%~dp0.."
git config core.hooksPath .githooks
if errorlevel 1 (
    echo [NG] Failed. Please check that Git is installed.
) else (
    echo [OK] Git hooks enabled: .githooks
)
pause
