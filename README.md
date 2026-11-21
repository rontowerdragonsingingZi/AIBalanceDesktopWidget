# AI Balance Desktop Widget

Windows 桌面额度挂件。在屏幕边缘显示 Codex（ChatGPT）短周期与每周剩余额度，以及 SuperGrok、Grok Bot 的每周剩余额度。

A compact Windows desktop widget for Codex / ChatGPT usage quota and Grok (xAI) weekly balance. It stays above other windows, shows remaining percent and reset time, and keeps login data on this PC.

## 能看到什么

默认是一条 600 × 68 的横条，四栏从左到右为：

- Codex 短周期（通常是 5 小时，以接口返回的窗口为准）
- Codex 每周
- SuperGrok
- Grok Bot

每栏显示剩余百分比、重置时间（`MM-dd HH:mm`）和进度条。没有标题栏，也不在条上显示积分。悬停可看详情；读取失败但还有上次成功数据时，数字为橙色并带 `*`。Grok 数据超过三分钟会标为过期。

可拖动。位置保存在本机。右键或托盘图标可操作。

## 运行

需要 Windows、.NET Framework 4.5 或更高版本，以及本机 Edge WebView2 运行时（仅 Grok 登录窗口需要）。下载后直接运行 `QuotaWidget.exe`。同一用户只会有一个实例。

Codex 需要本机已经用 ChatGPT 账号登录过 Codex。程序读取 `CODEX_HOME`，未设置时读取 `%USERPROFILE%\.codex\auth.json`，再向 ChatGPT 用量接口查询剩余额度。这不是官方客户端，接口变化时可能读不到。

Grok 需要本人在程序打开的官方页面登录：

1. 右键 → **登录 Grok / 管理授权…**
2. 在 `grok.com` 完成登录，打开用量页
3. 识别到 SuperGrok 和 Grok Bot 后，点击 **完成并连接**

连接后登录窗口可以隐藏，专用浏览器会继续在后台同步。这是保存在本机的网页会话，不是 xAI 官方 OAuth，不保证永久有效。过期、验证码或风控时需要重新登录。

## 右键菜单

- **立即刷新**：马上重查当前仍显示的额度
- **关闭 Codex / 显示 Codex**：收起或恢复两栏 Codex。关闭后窗口变为 300 × 68，只留 SuperGrok 和 Grok Bot，并停止 Codex 额度请求。选择会记住，下次启动仍然有效
- **登录 Grok / 管理授权…**：打开或重新显示 Grok 登录窗口。窗口里可以取消授权并清除挂件专用浏览器会话
- **保持置顶**
- **开机自启动**：写入或删除当前用户注册表 Run 项 `QuotaWidget`
- **隐藏 / 显示**
- **退出**

取消自启动后，再退出，即可停用。

## 刷新

- Codex 显示时，约每 60 秒查询一次。关闭 Codex 后不再发起该查询
- Grok 用量页约每 60 秒刷新，挂件约每 5 秒读取一次结果
- 已启用 Grok 同步时，若持续读不到用量，托盘会提示一次；恢复后可再次提示

## 隐私

额度查询和登录会话都留在本机。

- Codex 令牌只从本机 Codex 登录文件读取，不写入本仓库
- Grok 会话在 `%LOCALAPPDATA%\QuotaWidget\GrokWebProfile`
- 不安装浏览器扩展，不复制 Cookie，不读取已有 Chrome 配置
- 不上传账号、令牌或用量到这个 GitHub 仓库

## 编译

源码是 `QuotaWidget.cs`、`GrokSession.cs` 和 `grok-parser.js`。在仓库目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

`Microsoft.Web.WebView2.*.dll` 与 `WebView2Loader.dll` 来自微软 WebView2 SDK 1.0.4258.31，运行时仍使用本机 Edge WebView2。

## 参考

Codex 用量读取的实现参考 [CodexBar 的 Codex 文档](https://github.com/steipete/CodexBar/blob/main/docs/codex.md)。WebView2 用法见[微软 WinForms 入门文档](https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winforms)。Grok 只读取用户自己在官方用量页上已能看到的数据。
