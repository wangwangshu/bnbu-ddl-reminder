# Security and release status

目前没有正式签名证书或获批的免费签名服务。任何源码构建都必须被标记为 unsigned candidate，不能宣称已经获得 Windows 信任。

安全问题由仓库维护者审核。公开仓库建立后，需要由仓库所有者启用 GitHub 的 Private vulnerability reporting，并在本文件补充实际报告入口。该入口尚未配置，不要在公开 issue 中发布学校登录信息或可利用的个人数据。

项目不自动更改 Smart App Control、Defender 或安全软件设置，不提供关闭保护或规避拦截的功能。正式发布前需检查安装器、主程序、卸载器和所有依赖的签名；本项目卸载器是安装器的副本，签名随字节保留。

代码签名只是发布者与文件完整性验证，不代表绝对安全，也不能保证免除所有 SmartScreen 或企业安全策略提示。
