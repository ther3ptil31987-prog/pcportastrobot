@echo off
setlocal
if not "%ASTROHYBRID_BUILD_V3%"=="1" (
  echo Opt-in only: set ASTROHYBRID_BUILD_V3=1 to build the separate x86-64-v3 experiment.
  exit /b 2
)
set "ROOT=%~dp0..\.."
powershell -NoProfile -Command "if (Get-Process AstroBot* -ErrorAction SilentlyContinue) { exit 1 }; if ((Get-PSDrive C).Free -lt 30GB -or (Get-PSDrive E).Free -lt 12GB) { exit 2 }"
if errorlevel 1 exit /b 3
set "SRC=C:\kyty-src19"
set "OUT=E:\AstroHybrid-Work\build\kyty-pgo-v3"
set "DEPS=%ROOT%\Tools\KytyPS5\_Build\windows\_deps"
set "LLVM=E:/AstroHybrid-Work/tools/LLVM-22.1.3/bin"
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 10
set "PATH=E:\AstroHybrid-Work\tools\LLVM-22.1.3\bin;C:\VulkanSDK\1.4.363.0\Bin;%PATH%"
cmake -S "%SRC%" -B "%OUT%" -G Ninja ^
  -DCMAKE_BUILD_TYPE=Release ^
  -DCMAKE_C_COMPILER="%LLVM%/clang-cl.exe" -DCMAKE_CXX_COMPILER="%LLVM%/clang-cl.exe" ^
  -DCMAKE_LINKER="%LLVM%/lld-link.exe" -DCMAKE_AR="%LLVM%/llvm-lib.exe" ^
  -DKYTY_BUILD_LAUNCHER=OFF -DKYTY_EMULATOR_IPO=ON -DKYTY_CP_DIAGNOSTICS=OFF ^
  -DKYTY_NGX_SDK_DIR=E:/AstroHybrid-Work/sdk/streamline-v2.14.16/external/ngx-sdk ^
  -DKYTY_STREAMLINE_DIR=E:/AstroHybrid-Work/sdk/streamline-v2.14.16 ^
  -DKYTY_PGO_USE=C:/kyty-src19/tools/pgo/u59-int21-1.profdata ^
  -DKYTY_MARCH=x86-64-v3 -DKYTY_BUILD_ORIGIN=Source ^
  -DFETCHCONTENT_FULLY_DISCONNECTED=ON ^
  -DFETCHCONTENT_SOURCE_DIR_OPUS="%DEPS%\opus-src" ^
  -DFETCHCONTENT_SOURCE_DIR_OPENXR="%DEPS%\openxr-src" ^
  -DFETCHCONTENT_SOURCE_DIR_XBYAK="%DEPS%\xbyak-src" ^
  -DFETCHCONTENT_SOURCE_DIR_ZYDIS="%DEPS%\zydis-src" ^
  -DFETCHCONTENT_SOURCE_DIR_ZSTD="%DEPS%\zstd-src" ^
  -DFETCHCONTENT_SOURCE_DIR_ZARCHIVE_SOURCE="%DEPS%\zarchive_source-src" || exit /b 11
cmake --build "%OUT%" --target kyty_emulator --parallel 10 || exit /b 12
set "CANDIDATE=E:\AstroHybrid-Work\build\kyty-gpu-pgo-v3-fg\kyty_gpu.dll"
set "CANDIDATE_PDB=E:\AstroHybrid-Work\build\kyty-gpu-pgo-v3-fg\kyty_gpu.pdb"
python -I "%SRC%\tools\hybrid\build_kyty_gpu.py" "%OUT%" "E:\AstroHybrid-Work\build\kyty-gpu-pgo-v3-fg" || exit /b 13

rem Deploy every successful build to the directory used by the launcher.
rem Keep a timestamped backup of the previously active DLL/PDB before replacing it.
set "DEPLOY=%ROOT%\Build\RunHybrid"
powershell -NoProfile -Command "$src=$env:CANDIDATE; $pdb=$env:CANDIDATE_PDB; $dst=$env:DEPLOY; if (!(Test-Path $src) -or !(Test-Path $pdb) -or !(Test-Path $dst)) { exit 1 }; $stamp=Get-Date -Format 'yyyyMMdd-HHmmss'; $backup=Join-Path $env:ROOT ('work\kyty-v3-deploy-backups\'+$stamp); New-Item -ItemType Directory -Force $backup | Out-Null; $active=Join-Path $dst 'kyty_gpu.dll'; $activePdb=Join-Path $dst 'kyty_gpu.pdb'; if (Test-Path $active) { Copy-Item $active (Join-Path $backup 'kyty_gpu.dll') -Force }; if (Test-Path $activePdb) { Copy-Item $activePdb (Join-Path $backup 'kyty_gpu.pdb') -Force }; Copy-Item $src $active -Force; Copy-Item $pdb $activePdb -Force; $hash=(Get-FileHash $active -Algorithm SHA256).Hash; Write-Host ('Launcher-ready: '+$active); Write-Host ('SHA256: '+$hash)" || exit /b 14
echo Build complete and deployed to the launcher: %DEPLOY%
exit /b 0
