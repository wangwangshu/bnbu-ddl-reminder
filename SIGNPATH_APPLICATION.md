# 免费签名申请准备单

状态（2026-11-17）：**公开源码与 GitHub 构建已完成，申请表草稿已填写；GitHub MFA 尚待完成确认。尚未提交，尚未获批。**

申请入口：https://signpath.org/apply
资格条款：https://signpath.org/terms

## 项目事实

- 项目名称：BNBU DDL Reminder。
- 项目类型：Windows x64 桌面作业提醒工具，非学校官方客户端。
- 功能：用户在学校门户自行登录；只读取 Dashboard 作业清单，在本机保存并提前 24 小时提醒。
- 源码许可：MIT；微软 WebView2 组件保留上游许可及签名。
- 项目主页和公开仓库：https://github.com/wangwangshu/bnbu-ddl-reminder 。
- 维护者、审核者、批准者：GitHub 用户 wangwangshu；目前是单人维护项目。
- 联系邮箱：**由申请人本人提供并确认**。
- 公开发布状态：源码已公开；未签名审核候选包已由 GitHub Actions 构建并作为运行附件保存。尚无正式签名 release。
- 可核查构建：https://github.com/wangwangshu/bnbu-ddl-reminder/actions/runs/44024428000 （结果：成功；源码提交 `ed64bb95494c92a574db5c9f1495422422a8f284`）。
- 构建附件：`unsigned-review-candidate`，SHA-256 `6131f64dfef0d0675799fa73b38c3368577b9c7fd9c2fa7b0f06a1aedfd02a51`；运行附件有保留期限，不能代替永久 release。
- 已有验证：本地模拟页面采集/翻页、提醒逻辑、安装器解包、隔离安装引擎、快捷方式、图标。
- 限制：学校首次登录、不同物理电脑、正式签名后的 SAC 测试仍待记录。

## 必须如实披露的资格问题

这是刚建立的新项目，尚无可验证的公开使用和维护记录。SignPath 的条款对可执行应用要求可验证的项目信誉，免费服务不是自动批准。不能虚构下载量、用户数、维护团队或已有签名。

WebView2 SDK 使用随 SDK 提供的开源许可；Evergreen runtime bootstrapper 是微软签名的系统组件安装程序，审核时应明确询问其是否符合 System Libraries 例外，必要时调整运行时分发方式。不能声称这一点已获 SignPath 认可。

## 接下来需要实际完成

1. 维护者亲自在自己的 GitHub 账号启用 MFA；此账号同时承担维护、审核与签名批准职责。
2. 在申请中如实披露新项目现状，并向审核方确认 WebView2 运行时安装组件的资格；联系邮箱只用于申请，不在仓库公开。
3. 维护者核对申请草稿并完成最终提交。不要在聊天中发送密码、验证码、私钥或证件照片。
4. 等服务方审核结果。批准后再配置真实 organization/project/artifact/signing policy 和 API token，并开启 SignPath MFA。

源码准备完成不等于申请已提交，申请提交不等于审核通过；最终签名文件必须验证后再发布。
