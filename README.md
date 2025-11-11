# CPU Set Setter

<p align=center>
    <img height="75" src="Images/Logo.png">
</p>

Make your games and apps run on the right CPU cores &mdash; for smoother performance on AMD Dual-CCD and Intel Hybrid processors.

**Requirements:**
- **Windows 11**
- **.NET Desktop Runtime 9** (Follow in-app instructions)

# What it does
Windows tries its best to schedule tasks automatically, but it may often not be optimal. CPU Set Setter gives you control: you decide which cores your games and apps can use. This tool brings quick and easy access to **CPU Sets** &mdash; almost the same as Affinity, but better &mdash; for free.

### Experimentation is key
To quickly find out which core configuration works best for a certain game, CPU Set Setter provides Hotkey support to change/clear CPU Sets on the fly, so you can experiment quickly without having to leave your game.

## AMD Dual-CCD CPUs
Dual CCD CPUs like the 3900X, 3950X, 5900X, 5950X, 7900X, 7950X, 9900X, 9950X and their -X3D variants can achieve better gaming performance when a game only runs on a single CCD. Dual-CCD X3D CPUs especially:
- **Cache cores (CCD0)**: More cache, great for gaming.
- **Frequency cores (CCD1)**: Higher clock speeds, better for productivity and background tasks.

By default, Windows and AMD's driver try to achieve better gaming performance by **turning off** the CCD1/Frequency cores (called parking) - but this can sometimes hurt performance, especially when gaming and multitasking simultaneously. Because a parked core can't be used by background processes either.

## Intel Hybrid CPUs (12th-15th Gen)
Intel Hybrid processors like the 12900K, 13900K, 14900K, and Core Ultra 285K have two types of cores:
- **Performance cores (P-cores)**: High-performance cores with Hyper-Threading (12th-14th gen), optimized for gaming and demanding workloads.
- **Efficiency cores (E-cores)**: Power-efficient cores without Hyper-Threading, ideal for background tasks and multi-threaded workloads.

Windows tries its best to schedule tasks automatically, but manual control can improve performance in specific scenarios, especially for gaming while multitasking.

# Use cases
## AMD Dual-CCD
- Keep your game locked to one CCD to prevent cross-CCD latency, for maximum FPS.
- Let background apps (OBS, video editors, browsers) use the other CCD.
- Avoid FPS drops caused by background processes running on the same cores as the game.
- Almost always better than core parking!

**Results will be even better for Dual-CCD X3D CPUs**

## Intel Hybrid
- Lock games to P-cores for consistent high performance.
- Assign background tasks (streaming, encoding, downloads) to E-cores.
- Prevent Windows from migrating game threads to E-cores during intense scenes.

## Universal
- Soft-disabling Hyper Threading/SMT for individual games, without rebooting.
- Hotkeys let you assign Core Masks to the current foreground process, allowing you to quickly test what performs best while staying in-game.

# Quick start

## AMD Dual-CCD Setup
**Prerequisite (not technically required, but HIGHLY recommended)**
- Global C-State Control (in BIOS): **Enabled**
- Game Mode (Windows Settings > Gaming > Game Mode): **Off**

**Dual-CCD X3D CPUs only (in addition to the above):**
- X3D Gaming/Turbo Mode (in BIOS): **Disabled**
- CPPC Preferred Cores (in BIOS): **Driver** or **Frequency** 
> [!CAUTION]
> **IMPORTANT!** When using this on games, **always** disable Windows Game Mode. Even if CPPC Preferred Cores is set to Frequency instead of Driver. Windows Game Mode's optimisations may conflict with CPU Set Setter, causing performance reductions or even game crashes when left enabled.

## Intel Hybrid Setup
**Recommended settings**
- Game Mode (Windows Settings > Gaming > Game Mode): **Off**, but feel free to experiment
- Hardware-Accelerated GPU Scheduling (Windows Settings > System > Display > Graphics): **On**

## AFTER the above setup, continue...
If you're on a supported CPU (AMD Dual-CCD | Intel Hybrid CPU), CPU Set Setter will auto-create some default Core Masks for you to start with:
- **AMD Dual-CCD**: "CCD0" and "CCD1", or "Cache" and "Freq" for X3D.
- **Intel Hybrid**: "P-Cores" and "E-Cores"

Just:
1. Download the [latest Release](https://github.com/SimonvBez/CPUSetSetter/releases/latest), extract and run.
2. If prompted, install the .NET 9 Runtime (the app will guide you).
3. Open the **Processes tab**.
4. Apply the appropriate CPU Set to your open game:
   - **AMD**: Apply "Cache" or "CCD0" for gaming
   - **Intel**: Apply "P-Cores" for gaming
5. (Optional) Apply the other CPU Set to heavy background apps:
   - **AMD**: Apply "Freq" or "CCD1" to OBS, browsers, etc.
   - **Intel**: Apply "E-Cores" to OBS, browsers, etc.
6. Done - your choices are saved and auto-applied next time, as long as CPU Set Setter is running.
7. (Optional) Go to the Settings tab and enable "Start with Windows" to make CPU Set Setter start automatically when your computer starts.

If you're on another CPU or want to otherwise tweak which cores can be used, you can create/modify your own Core Masks in the **Masks tab**:
- Click Create..., pick the CPU cores and give your Mask a name.

# Screenshots
![](Images/ProcessesTab.png)

![](Images/MasksTab.png)

![](Images/RulesTab.png)

# Featured on

<a href="https://www.youtube.com/watch?v=m3YVDB0Ymi8"><img src="https://img.youtube.com/vi/m3YVDB0Ymi8/hq720.jpg" width="325" alt="Everyone should try this FREE Utility!"/></a>
<a href="https://www.youtube.com/watch?v=jZXNo-Xu1TY"><img src="https://img.youtube.com/vi/jZXNo-Xu1TY/hq720.jpg" width="325" alt="Use this FREE software to CONTROL your CPU and get MORE FPS!! Testing CPU Set Setter!"/></a>
<br>
❤️

# CPU Sets vs Affinity
But what are these CPU Sets you speak of? Aren't they just Affinities?
<br>
CPU Sets achieve the same results as Affinities; restricting which cores a process can use, but come with some subtle differences:
- **Affinity** = Hard lock (some games crash/freeze)
- **CPU Set** = Very strong hint but may be deviated from when necessary (more stable, works with more games)
- Bonus: CPU Sets require fewer process privileges to set, allowing them to work in games with anti-cheats too

This makes them better fit in almost every scenario.
