# BNBU DDL Reminder

Windows 10/11 x64 的本地作业截止提醒工具。内置 Microsoft WebView2 打开 BNBU iSpace，使用者在学校页面自行登录，程序读取 Dashboard Timeline 并提前 24 小时提醒。关掉主窗口后可在托盘继续运行。

独立版 3.0.2 / 商店候选版 3.1.1 修复文字和按钮裁切：启用 .NET Framework 4.8 的 PerMonitorV2 缩放，主界面按内容布局，长作业标题/课程换行，卡片按实测文字高度扩展。检查覆盖两种窗口大小与 100%/125%/150%/200% 的文字放大压力场景；真实 Windows 显示缩放和跨屏切换仍需设备复核。

项目主页：https://github.com/wangwangshu/bnbu-ddl-reminder 。维护者：[wangwangshu](https://github.com/wangwangshu)。

**发布状态：SignPath Foundation 免费签名申请因公开信誉记录不足未获批准，程序尚未获得正式代码签名。** 未签名测试包可能被 Windows 智能应用控制拦截。不要为了运行测试包关闭系统保护。

Microsoft Store 版 **3.1.1.0 已于 2026-11-19 正式提交，后台显示“正在认证”**，已设置审核通过后自动发布；**尚未通过商店审核或获得微软签名，目前不能视为已上架**。提交状态和构建方法见 [STORE_RELEASE.md](STORE_RELEASE.md)。

这是个人制作的工具，非学校官方客户端。学校登录和网络访问权限由学校决定。页面结构变化可能影响采集；请以学校实际截止时间为准。

## 功能

- 程序内登录、同步，Timeline 最多自动展开 50 次。
- 本地保存作业，提前一天提醒，支持稍后提醒、标记完成。
- Edge 关闭后继续提醒；电脑需开机且登录 Windows。
- 导入 JSON 和手动添加作业。
- 当前用户安装、桌面快捷方式、可选开机启动和卸载入口。
- 不附带任何使用者的作业或登录资料。

以上安装器功能用于独立 EXE 版。商店版通过 MSIX 安装/卸载，自动启动需在 Windows 启动设置中由使用者开启，不扫描 Downloads，也不自动导入旧版数据。

## Download

[未签名审核候选包与构建记录](https://github.com/wangwangshu/bnbu-ddl-reminder/actions/runs/44024428000)：打开页面底部的 `unsigned-review-candidate` 附件，下载并解压后可找到安装包。GitHub 附件下载需要登录，且有保留期限；此附件预计保留至 2027-02-15。

此构建**尚未签名，尚未获 SignPath Foundation 批准**。签名政策见下方 Code signing policy；后续只有实际通过签名验证的版本才会标为正式签名版。

## 从源码构建

在 Windows PowerShell 或 PowerShell 7 中运行：

```powershell
./RestoreDependencies.ps1
./Build.ps1
```

构建使用 Windows 的 .NET Framework 编译器。WebView2 SDK 版本及 SHA256 固定；运行时 bootstrapper 从微软下载并验证微软数字签名，构建时记录其 SHA256。正式审核前应与签名服务确认该系统组件的分发方式。

构建结果是 `dist/BNBU-DDL-Setup.exe`，**默认是未签名候选包**。仓库不含证书、签名密钥或用户资料。MIT 许可适用于本项目源码；微软组件适用其自己的许可证和 ThirdPartyNotices。

## 检查

`BNBU-DDL.exe --self-test <report>` 检查提醒逻辑。`--webview-test tests/portal-fixture.html <report>` 只读取模拟页面，并需设置隔离的 `BNBU_DDL_DATA_DIR`。安装器的 `--verify-package <report>` 检查解包完整性和无用户数据；`--install-test <report>` 需隔离的 `BNBU_INSTALL_TEST_ROOT`，测试快捷方式和临时注册表项。

已有本地检查包括：提醒边界、去重、稍后提醒、完成状态、UTF8 数据保存、WebView2 初始化、模拟 Timeline 采集和翻页、安装器和快捷方式。尚未记录正式签名后的 SAC 设备测试，也未记录另一位学生的首次学校登录测试。

## Code signing policy

见 [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md)。免费签名审核结果见 [SIGNPATH_APPLICATION.md](SIGNPATH_APPLICATION.md)。**本项目目前没有 SignPath Foundation 正式签名资格。**

## 隐私与维护

见 [PRIVACY.md](PRIVACY.md)、[SECURITY.md](SECURITY.md)。请勿在 issue、截图或日志里上传学校密码、完整作业数据或 portal-profile 文件夹。维护者须保持账号 MFA 开启；申请联系邮箱只提供给审核方。
