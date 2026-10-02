# Headroom 额度小组件

现有 Windows 原生程序继续支持 Claude Code 和 Codex；仓库内新增 Python/Tk 前端，适配 macOS/Linux 的窗口、字体和设置路径。**Windows 已完成本地可视化验证；[CI](https://github.com/iserlohn0522-cell/headroom-ai-usage-monitor/actions/runs/37033746827) 已在 Windows、macOS arm64 和 Linux 通过 16 项测试，包括真实 Tk 界面集成（Linux 使用 Xvfb）。仍未完成 macOS/Linux 人工桌面验证。**

右键可选三种皮肤：午夜（柔和深色）、纸张（浅色卡片）、终端（等宽字体、分段进度条）。默认简体中文，也可选择 English；旧日语设置迁移为中文，英语保留。

悬浮球的外圈表示 Claude 5h，内圈表示 Claude 每周额度，中央液体面积表示 Codex 每周剩余额度。仅在真实报告 Codex 5h 时显示额外细圈，不支持的窗口不显示虚假空条。悬停查看图例、状态和重置时间。各额度独立显示，不进行平均。

- `0% / 已用尽`：真实余额为零。
- `— / 无数据`：无可用数值，不是零。
- `不提供`：明确不支持，不画额度条。
- `更新中`、`读取失败`、`已过期`：分别显示；旧值添加 `~` 和灰色纹理。
- 重置时间来自提供方，不自行推算下一周期。

```shell
python -m portable.app
python examples/providers/mock_provider.py --out .local-demo --scenario weekly-only
python -m portable.app --providers .local-demo/manifest.json
```

第一条命令为明确标注的示例数据。便携版要求 Python 3.10+、Tk 8.6+，日常运行无第三方依赖。右键设置；双击或空格切换悬浮球；F5 刷新。

自定义提供方使用[版本化本地 JSON 接口](docs/providers/README.md)，包含窗口 ID、能力、状态、余额、观测时间及重置语义。Headroom 不执行提供方脚本，不索取其凭据。安全 mock 可演示耗尽、更新、过期和错误。可选 `--codex-cli` 仅通过现有已登录 CLI 读取额度；本次验证模拟协议，未调用真实账号。

[平台覆盖](portable/README.md) · [截图与验证](docs/previews/current/README.md) · [英文说明](README.md)
