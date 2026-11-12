# 终端代理（客户端程序）

装在每台员工电脑上，负责记录开关机/睡眠唤醒、检测键鼠空闲、采集前台程序名，并上报给服务端。对应产品说明书第 3、5 章。

## 组成

- **MonitorAgent.Service**：Windows 服务，LocalSystem 权限运行，开机自启。负责开机/关机/睡眠/唤醒事件、每 5 分钟心跳。
- **MonitorAgent.Tracker**：随用户登录启动的无窗口小程序。每 30 秒采样一次键鼠空闲时间和前台窗口程序名，每 1 分钟汇总上报一次；检测锁屏/解锁/登出。
- **MonitorAgent.Shared**：两者共用的配置读写、HTTP 上报与离线缓存重试逻辑。

两者分离是因为 Windows 不允许 LocalSystem 服务直接读取当前登录用户的桌面操作状态（键鼠、前台窗口），必须由运行在用户会话里的 Tracker 来做。

## 一、编译

**前提**：开发机或公司服务器装有 .NET 8 SDK。

```powershell
cd "D:\work\01_进行中项目\Z终端行为监控系统\agent"
powershell -ExecutionPolicy Bypass -File publish.ps1
```

生成的 `agent\dist\` 目录里有 `Service\`、`Tracker\`、`install.ps1`、`uninstall.ps1`。把整个 `dist` 文件夹拷到 U 盘即可带去逐台安装。

两个 exe 都是**自包含单文件**（打包了 .NET 运行时，每个约 65~70MB），**目标电脑不需要安装任何 .NET 运行时或 SDK**，符合说明书"单个 exe，不需要另装运行环境"的要求。只有编译这台机器（公司服务器或你的开发机）需要装 .NET 8 SDK。

## 二、安装（每台电脑，需要管理员权限）

1. 把 U 盘里的 `dist` 文件夹拷到目标电脑。
2. 以管理员身份打开 PowerShell，进入该目录，执行：

   ```powershell
   powershell -ExecutionPolicy Bypass -File install.ps1 -ServerUrl "http://<服务器内网IP>:5208"
   ```

   如果服务端配置了 `Agent:ApiKey`，加上 `-ApiKey "你设置的key"`。

3. 脚本会自动：
   - 把程序复制到 `C:\Program Files\MonitorAgent`
   - 写入配置 `C:\ProgramData\MonitorAgent\agent.config.json`
   - 安装并启动 Windows 服务 `MonitorAgentService`
   - 注册一个"登录时启动"的计划任务来运行 Tracker（崩溃后 1 分钟自动重启，最多重试 999 次）

4. 装完几分钟内，去管理网页的"设置"页应该能看到这台电脑按主机名自动出现一行；"总览"页应显示在线。

5. 确认员工账户为普通账户（代理本身不要求管理员权限运行，但安装/卸载需要）。

## 三、本地快速测试（不装 Windows 服务，直接跑 exe 看效果）

如果只是想先看看代理是否正常工作，不想真的装成服务，可以直接双击或命令行运行发布出来的 exe（以当前用户身份跑，日志会打印在控制台窗口里）：

```powershell
cd "D:\work\01_进行中项目\Z终端行为监控系统\agent\dist\Service"
.\MonitorAgent.Service.exe
```

```powershell
cd "D:\work\01_进行中项目\Z终端行为监控系统\agent\dist\Tracker"
.\MonitorAgent.Tracker.exe
```

运行前记得先按上面"二、安装"第 2 步的方式手动写一份 `C:\ProgramData\MonitorAgent\agent.config.json`（内容示例见下），否则会用默认的 `http://localhost:5208`。

```json
{
  "ServerUrl": "http://192.168.1.10:5208",
  "ApiKey": "",
  "HostName": "我的测试电脑"
}
```

## 四、验证清单

- [ ] 安装/运行代理后 1~2 分钟内，管理网页"设置"页出现以主机名命名的新设备
- [ ] "总览"页该设备状态为"干活"或"空闲"（取决于你当时是否在操作电脑），不是"离线"
- [ ] 操作一会儿电脑（打字、切换窗口），5 分钟以上后看"电脑时间线"页，应该能看到刚才那段时间的"干活"分段和前台程序（如 notepad、chrome、WINWORD 等，注意是进程名不是窗口标题）
- [ ] 放着电脑不动超过"空闲判断时长"（默认 5 分钟），时间线应变成"空闲"
- [ ] 锁屏（Win+L）后解锁，时间线里应能看到对应分段，`EventLog` 里应有 Lock/Unlock 事件（可在数据库里查，或后续在管理页加事件明细查看功能）
- [ ] 断开网络几分钟再恢复，代理应能补报（检查 `C:\ProgramData\MonitorAgent\tracker-queue.json` / `service-queue.json`，断网时文件里会堆积未发送的数据，恢复网络并等待一个采样周期后文件应清空）
- [ ] 重启电脑后能看到 Boot 事件，关机前能看到 Shutdown 事件（受限于服务停止的时间窗口，关机事件偶尔可能来不及上报，这是第一期已知限制）

## 五、卸载

```powershell
cd "<dist目录>"
powershell -ExecutionPolicy Bypass -File uninstall.ps1
```

## 六、已知限制

- 关机事件依赖服务收到停止信号后的几秒钟内完成上报，极端情况下（强制断电、长按关机键）可能来不及上报，只能靠下次开机的心跳间隔推断大致下线时间。
- 活动/前台程序按"分钟"粒度上报（内部每 30 秒采样两次合并成一分钟），不是逐秒精确记录，与说明书"每 30 秒读取"在采样频率上一致，但上报粒度为分钟，足够支撑日/周/月统计，属于合理简化。
- 代理与服务端之间默认不做身份校验（局域网内测试阶段）；如需要，在服务端 `appsettings.json` 配置 `Agent:ApiKey`，并在安装时用 `-ApiKey` 参数传给每台代理。
- 暂无"服务端统一下发升级"的实现（说明书里的"升级"一节），目前升级仍需重新运行 install.ps1。
