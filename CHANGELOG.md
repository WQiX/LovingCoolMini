# Changelog

## v1.1.0 — 2026-11-08

- 新增显卡温度读取：NVIDIA / AMD / Intel 核显（由 LibreHardwareMonitor 枚举）。
- 主界面和托盘可切换面板显示 CPU 或 GPU 温度。
- 多显卡时可在“显卡”下拉框选择；默认优先有 GPU Core 温度的独立显卡。
- GPU 温度不可用时显示明确说明，并暂停写入面板，不会写 0。
- 温度来源和显卡选择写入配置，重启后保留。
- 本机实测：RTX 2080 SUPER GPU Core、AMD Radeon(TM) Graphics 核显、Ryzen 7 7800X3D。
- 仅适用于 VID_F799 PID_4707（LOVINGCOOL AP1000 / AP1000 Pro）。

## v1.0.0 — 2026-11-08

- 首个版本，仅 CPU 温度。
