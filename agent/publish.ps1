# 在公司服务器（装了 .NET 8 SDK）上执行，把终端代理编译成可拷贝到 U 盘、分发给 15 台电脑的独立程序。
# 用法：在 agent 目录下执行  powershell -ExecutionPolicy Bypass -File publish.ps1
# 产物输出在 agent\dist\Service 和 agent\dist\Tracker
#
# 发布为自包含单文件（--self-contained true -p:PublishSingleFile=true）：
# 生成的 exe 自带 .NET 运行时，目标电脑不需要单独安装 .NET，对应产品说明书"单个 exe，不需要另装运行环境"的要求。

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"

Write-Host "发布 MonitorAgent.Service ..."
dotnet publish (Join-Path $root "MonitorAgent.Service\MonitorAgent.Service.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o (Join-Path $dist "Service")

Write-Host "发布 MonitorAgent.Tracker ..."
dotnet publish (Join-Path $root "MonitorAgent.Tracker\MonitorAgent.Tracker.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o (Join-Path $dist "Tracker")

Copy-Item (Join-Path $root "install.ps1") $dist -Force
Copy-Item (Join-Path $root "uninstall.ps1") $dist -Force

Write-Host ""
Write-Host "完成。把整个 dist 文件夹拷到 U 盘，带去每台电脑上运行 install.ps1 即可。"
