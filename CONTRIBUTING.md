# 贡献指南

欢迎通过 Issue 提交可复现问题，或通过 Pull Request 改进代码和中文文档。先搜索已有问题；不要提交个人配置、API Key、原始屏幕隐私、日志中的身份或本机绝对路径。

## 开发环境

Windows x64、PowerShell、系统 .NET Framework 编译器；首次运行 `tools/prepare.ps1` 下载并校验 OCR 资源。修改前阅读 README 和 docs/开发说明.md。

## 检查

```powershell
.\tools\prepare.ps1
.\src\build.ps1 -Tests
& .\src\验证.exe
if ($LASTEXITCODE -ne 0) { throw '检查失败' }
.\src\build.ps1
.\tools\privacy-check.ps1
```

默认测试使用隔离临时设置目录、本机模拟 API，临时创建自己的桌面测试 PNG 并删除，尝试恢复剪贴板。请避免在检查期间使用剪贴板。真实 API 检查必须自行配置，不将 Key 放进代码或 CI。

提交需说明具体问题、修改后的行为、实际验证和仍然存在的限制。交互变化附上脱敏或虚构样例；不要声称未执行的测试已经通过。第三方资源变更需补来源、许可、校验及获取步骤。普通 PR 不接触发布令牌。
