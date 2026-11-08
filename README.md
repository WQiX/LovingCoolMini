# LOVINGCOOL Mini v1.1.0

> **重要：仅适用于 LOVINGCOOL AP1000 / AP1000 Pro（AP Series）散热器。**
> USB 设备必须是 `VID_F799 & PID_4707`（`HID Nixie`）。其他 LOVINGCOOL 散热器、水冷、其他品牌型号的协议不同，本程序不保证可用，可能完全无法显示或显示错误数据。

一个只做一件事的小工具：读取 CPU 或显卡温度，通过 USB HID 让 LOVINGCOOL AP1000 Pro 盖板数显面板显示整数温度。

不依赖原版 LOVINGCOOL 软件，不使用 WebView2，不包含 ffmpeg、主题、灯效、屏幕商店等功能。

## 下载

- [v1.1.0（最新，CPU/GPU 切换）](https://github.com/WQiX/LovingCoolMini/releases/download/v1.1.0/LovingCoolMini-v1.1.0-win-x64.zip)
- [v1.0.0（仅 CPU 温度）](https://github.com/WQiX/LovingCoolMini/releases/download/v1.0.0/LovingCoolMini-v1.0.0-win-x64.zip)

## 适用设备

- LOVINGCOOL AP Series（AP1000 / AP1000 Pro）
- USB HID：`VID_F799 & PID_4707`，产品名 `HID Nixie`
- 已在以下环境实测通过：
  - AMD Ryzen 7 7800X3D
  - NVIDIA GeForce RTX 2080 SUPER
  - AMD Radeon(TM) Graphics 核显
  - LOVINGCOOL AP1000 Pro
  - Windows 11 x64

其他 PID 的 LOVINGCOOL 设备（例如 `0x4710`、`0x470A`、`0x4712`）协议不同，本程序目前只处理 `0x4707`。

## 显卡支持

程序通过 LibreHardwareMonitor 统一枚举显卡，支持：

- NVIDIA 显卡（NVAPI）：RTX 2080 SUPER 及其他 NVIDIA 型号
- AMD 独立显卡和 APU 核显（ADL）
- Intel 核显（如果驱动提供温度传感器）

程序会列出检测到的所有 GPU。默认优先选择有 `GPU Core` 温度的独立显卡；如果有多个 GPU，可以在界面的“显卡”下拉框中切换。

如果某块显卡被识别到、但驱动没有提供温度传感器，界面会显示：

> 已检测到 XXX，但驱动没有提供温度传感器。请更新显卡驱动，或切换回 CPU 温度。

此时程序**不会向面板写 0**，面板会保留最后一次有效温度，直到切换回可用来源。

## 系统要求

- Windows 10 / 11 x64
- .NET Framework 4.8（Windows 10/11 自带）
- 管理员权限（读取 CPU 温度需要加载 LibreHardwareMonitor 的内核驱动）
- 原版 LOVINGCOOL 软件不需要运行

## 安装与启动

1. 把整个文件夹解压到一个**固定位置**，例如：
   `C:\Users\你的用户名\Apps\LovingCoolMini`
2. 双击 `Start LovingCoolMini.cmd`，或者右键 `LovingCoolMini.exe` → **以管理员身份运行**。
3. UAC 弹窗点“是”。
4. 首次运行会自动创建开机自启计划任务。

程序界面会显示：

- 大号温度（保留一位小数）
- 当前温度来源：CPU 或 GPU
- GPU 模式下显示显卡下拉框
- 当前使用的传感器名称
- 面板连接状态
- 面板当前显示的整数温度

## 使用说明

- **切换 CPU / GPU**：主界面右上角“显示”下拉框选择 `CPU` 或 `GPU`；也可以右键托盘图标选择“显示 CPU 温度”或“显示 GPU 温度”。
- **选择显卡**：选择 GPU 后，界面会出现“显卡”下拉框，列出所有检测到的 GPU。
- **关闭窗口**：程序不会退出，会隐藏到系统托盘，继续刷新面板。
- **托盘图标**：
  - 双击：显示主界面
  - 右键：显示主界面、显示 CPU/GPU 温度、选择显卡、开机自启开关、打开日志、退出
- **退出程序**：右键托盘图标 → **退出**。
- **开机自启**：默认开启。首次运行会创建名为 `LovingCoolMini` 的计划任务，登录 Windows 后以最高权限启动，不弹 UAC。
- **移动程序位置后**：先在托盘里关闭“开机自启”，移动文件夹，再重新打开“开机自启”，让计划任务指向新路径。

> 面板本身只显示数字，不能显示“CPU”或“GPU”字样；请在主界面/托盘提示中确认当前来源。

## 温度与显示逻辑

- CPU：LibreHardwareMonitor，优先 `Core (Tctl/Tdie)`，其次 CPU Package，最后取最高温度。
- GPU：优先 `GPU Core`，其次 `GPU Temperature`、`GPU Diode`、`GPU Edge`；核显只有 `GPU VR SoC` 时也会使用，并在界面显示传感器名。
- 主界面：显示一位小数。
- 数显面板：显示四舍五入后的整数 °C，约每秒刷新；温度不变时每 5 秒发送一次心跳。
- 选中的温度来源不可用时，程序暂停写面板并显示原因，不会写 0。
- 如果面板被拔掉/占用，程序会自动重连。

## 文件与配置

- 程序目录：
  - `LovingCoolMini.exe`
  - `LibreHardwareMonitorLib.dll`
  - `LovingCoolMini.exe.config`
- 配置：`%APPDATA%\LovingCoolMini\config.json`
  - `TemperatureSource`：`CPU` 或 `GPU`
  - `GpuId`：上次选择的显卡 ID
- 日志：`%APPDATA%\LovingCoolMini\logs\app.log`
- 日志超过 1 MB 会自动轮转，最多保留 3 份历史日志。

## 常见问题

### 面板不刷新，或显示的是原软件的温度

请确认原版 LOVINGCOOL 软件已经**彻底退出**（托盘右键 → 退出），不要只关闭窗口。原软件会每秒覆盖面板数据。

### 提示“GPU 温度不可用”

- 确认显卡驱动已安装，并且 GPU-Z/HWiNFO 等工具能看到 GPU 温度。
- 某些核显或旧驱动不暴露温度传感器，此时只能切换回 CPU。
- 如果有多个 GPU，确认下拉框里选中的是有温度的那一块。
- 查看日志中 `GPU:` 开头的行，会列出检测到的显卡和传感器。

### 提示“CPU 温度不可用”

- 确认程序是以管理员身份运行。
- 确认系统是 Windows 10/11 x64，且 .NET Framework 4.8 可用。
- 某些安全软件会拦截 LibreHardwareMonitor 的内核驱动，请允许或加白名单。
- 查看日志文件中的具体错误。

### 开机自启没有生效

- 打开“任务计划程序”，查看根目录下是否有 `LovingCoolMini` 任务，状态是否为“就绪/已启用”。
- 如果移动过程序目录，请重新开关一次托盘里的“开机自启”。
- 某些系统优化工具会禁用计划任务，请检查。

### 杀毒软件/Windows 提示未知发布者

程序没有购买代码签名证书，所以 Windows 可能显示“未知发布者”。这是正常的；可以查看源码和日志。LibreHardwareMonitor 需要访问底层硬件，部分安全软件会提示风险。

## 卸载

1. 右键托盘图标 → 关闭“开机自启”。
2. 右键托盘图标 → **退出**。
3. 删除程序文件夹。
4. 可选：删除 `%APPDATA%\LovingCoolMini`（配置和日志）。

程序不安装常驻服务、不写注册表自启项；退出后不会留下常驻进程。LibreHardwareMonitor 的内核驱动只在运行时按需加载。

## 体积与内存

- 程序目录约 **800 KB**（不含 Windows 自带的 .NET Framework）。
- 托盘后台工作集实测约 **13–20 MB**（经过工作集整理；首次启动瞬间会高一些）。
- 私有提交内存约 **30–90 MB**，取决于是否启用 GPU 传感器、主窗口是否打开过；GPU 模式会比纯 CPU 模式高一些。这仍比原软件的数百 MB（主程序 + WebView2 多进程）低很多。

## 技术说明

- 数显协议：35 字节 HID 输出报告，前 7 字节为：
  `[0x01, 0x01, 温度, 使用率, 功耗, 风扇高字节, 风扇低字节]`
  其余补 0。本程序把当前选择的 CPU 或 GPU 温度写入“温度”字节；使用率/功耗/风扇字段填 0。
- 通过 Windows `WriteFile` 写 HID；`HidD_SetOutputReport` 在本设备上不会刷新显示。
- 温度读取使用 LibreHardwareMonitorLib 0.9.4（MPL-2.0）。
- 没有网络访问，没有遥测。

## 许可证与声明

- 本程序自身代码：MIT License，见 `LICENSE`。
- `LibreHardwareMonitorLib.dll`：MPL-2.0，见 `LibreHardwareMonitor-LICENSE.txt`。
- 本程序是个人使用的非官方工具，与 LOVINGCOOL/雷利库无隶属关系。
- 使用底层硬件访问和 HID 写入存在风险，请自行评估；作者不对硬件损坏或数据丢失负责。
