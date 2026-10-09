# Simple KVM
Control multiple computers using one set of Keyboard, Mouse and Monitor.

Full KVMs are quite expensive, particularly ones which supports high resolution / high framerate.

Using this program and a cheap USB Switch (see below), you can achieve the same without spending hundreds of dollars.

<img width="800" alt="SimpleKVM" src="https://github.com/fiddyschmitt/SimpleKVM/assets/15338956/fb0a0817-f6f5-415e-b027-0fc5b0d19b92">

## Where to download
Releases can be found over in the [releases](https://github.com/fiddyschmitt/SimpleKVM/releases/latest) section.

- **Windows**: download `SimpleKVM.exe` and run it.
- **macOS** (Apple Silicon): download `SimpleKVM-macos-arm64.zip`, unzip it, and drag `SimpleKVM.app` to Applications. Because the app isn't notarized, macOS blocks the first launch: open System Settings → Privacy & Security, scroll down to the message saying SimpleKVM was blocked, and click "Open Anyway" (one time only). Terminal alternative: `xattr -dr com.apple.quarantine /Applications/SimpleKVM.app`.

- **Linux**: download `simplekvm-linux-x64.tar.gz` (or `simplekvm-linux-arm64.tar.gz` for ARM), unpack it, and run `./install.sh` in the unpacked folder. See [Linux notes](#linux-notes).

## What you need

1. Connect your computers to your monitor.
2. Connect your mouse & keyboard to a USB Switch.
3. Run SimpleKVM on one of the computers.

   ![image](https://github.com/user-attachments/assets/8e73fa91-49b3-4c19-983b-8367e8a1eaba)


You can now switch between the computers using the USB Switch, or a hotkey.

## USB Switches
Any USB Switch will do. Here are some examples (no affiliation).

<br />
<br />

This one supports 4 computers. [SABRENT 4 Port USB Switch for $29 USD](https://www.amazon.com/Sabrent-Computers-Peripherals-Indicators-USB-USS4/dp/B07RC8F2L3)

<img src="https://github.com/fiddyschmitt/SimpleKVM/assets/15338956/e18b938e-7b8c-4515-9d63-78c858ba2fad" width="400">

<br />
<br />
<br />
<br />

This one supports 2 computers. [2 port USB switch for $24 USD](https://www.amazon.com/UGREEN-Selector-Computers-Peripheral-One-Button/dp/B01MXXQKGM)

<img src="https://github.com/fiddyschmitt/SimpleKVM/assets/15338956/3dd14d24-c00a-48b0-b812-7e4647d4d25b" width="400">

<br />
<br />
<br />
<br />

This one supports 2 computers, but only has one input USB. [2 ports for for $3 USD](https://www.aliexpress.com/item/1005005372231623.html)

<img src="https://github.com/fiddyschmitt/SimpleKVM/assets/15338956/69acf3fd-f5f8-4522-9c08-63f2242d4021" width="400">

<br />
<br />

## How does it work?
The program detects when USB devices connect or disconnect, or when hotkeys are pressed. It then tells the monitor to change its input source using a DDC/CI command, which many monitors support.

## Does it support multiple monitors?

Yes

![image](https://github.com/user-attachments/assets/f63bb585-4215-4ffe-8a29-995bab5aceae)


## Run at startup
1. Click the settings button
2. Tick 'Run at Startup'

On Windows this creates a shortcut in the Startup folder; on macOS it creates a LaunchAgent in `~/Library/LaunchAgents`; on Linux, an autostart entry in `~/.config/autostart`.

## Linux notes
- **Installing**: `tar -xzf simplekvm-linux-x64.tar.gz`, then `simplekvm/install.sh`. It puts the program in `~/.local/bin` and Simple KVM in your applications menu; `install.sh --startup` also starts it at every login, and `uninstall.sh` removes it again (your rules stay). Nothing needs root. You can also just run `simplekvm/simplekvm` where you unpacked it, and tick "Show in the applications menu" in Settings if you want it listed there. (Where hotkeys go through the desktop's shortcut service, the app keeps a hidden menu entry in any case, because the desktop identifies it by that file.)
- **Monitors (DDC/CI)** go through `/dev/i2c-*`, so the `i2c-dev` module must be loaded (`sudo modprobe i2c-dev`, and `i2c-dev` in `/etc/modules-load.d/` to keep it). Your user needs access to the display buses: ddcutil's udev rules grant it to the logged-in user (they come with the `ddcutil` package, which KDE Plasma already pulls in for brightness control); otherwise add yourself to the `i2c` group. [ddcutil's permissions guide](https://www.ddcutil.com/i2c_permissions) has the details. The rule editor and `--list-monitors` say so when access is missing. Buses are matched to connectors by EDID, so NVIDIA's proprietary driver works too.
- **Screen layout** comes from the desktop: GNOME (Mutter) and KDE Plasma (`kscreen-doctor`) on Wayland, and `xrandr` on any X11 desktop. Elsewhere screens are laid out left to right.
- **Hotkeys** need no special permission on most desktops, and the key combination is the app's alone:
  - On **X11** (any desktop) the keys are grabbed from the X server. A combination something else already holds shows as unavailable when you choose it.
  - On **Wayland with GNOME 48 or later, or KDE Plasma 6**, they go through the desktop's global shortcuts service. The desktop asks you to confirm each new hotkey the first time, and you can change its keys afterwards in the desktop's own keyboard settings.
  - On **other Wayland desktops** (GNOME 46 on Ubuntu 24.04, for one) the keys are read from `/dev/input`, which needs the `input` group: `sudo usermod -aG input $USER`, then log in again. Two things to know there: the group lets any program you run read your keystrokes, and the hotkeys aren't exclusive, so the combination also reaches the focused application and the desktop's own shortcuts. Pick one nothing else uses.
- **Idle detection** ("no longer idle" rules) needs nothing on GNOME, which reports idle time itself. KDE Plasma on Wayland has no such interface, so there the `input` group is needed (the rule editor says so).
- **The window**: closing it leaves Simple KVM running, so your rules keep working. The tray icon brings the window back, and so does starting Simple KVM again, which is the way on GNOME (it shows no tray icons unless the AppIndicator extension is installed). To stop it, use the window's Quit button, the tray menu, a right-click on its launcher icon, or `simplekvm --quit`.
- **USB events** are read from sysfs and need no permissions.
- **Run at startup** writes `~/.config/autostart/io.github.fiddyschmitt.simplekvm.desktop`. Settings and rules live in `~/.config/simplekvm`, or under `$XDG_CONFIG_HOME` when that is set.
- **Troubleshooting**: `simplekvm --test-hotkey "Ctrl+Alt+F1"` and `simplekvm --watch-idle` say which of these routes is in use on your desktop. `SIMPLEKVM_HOTKEYS=evdev` (or `x11`, `portal`) and `SIMPLEKVM_IDLE_SOURCE=evdev` (or `desktop`) in the environment pin one, should the automatic choice not suit.
- **Building from source**: `dotnet publish SimpleKVM/SimpleKVM.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`, then run `SimpleKVM/bin/Release/net10.0/linux-x64/publish/SimpleKVM`.

## Thanks to
This program was inspired by [haimgel's display-switch program](https://github.com/haimgel/display-switch).
