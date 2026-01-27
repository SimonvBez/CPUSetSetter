# AMD Setup

## Prerequisites for Ryzen 9 X3D
Only follow this if you have a Ryzen 9 X3D CPU, like the 9950X3D, 9900X3D, 7950X3D, 7900X3D.
<br>
If you have another type of AMD CPU, like a Ryzen 7, skip this.

While not technically required, it is **strongly recommended** to follow these settings:

- Global C-State Control (in BIOS): **Enabled**
- X3D Gaming/Turbo Mode (in BIOS): **Disabled**
  - This will ensure all your cores are available for use.
- CPPC Preferred Cores (in BIOS): **Driver** (or Frequency)
  - This will make Windows prefer to use the Frequency cores, so your Cache cores remain available for games you configure with CPU Set Setter.
- Game Mode (Windows Settings > Gaming > Game Mode): **OFF**
> [!CAUTION]
> **IMPORTANT!** On Ryzen 9 X3D CPUs, **always** disable Windows Game Mode when using CPU Set Setter on a game! Even if CPPC Preferred Cores is set to Frequency instead of Driver. Windows Game Mode's optimisations may conflict with CPU Set Setter, causing performance reductions or even game crashes when left enabled.

## Installing and using CPU Set Setter
1. Download the .exe installer or .zip file of the [latest Release](https://github.com/SimonvBez/CPUSetSetter/releases/latest), install/extract and run it.
2. If prompted, install the .NET 10 Runtime (the app will guide you) and run CPU Set Setter again.
3. Open the **Processes tab**.
4. Apply the "Cache" or "CCD0" Core Mask to your open game. (for some, CCD1 was better for gaming. Try both!)
5. (Optional) Allocate your other CCD ("Freq" or "CCD1") to heavy background apps like OBS, iCue, webbrowsers, etc.
6. Done - your choices are saved and auto-applied next time, as long as CPU Set Setter is running.
7. (Optional) Set a hotkey combination in the Masks tab for some of the Core Mask to change/clear a program's Core Mask on the fly, so you can experiment quickly without having to even tab-out of your game.
8. (Optional) Go to the Settings tab and enable "Start with Windows" to make CPU Set Setter start automatically when your computer starts.

> [!NOTE]
> CPU Set Setter will auto-create some default Core Masks to start with, relevant to your CPU, for example: `CCD0`, `CCD1`, `Cache`, `Cache no SMT`, `Freq`, `Freq no SMT` and `All no SMT`.
> 
> On **Windows 10, automatic CCD detection is not possible**, so you will have to create those Core Masks for yourself in the Masks tab; Click `Create...`, pick the CPU cores and give your Mask a name. Also, you should probably upgrade to Windows 11, even if it's just for the [better performance](https://www.youtube.com/watch?v=32lBRYknKgA)
