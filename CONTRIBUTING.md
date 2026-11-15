# Contributing

## 环境

- Windows 10/11 x64
- .NET 8 SDK
- Visual Studio 2022 可选，命令行即可构建
- Inno Setup 7（仅构建安装包）

## 本地构建

```powershell
dotnet build .\winui\IdvLauncher.csproj -c Release -p:Platform=x64
```

## 提交前

- 确认构建通过，无新增警告
- 涉及 UI 的改动请实际运行验证
- 不要提交以下内容：
  - `dist/`
  - `installer/`
  - `engine-payload/`
  - `%LOCALAPPDATA%\IDVLauncher`
  - `C:\ProgramData\idv-login` 日志和账号数据
  - 个人截图中的账号信息

## 代码风格

- 使用现有 XAML 资源和控件样式
- C# 使用 nullable，保持现有命名习惯
- 页面卸载时取消事件订阅 / 停止计时器
- 不在 UI 线程执行磁盘扫描或进程枚举

## 提交 PR

请附上：

1. 改动说明
2. 复现或验证步骤
3. UI 截图（如适用）
