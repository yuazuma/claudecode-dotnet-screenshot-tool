@echo off
REM AutoScreenshot ビルド / パブリッシュスクリプト
setlocal

set DOTNET="C:\Program Files\dotnet\dotnet.exe"
set PROJECT=src\AutoScreenshot\AutoScreenshot.csproj

if "%1"=="publish" goto publish

:build
echo [build] Release ビルド中...
%DOTNET% build %PROJECT% -c Release --nologo
if errorlevel 1 ( echo [ERROR] ビルド失敗 & exit /b 1 )
echo [done] ビルド完了 (src\AutoScreenshot\bin\Release\)
goto end

:publish
echo [publish] 自己完結型単一 exe を publish/ に出力中...
%DOTNET% publish %PROJECT% -p:PublishProfile=win-x64-release --nologo
if errorlevel 1 ( echo [ERROR] パブリッシュ失敗 & exit /b 1 )
echo [done] publish\AutoScreenshot.exe が生成されました。

:end
endlocal
