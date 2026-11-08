# AP1000 Pro HID 协议笔记

> 仅针对 `VID_F799 & PID_4707`（产品名 `HID Nixie`，LOVINGCOOL AP Series）。  
> 这些信息来自对原版 LOVINGCOOL 前端资源的分析和实机验证，用于互操作性。

## 设备

- HID Usage Page：`0xFF00`
- Usage：`0x0001`
- Output Report Byte Length：`35`
- Feature Report：无
- 序列号示例：`7897C9920164F3`

## 输出报告

通过 Windows `WriteFile` 写入 35 字节，不能使用 `HidD_SetOutputReport`（本设备上调用会返回成功但不会刷新显示）。

前 7 字节：

| 偏移 | 值 | 含义 |
|---|---|---|
| 0 | `0x01` | HID Report ID |
| 1 | `0x01` | AP Series 命令/页面 |
| 2 | 0–255 | 当前选择的 CPU 或 GPU 温度（整数 °C） |
| 3 | 0–255 | CPU 使用率（%） |
| 4 | 0–255 | CPU 功耗（W） |
| 5 | 高字节 | 风扇转速 16 位高字节 |
| 6 | 低字节 | 风扇转速 16 位低字节 |
| 7–34 | `0x00` | 补零到 35 字节 |

本程序只需要温度，因此偏移 3–6 填 0；实机验证面板仍正常显示温度。面板只显示数字，不区分 CPU/GPU；来源由程序界面标注。

## 原版前端逻辑

原版 `useHidDevice` 中按 PID 分发：

- `18183`（0x4707，AP Series）：
  `[1, 1, temperature, usage, power, fan >> 8, fan & 0xFF]`
- `18192`（0x4710，UK Series）：先发 3 次 `[1, 4, 4]`，再发 `[1, 1, temperature, fan >> 8, fan & 0xFF]`
- `18194`（0x4712，NIXIE-STAT）：`[1, 1, temperature, usage, power >> 8, power & 0xFF, fan >> 8, fan & 0xFF]`

本程序只实现 `18183`。

## 温度来源

LibreHardwareMonitorLib 0.9.4（net472），读取：

- CPU：`HardwareType.Cpu`，`SensorType.Temperature`，优先 `Core (Tctl/Tdie)`
- GPU：`HardwareType.GpuNvidia` / `GpuAmd` / `GpuIntel`，优先 `GPU Core`，其次 `GPU Temperature`、`GPU Diode`、`GPU Edge`；核显可用 `GPU VR SoC`
- 多 GPU 时由界面选择，默认优先有 `GPU Core` 温度的独立显卡
- GPU 温度不可用时暂停写面板，并在界面说明原因，不写 0

CPU 温度需要管理员权限；无管理员权限时 LHM 会打开但 CPU 温度恒为 0。GPU 温度在普通权限下通常也能读取。
