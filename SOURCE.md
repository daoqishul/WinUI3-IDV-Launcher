# IDV 启动器源码说明

这是 **IDV 启动器（WinUI 3 / Windows App SDK 1.8 / .NET 8）** 的完整源码。

安装包会内置原版 IDV-Login 的 Python 引擎到 `engine\IDV-Login`；源码压缩包不包含该引擎本体，构建安装包时需要另行放置 `engine-payload\IDV-Login`。

## 构建

需要：

- Windows 10 1809+ / Windows 11
- .NET 8 SDK
- Windows App SDK 1.8（NuGet 自动还原）
- 管理员权限（运行 Release 版时）

```powershell
dotnet build .\winui\IdvLauncher.csproj -c Release -p:Platform=x64
```

或者直接生成发布目录：

```powershell
.\build.ps1 -Package
```

输出在 `dist\`，发布包会生成到仓库根目录。

## 目录

```text
winui\
  App.xaml / App.xaml.cs          应用入口、主题与全局资源
  MainWindow.xaml / .xaml.cs     主窗口、导航、托盘、关闭清理
  Pages\                          首页、账号、登录引擎、解帧、云存档、设置
  Services\                       引擎、配置、API、云同步、托盘等
  Dialogs\                        改名/确认等原生对话框
  Models\                         账号、配置、行模型
  Assets\                         封面、应用图标、赞助码
  Tools\解帧.exe                  解帧功能使用的第三方工具
data\announcements.json           本地公告回退数据
packaging\README.txt              发布包内说明
shots\winui\                      界面截图
tools\                            开发辅助脚本和演示数据
```

## 运行依赖

- 原版 **IDV-Login** 引擎（默认 `D:\ProgramData\IDV-Login`），包含 `python-embed\python.exe` 和 `src\main.pyc`。
- 启动器会自动检测并隐藏启动引擎，不再显示引擎目录/端口配置。
- 游戏路径在「设置」中配置。

## 说明

- 本源码中的账号数据、`settings.json`、引擎日志等均位于 `%LOCALAPPDATA%\IDVLauncher` 或 `C:\ProgramData\idv-login`，不包含在源码包内。
- `winui\Tools\解帧.exe` 为解帧功能使用的第三方可执行文件；如不需要解帧功能，可从 `IdvLauncher.csproj` 和 `Pages\InjectionPage` 中移除，但会失去解帧页。
- 本项目为非官方工具，与网易 / 第五人格官方无关。使用风险自负。
