# WinUI3 IDV Launcher

面向《第五人格》PC 端的 WinUI 3 单窗口启动器。

[![Build](https://github.com/daoqishul/WinUI3-IDV-Launcher/actions/workflows/build.yml/badge.svg)](https://github.com/daoqishul/WinUI3-IDV-Launcher/actions/workflows/build.yml)
[![License: GPL-3.0](https://img.shields.io/badge/License-GPL--3.0-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)

> 非官方工具，与网易 / 第五人格官方无关。使用前请阅读免责声明。

## 功能

- 渠道账号管理、搜索、收藏
- 原版 IDV-Login 引擎自动启动
- 收藏账号自动登录；可最小化内置登录窗口
- 原版云存档
- 游戏画质、显示模式、帧率、视野范围设置
- WinUI 3 原生键位编辑器
- 解帧工具集成
- 官方公告、托盘、主题、窗口状态记忆

## 界面

| 首页 | 账号管理 |
| --- | --- |
| ![首页](shots/winui/home-final.png) | ![账号管理](shots/winui/accounts-final.png) |

| 键位设置 | 云存档 |
| --- | --- |
| ![键位设置](shots/winui/key-final-layout.png) | ![云存档](shots/winui/cloud-final.png) |

## 系统要求

- Windows 10 1809+ 或 Windows 11，x64
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows App Runtime 1.8](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
- 管理员权限
- 原版 IDV-Login（完整登录 / 云存档功能需要；安装包可内置）
- 《第五人格》PC 客户端（启动游戏需要）

## 下载

前往 [Releases](https://github.com/daoqishul/WinUI3-IDV-Launcher/releases) 下载：

- `IDVLauncher-Setup-*.exe`：安装包
- `IDVLauncher-WinUI3-*-win64.zip`：便携版

## 详细使用

完整功能、数据路径和排错说明见 [docs/USAGE.md](docs/USAGE.md)。

## 从源码构建

需要 .NET 8 SDK：

```powershell
dotnet build .\winui\IdvLauncher.csproj -c Release -p:Platform=x64
```

或生成便携包：

```powershell
.\build.ps1 -Package
```

输出：

```text
dist\
IDVLauncher-WinUI3-v1.0.0-win64.zip
```

## 项目结构

```text
winui/                  WinUI 3 主程序
  Pages/                首页、账号、云存档、引擎、键位、解帧、设置
  Services/             引擎、API、云同步、游戏设置、键位、托盘
  Models/               数据模型
  Dialogs/              原生对话框
  Assets/               封面、图标、赞助码、画质预览
  Tools/解帧.exe        解帧工具
data/                   本地公告回退数据
packaging/              发布包说明、安装许可、简体中文语言文件
tools/                  开发辅助脚本
shots/winui/            界面截图
installer.iss           Inno Setup 安装脚本
build.ps1               构建脚本
```

## 数据目录

| 内容 | 路径 |
| --- | --- |
| 启动器设置 / 账号 | `%LOCALAPPDATA%\IDVLauncher` |
| 引擎配置 / 日志 | `C:\ProgramData\idv-login` |
| 原版引擎程序 | 安装包内置时位于 `{app}\engine\IDV-Login` |
| 游戏键位配置 | `<游戏目录>\Documents\pc_key_setting_data.json` |
| 游戏画质配置 | `<游戏目录>\Documents\setting_data.json` |

## 参与贡献

请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 免责声明

本软件为非官方第三方工具，仅用于个人账号管理与游戏启动辅助。使用本软件可能违反游戏用户协议，存在账号封禁、数据丢失等风险；因使用本软件产生的全部后果由使用者自行承担。

本项目不包含《第五人格》游戏本体，也不包含原版 IDV-Login 引擎的源代码。Release 安装包可能内置原版引擎二进制；其许可证见 [NOTICE.md](NOTICE.md)。

## License

[GNU General Public License v3.0](LICENSE)
