# Stealth

A tool to change window's opacity attribute on Windows Vista and above.\
This is a tiny personal project for .Net coding practice.

[![Github All Releases](https://img.shields.io/github/downloads/celeron533/Stealth/total.svg)]()

## Features
- Set target window transparency
- Set target window as pin-on-top
- List all visible windows
- Filter: show modified windows only
- Filter: hide removed windows
- Filter: on window title

## Screenshots (v0.6.2)
Thanks to [Symin](https://github.com/syminomega) provides new icon, UI sytle and screenshots.

![Stealth](screenshot/Stealth.png)

![Sample with explorer](screenshot/Sample.png)

## License
[GNU GENERAL PUBLIC LICENSE (GPL)](./LICENSE)\
(Any further development based on this code should be open-sourced)
## Languages / 语言

Supports English and Simplified Chinese. The initial language follows Windows; switch later using **Language → English / 简体中文**. The choice is saved per user.

支持英文与简体中文。首次启动自动识别 Windows 语言，也可以在「语言」菜单中手动切换并保存偏好。

## Official fork releases

Version **0.8.0** adds Simplified Chinese (automatic locale detection and a manual language selector), fixes snapshot process-handle cleanup, and restores the opacity slider after a failed Win32 change.

Download this fork's official builds at [Releases](https://github.com/snownico0722/Stealth/releases). Release builds target Windows and require .NET Framework 4.7.2.

本分支的 0.8.0 版增加简体中文、语言切换和窗口处理修复。请从本仓库 Releases 下载正式编译版。

## Single EXE / 单文件版 v0.9.0

[Download Stealth.exe](https://github.com/snownico0722/Stealth/releases/latest/download/Stealth.exe)

The app and its managed dependencies are combined in one executable via Costura.Fody. No companion DLLs are needed, but Windows must have **.NET Framework 4.7.2 or newer** installed.

程序的托管依赖已嵌入单个 EXE 中，下载即可运行，无需放置额外 DLL。电脑仍需安装 .NET Framework 4.7.2 或以上版本。
