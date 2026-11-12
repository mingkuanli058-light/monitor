# 终端行为监控系统 —— 单台电脑卸载脚本，需以管理员身份运行。
$ErrorActionPreference = "SilentlyContinue"

$serviceName = "MonitorAgentService"
$taskName = "MonitorAgentTracker"
$installDir = "C:\Program Files\MonitorAgent"
$dataDir = "C:\ProgramData\MonitorAgent"

Write-Host "停止并删除服务 $serviceName ..."
Stop-Service $serviceName -Force
sc.exe delete $serviceName | Out-Null

Write-Host "删除登录计划任务 $taskName ..."
Get-Process -Name "MonitorAgent.Tracker" | Stop-Process -Force
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false

Write-Host "删除安装目录 $installDir ..."
Remove-Item $installDir -Recurse -Force

$keep = Read-Host "是否保留本机配置和待上报队列 ($dataDir)？(Y/N，默认 Y)"
if ($keep -eq "N" -or $keep -eq "n") {
    Remove-Item $dataDir -Recurse -Force
}

Write-Host "卸载完成。"
