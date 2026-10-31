# 额度挂件：紧凑横条版

界面为 600 × 68 的横向长条，分四栏显示 Codex 5小时、Codex 每周、SuperGrok、Grok Bot 的剩余百分比、重置时间与进度条。无标题、底部说明或积分显示。重置时间直接显示在每栏第二行，格式为 MM-dd HH:mm；悬停可查看详情；过期数据为橙色并带 *。支持拖动、右键菜单和原有自启动。

运行 QuotaWidget.exe。右键 → 登录 Grok / 管理授权，在官方页面自行登录，再点击“打开用量页”。识别到用量后会提示“已连接”，点击“完成并连接”后继续后台刷新。

- Codex 自动读取本机 ChatGPT 登录，查询短周期/每周剩余额度及额外积分。
- SuperGrok 和 Grok Bot 均读取 grok.com/?_s=usage 中的两项周额度。桌面大数字是剩余百分比，详情列出已用比例、网页重置时间和可读的额外使用积分。
- 每 60 秒刷新网页，挂件每 5 秒检查读取结果。需要网络，网页结构变化可能需要更新解析器。
- 登录窗口使用微软 WebView2；独立会话保存在 %LOCALAPPDATA%/QuotaWidget/GrokWebProfile。无需浏览器扩展，无需复制 Cookie，不读取已有 Chrome 配置。
- 这是本地持久化网页登录会话，不是官方 OAuth 授权。网站正常续期由浏览器处理，不保证永久登录。到期、风控、验证码需本人重新登录。
- 超过三分钟的旧数据会明确标记过期。已启用同步时发生持续错误会提示一次；恢复后重置提醒。右键可再次打开登录窗口，点击“取消授权”会清除挂件专用浏览器的会话。
- 挂件使用当前用户注册表 Run 项 QuotaWidget 在 Windows 登录后启动；已有连接时会启动专用浏览器后台同步。取消右键的自启动并退出即可停用。

## 验证范围

Codex 在线读取已通过；WebView2 登录窗口启动已通过。Grok 解析器通过中文截图转录、英文、0%、100%、缺失值等七项测试；额度模型通过剩余转换、过期与注销测试。已使用本机真实登录会话验证两项周额度、重置时间及额外积分。已修复 number-flow-react 动态数字组件的 aria-label 读取，并验证后台隐藏读取。完成按钮会验证两项额度后再隐藏；失败会保持窗口打开。

## 文件

QuotaWidget.cs、GrokSession.cs、grok-parser.js 是源码，build.ps1 可重编译。Microsoft.Web.WebView2.*.dll 和 WebView2Loader.dll 为微软 SDK 1.0.4258.31，运行时使用本机 Edge WebView2。

原版本的 Cursor Grok Bot 查询和手动 Token 设置已经停用。原版本手动保存的 grok.bin/grokbot.bin 不再读取。

来源：Codex 非官方查询实现参考 https://github.com/steipete/CodexBar/blob/main/docs/codex.md ；WebView2 文档 https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/winforms 。Grok 使用用户官方用量页面已显示的数据。
