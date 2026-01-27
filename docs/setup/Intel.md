# Intel Setup

## Installing and using CPU Set Setter
1. Download the .exe installer or .zip file of the [latest Release](https://github.com/SimonvBez/CPUSetSetter/releases/latest), install/extract and run it.
2. If prompted, install the .NET 10 Runtime (the app will guide you) and run CPU Set Setter again.
3. Open the **Processes tab**.
4. Apply the "P-Cores" Core Mask to your open game.
5. (Optional) Allocate your E-Cores to heavy background apps like OBS, iCue, webbrowsers, etc.
6. Done - your choices are saved and auto-applied next time, as long as CPU Set Setter is running.
7. (Optional) Set a hotkey combination in the Masks tab for some of the Core Mask to change/clear a program's Core Mask on the fly, so you can experiment quickly without having to even tab-out of your game.
8. (Optional) Go to the Settings tab and enable "Start with Windows" to make CPU Set Setter start automatically when your computer starts.

> [!NOTE]
> CPU Set Setter will auto-create some default Core Masks to start with, relevant to your CPU, for example: `P-Cores`, `P-Cores no HT`, `E-Cores` and `All no HT`.
> 
> If your Intel CPU does not have P- and E-cores, only the `All no HT` Mask will be automatically created.
