@echo off
C:\Users\y\AppData\Local\Microsoft\dotnet\dotnet.exe build C:\Users\y\Documents\GitHub\claudecode-dotnet-screenshot-tool\src\AutoScreenshot\AutoScreenshot.csproj -c Debug > C:\Users\y\Documents\GitHub\claudecode-dotnet-screenshot-tool\build_output.txt 2>&1
echo Exit code: %ERRORLEVEL% >> C:\Users\y\Documents\GitHub\claudecode-dotnet-screenshot-tool\build_output.txt
dir C:\Users\y\Documents\GitHub\claudecode-dotnet-screenshot-tool\src\AutoScreenshot\bin\Debug\net8.0-windows\AutoScreenshot.dll >> C:\Users\y\Documents\GitHub\claudecode-dotnet-screenshot-tool\build_output.txt 2>&1
