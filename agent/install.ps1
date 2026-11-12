# 终端行为监控系统 —— 单台电脑安装脚本
# 必须以管理员身份运行（右键 PowerShell -> 以管理员身份运行）。
#
# 用法示例：
#   powershell -ExecutionPolicy Bypass -File install.ps1 -ServerUrl "http://192.168.1.10:5208"
#
# 本脚本假定与自己同目录下有 Service\ 和 Tracker\ 两个文件夹（由 publish.ps1 生成）。

param(
    [Parameter(Mandatory = $true)]
    [string]$ServerUrl,

    [string]$ApiKey = ""
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsIdentity]::GetCurrent().Groups -contains "S-1-5-32-544")) {
    Write-Warning "建议以管理员身份运行本脚本，否则安装服务可能会失败。"
}

$root = $PSScriptRoot
$installDir = "C:\Program Files\MonitorAgent"
$dataDir = "C:\ProgramData\MonitorAgent"
$serviceName = "MonitorAgentService"

Write-Host "1. 创建安装目录与配置目录..."
New-Item -ItemType Directory -Force -Path "$installDir\Service" | Out-Null
New-Item -ItemType Directory -Force -Path "$installDir\Tracker" | Out-Null
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

# 普通账户用户需要能写配置/队列文件
icacls $dataDir /grant "Users:(OI)(CI)M" | Out-Null

Write-Host "2. 写入配置文件 ($dataDir\agent.config.json) ..."
$config = @{
    ServerUrl = $ServerUrl
    ApiKey    = $ApiKey
    HostName  = $env:COMPUTERNAME
} | ConvertTo-Json
Set-Content -Path (Join-Path $dataDir "agent.config.json") -Value $config -Encoding UTF8

Write-Host "3. 复制程序文件..."
Copy-Item (Join-Path $root "Service\*") "$installDir\Service" -Recurse -Force
Copy-Item (Join-Path $root "Tracker\*") "$installDir\Tracker" -Recurse -Force

Write-Host "4. 安装/更新 Windows 服务 ($serviceName) ..."
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Stop-Service $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}
$exePath = Join-Path $installDir "Service\MonitorAgent.Service.exe"
New-Service -Name $serviceName -BinaryPathName "`"$exePath`"" -DisplayName "终端行为监控代理" -StartupType Automatic | Out-Null
Start-Service $serviceName

Write-Host "5. 注册登录自启的后台小程序（计划任务）..."
$trackerExe = Join-Path $installDir "Tracker\MonitorAgent.Tracker.exe"
$taskName = "MonitorAgentTracker"
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue

$action = New-ScheduledTaskAction -Execute $trackerExe
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero)
$principal = New-ScheduledTaskPrincipal -GroupId "Users" -RunLevel Limited

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal | Out-Null

# 如果当前已有用户登录，立即启动一次 Tracker，不用等下次登录
if ($env:USERNAME -and (Get-Process -Name "explorer" -ErrorAction SilentlyContinue)) {
    Start-Process $trackerExe -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "安装完成。"
Write-Host "  服务端地址：$ServerUrl"
Write-Host "  主机名：$env:COMPUTERNAME"
Write-Host "几分钟内可在管理网页的「设置」页看到这台电脑按主机名自动出现，请到「总览」确认状态为在线。"
