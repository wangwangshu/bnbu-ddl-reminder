# Privacy policy

本程序保存作业 ID、标题、课程、截止时间、学校作业链接和本地提醒/完成记录。默认目录为 `%LOCALAPPDATA%\BNBU-DDL\data`。

学校账号在内置 WebView2 显示的学校登录页面输入。本程序没有读取密码字段的代码，不自动填写密码，并禁用 WebView2 的密码自动保存。WebView2 自己管理登录会话和浏览器配置，位于 `portal-profile`。程序只在 HTTPS 的 `ispace.bnbu.edu.cn/my/` Dashboard 运行作业采集脚本。

登录和浏览会按用户操作访问学校服务、学校配置的身份认证服务，以及页面引用的网络资源。Microsoft WebView2 和运行时安装程序可能根据微软的组件设置联系微软服务，包括安装、更新和浏览器服务；参见 [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement) 和 [WebView2 privacy considerations](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)。

本项目不提供自己的远程数据服务器或分析服务，不上传任务清单。用户主动选择的 JSON 文件和旧版扩展下载清单可导入本机。日志仅供本机故障诊断，用户不应公开含个人信息的日志。

卸载保留用户数据和登录会话。如要清除，请在程序退出后由本人删除 `%LOCALAPPDATA%\BNBU-DDL\data` 和 `portal-profile`。开源发布包不包含这两个目录。
