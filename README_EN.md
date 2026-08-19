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
<b>An interactive 3D simulation of a fruit fly (Drosophila melanogaster) for Windows 10/11</b><br>
The fly's behavior is driven in real-time by a spiking neural network (LIF) based on the real <a href="https://codex.flywire.ai">FlyWire v783</a> whole-brain connectome.
</p>

<p align="center">
  <i>Based on the original project: <a href="https://github.com/DenisSergeevitch/desktop-fly">DenisSergeevitch/desktop-fly</a></i>
</p>

<p align="center">
  <img src="assets/brain.png" width="560" alt="Live brain window: 23,210 real neuron positions, spikes flashing">
</p>

<p align="center"><sub>
The fly's brain window: 23,210 real neuron soma positions from FlyWire v783 with real-time flashing spikes. The two prominent yellow markers are the Giant Fibers (escape command neurons). Click anywhere to optogenetically stimulate neurons!
</sub></p>

---

## 🌟 Key Features (C# / .NET 10 + OpenGL)

- **Dual Viewports in a Single Context (1200×800 Window)**:
  - **Left Viewport (840×800)**: Procedural 3D fly with realistic tripod locomotion kinematics, wingbeats, grooming, and sleep postures.
  - **Right Viewport (360×800)**: Interactive 3D connectome (23,210 neuron somas) with free mouse rotation (Drag), zoom (Scroll), and optogenetic stimulation (Click).
- **HUD Overlay and Real-Time Information Cards**:
  - **Top HUD**: Compact overlay displaying all hotkeys and commands.
  - **Bottom Info Toast**: Clicking on neurons or pressing shortcuts pops up an information card detailing the stimulated cell group, neuron count, and biological function.
  - **3D Highlighting**: Stimulated neuron clusters pulse with a vibrant cyan-gold glow for 2.5 seconds.
- **Multi-Fly Flock Synchronization (`A` / `R`)**:
  - Add or remove flies at runtime. All flies synchronously process connectome motor signals and sensory threats.
- **Expressive Cephalic Grooming Animation**:
  - Anatomically accurate 4.5 Hz rhythmic sweeping of front legs across the eyes and antennae, accompanied by subtle head bobs.

---

## 🧠 Biological Connectome Foundation (FlyWire)

- **23,210 neuron soma coordinates** (out of 139,255 in FlyWire v783) rendered as a point cloud, color-coded by super-class.
- **668-neuron active spiking circuit with ~19,000 synaptic connections (1 kHz LIF simulation)**:
  - **LC4 (104) + LPLC2 (210)** — looming visual threat detectors.
  - **DNp01 / Giant Fiber (GF) (2)** — emergency takeoff command neurons.
  - **DNa01 + DNa02 (4)** — descending steering neurons.
  - **DNp09 (2)** — forward walking command neurons.
  - **DNg11 (6)** — cephalic grooming command neurons.
  - **MDN (4)** — backward walking ("moonwalker") command neurons.
  - **DNp02/DNp04/DNp11 (6)** — flight maneuver and wing-beat control neurons.
  - **330 circuit partners**, including ascending proprioceptors and sensory wind mechanoreceptors.
- **Non-Scripted Escape Reflex**: Takeoff is triggered only when optical looming overcomes ~1,200 feedforward inhibitory synapses and fires an action potential in the Giant Fiber (~4 ms latency).

---

## 🎮 Controls & Shortcuts

| Key / Action | Action | Activated Neurons |
|---|---|---|
| `Space` / `E` | Escape takeoff flight | Giant Fiber (DNp01) |
| `W` | Walk forward | DNp09 (Forward Walking) |
| `G` | Head & eye grooming | DNg11 (Cephalic Grooming) |
| `M` / `B` | Backward walk (Moonwalk) | MDN (Moonwalker) |
| `P` | Pause / Resume | Freeze simulation |
| `A` / `R` | Add / Remove fly | Multi-fly population |
| `Hold LMB + Drag` | Rotate 3D brain | Yaw / Pitch rotation |
| `Mouse Scroll` | Zoom brain viewport | Scale view |
| `Click on brain` | Optogenetic stimulation | Nearest neuron cluster |
| `Click on fly scene` | Substrate tap (startle) | Sensory mechanoreceptors |

---

## 🚀 Build & Run

### Requirements
* **Windows 10 / 11 x64**
* **.NET 10 SDK** (or run precompiled `DesktopFly.exe`)

```bat
# 1. Clone repository
git clone https://github.com/EvdokimovDU/desktop-fly.git
cd desktop-fly

# 2. Build single-file executable
build.cmd

# 3. Launch
run.cmd
```

Or run directly:
```bat
.\DesktopFly.exe
```

---

## 🧪 Testing & Verification

Run built-in diagnostic and behavioral validation suites:

```sh
# Verify 1 kHz LIF connectome invariants
.\DesktopFly.exe --simtest

# Run 17 end-to-end behavioral test scenarios
.\DesktopFly.exe --behaviortest

# Run xUnit test suite
dotnet test

# Offscreen rendering test
.\DesktopFly.exe --snapshot fly_preview.png
.\DesktopFly.exe --brainshot brain_preview.png
```

---

## 📜 License & Citations

- Source code is licensed under the **MIT License**.
- Connectome data in `data/` is derived from **FlyWire (FAFB v783)** and licensed under **CC BY-NC 4.0** (see [data/DATA_LICENSE.md](data/DATA_LICENSE.md)).
- Based on the original repository: [DenisSergeevitch/desktop-fly](https://github.com/DenisSergeevitch/desktop-fly).

If you use this software in academic research, please cite:
1. Dorkenwald, S. et al. *Neuronal wiring diagram of an adult brain.* Nature 634, 124–138 (2024). https://doi.org/10.1038/s41586-024-07558-y
2. Schlegel, P. et al. *Whole-brain annotation and multi-connectome cell typing of Drosophila.* Nature 634, 139–152 (2024). https://doi.org/10.1038/s41586-024-07686-5
