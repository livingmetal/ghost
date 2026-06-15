@echo off
chcp 65001 > nul
setlocal

cd /d "%~dp0"

where py > nul 2> nul
if %errorlevel% equ 0 (
  set PY=py
) else (
  where python > nul 2> nul
  if %errorlevel% equ 0 (
    set PY=python
  ) else (
    echo [ERROR] Python을 찾을 수 없습니다.
    echo         https://www.python.org/downloads/ 에서 Python 설치 후 다시 실행하세요.
    pause
    exit /b 1
  )
)

%PY% - <<PYTEST 2> nul
import PIL
PYTEST
if not %errorlevel% equ 0 (
  echo [INFO] Pillow가 없어 설치합니다...
  %PY% -m pip install pillow
  if not %errorlevel% equ 0 (
    echo [ERROR] Pillow 설치 실패
    pause
    exit /b 1
  )
)

echo [INFO] 쑝 스프라이트 정규화 시작
%PY% .\normalize_sprites.py --repair-empty

echo.
echo 결과는 normalized 폴더에 생성됩니다.
echo 확인 후 원본을 덮어쓰려면 아래 명령을 실행하세요:
echo   %PY% .\normalize_sprites.py --repair-empty --apply
echo.
pause
