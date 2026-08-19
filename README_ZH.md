<p align="center">
  <img src="assets/fly.png" width="340" alt="DesktopFly — a 3D fruit fly">
</p>

<h1 align="center">DesktopFly 🪰</h1>

<p align="center">
  <a href="README.md"><b>Русский</b></a> |
  <a href="README_EN.md"><b>English</b></a> |
  <a href="README_ZH.md"><b>简体中文</b></a>
</p>

<p align="center">
<b>基于真实果蝇（Drosophila melanogaster）全脑连接组驱动的桌面 3D 仿真程序（Windows 10/11）</b><br>
果蝇的行为由基于 <a href="https://codex.flywire.ai">FlyWire v783</a> 真实全脑连接组构建的 1 kHz 脉冲神经网络（LIF）实时驱动。
</p>

<p align="center">
  <i>本项目基于原始项目：<a href="https://github.com/DenisSergeevitch/desktop-fly">DenisSergeevitch/desktop-fly</a></i>
</p>

<p align="center">
  <img src="assets/brain.png" width="560" alt="Live brain window: 23,210 real neuron positions, spikes flashing">
</p>

<p align="center"><sub>
果蝇大脑视窗：展示来自 FlyWire v783 数据集的 23,210 个真实神经元胞体位置，并实时渲染神经元脉冲（Spikes）。两个明亮的黄色发光点为巨纤维神经元（Giant Fiber，起飞逃逸指挥神经元）。点击任意脑区即可进行光遗传学刺激！
</sub></p>

---

## 🌟 主要特性（C# / .NET 10 + OpenGL）

- **单上下文双视口渲染（1200×800 窗口）**：
  - **左侧视口 (840×800)**：程序化 3D 果蝇模型，具备三角步态行走动力学、翅膀扇动、梳理触角与复眼以及睡眠姿态。
  - **右侧视口 (360×800)**：交互式 3D 大脑连接组点云（23,210 个胞体），支持鼠标拖拽旋转 (Drag)、滚轮缩放 (Scroll) 与点击刺激 (Click)。
- **HUD 抬头显示与神经元信息卡片**：
  - **顶部 HUD**：紧凑半透明面板，显示所有快捷键与操作指南。
  - **底部悬浮卡片**：点击神经元或触发快捷键时，底部自动浮出信息卡，展示激活神经元数量、细胞类别及其生物学功能。
  - **3D 高亮光晕**：被激活的神经元集群将发出蓝金色脉冲光晕并持续 2.5 秒。
- **多蝇协同感知 (`A` / `R`)**：
  - 可动态增减果蝇数量。所有果蝇同步响应大脑连接组输出信号与光标威胁。
- **生动的头部梳理（Grooming）动画**：
  - 逼真的 4.5 Hz 前足交替梳理头部与复眼动作，并伴随头部微倾与触角摆动。

---

## 🧠 生物学连接组基础 (FlyWire Connectome)

- **23,210 个真实神经元胞体坐标**（源自 FlyWire v783 的 139,255 个神经元），按超类进行色彩分类渲染。
- **668 个神经元构成的核心脉冲环路与约 19,000 个突触连接（1 kHz LIF 仿真）**：
  - **LC4 (104) + LPLC2 (210)** — 视觉光流膨胀（Looming）威胁检测神经元。
  - **DNp01 / Giant Fiber (GF) (2)** — 紧急起飞逃逸下行指挥神经元。
  - **DNa01 + DNa02 (4)** — 转向控制下行神经元。
  - **DNp09 (2)** — 前进运动控制神经元。
  - **DNg11 (6)** — 头部梳理行为指挥神经元。
  - **MDN (4)** — 倒退行走（"Moonwalker" 太空步）指挥神经元。
  - **DNp02/DNp04/DNp11 (6)** — 飞行机动与翅膀下压控制神经元。
  - **330 个环路关联神经元**，包含上行本体感受器与气流触觉感受器。
- **非脚本化自然逃逸反射**：只有当光标靠近产生的光流膨胀克服约 1,200 个前馈抑制突触并在 Giant Fiber 中激发动作电位时，果蝇才会起飞（约 4 ms 反应潜伏期）。

---

## 🎮 控制与快捷键

| 按键 / 操作 | 功能 | 激活的神经回路 |
|---|---|---|
| `Space` / `E` | 紧急起飞逃逸 (Escape) | 巨纤维神经元 (Giant Fiber, DNp01) |
| `W` | 向前行走 | DNp09 (Forward Walking) |
| `G` | 梳理头部与复眼 (Grooming) | DNg11 (Cephalic Grooming) |
| `M` / `B` | 倒退行走 (Moonwalk) | MDN (Moonwalker) |
| `P` | 暂停 / 继续 | 冻结仿真 |
| `A` / `R` | 增加 / 减少果蝇 | 多果蝇群体控制 |
| `按住左键拖拽` | 3D 旋转大脑视窗 | 偏航 / 俯仰旋转 |
| `鼠标滚轮` | 缩放大脑视窗 | 视野距离调节 |
| `左键点击大脑` | 光遗传学神经刺激 | 刺激最近的神经元集群 |
| `左键点击场景` | 敲击表面（触觉惊吓） | 机械应力感受器通路 |

---

## 🚀 构建与运行

### 系统要求
* **Windows 10 / 11 x64**
* **.NET 10 SDK** (或直接运行预编译的 `DesktopFly.exe`)

```bat
# 1. 克隆代码仓库
git clone https://github.com/EvdokimovDU/desktop-fly.git
cd desktop-fly

# 2. 编译项目
build.cmd

# 3. 运行
run.cmd
```

或直接双击运行编译好的程序：
```bat
.\DesktopFly.exe
```

---

## 🧪 测试与验证

运行内置的生物学仿真与行为测试套件：

```sh
# 验证 1 kHz LIF 连接组动力学不变量
.\DesktopFly.exe --simtest

# 验证全部 17 种端到端行为测试场景
.\DesktopFly.exe --behaviortest

# 运行 xUnit 单元测试
dotnet test

# 生成离屏渲染预览图
.\DesktopFly.exe --snapshot fly_preview.png
.\DesktopFly.exe --brainshot brain_preview.png
```

---

## 📜 开源协议与学术引用

- 源代码遵循 **MIT License**。
- `data/` 目录中的连接组数据派生自 **FlyWire (FAFB v783)**，遵循 **CC BY-NC 4.0** 协议（详见 [data/DATA_LICENSE.md](data/DATA_LICENSE.md)）。
- 本项目基于原始代码库：[DenisSergeevitch/desktop-fly](https://github.com/DenisSergeevitch/desktop-fly)。

如在学术研究中使用本项目，请引用以下论文：
1. Dorkenwald, S. et al. *Neuronal wiring diagram of an adult brain.* Nature 634, 124–138 (2024). https://doi.org/10.1038/s41586-024-07558-y
2. Schlegel, P. et al. *Whole-brain annotation and multi-connectome cell typing of Drosophila.* Nature 634, 139–152 (2024). https://doi.org/10.1038/s41586-024-07686-5
