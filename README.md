# Simple KVM
Control multiple computers using one set of Keyboard, Mouse and Monitor.

Full KVMs are quite expensive, particularly ones which supports high resolution / high framerate.

Using this program and a cheap USB Switch (see below), you can achieve the same without spending hundreds of dollars.

<img width="800" alt="SimpleKVM" src="https://github.com/fiddyschmitt/SimpleKVM/assets/15338956/fb0a0817-f6f5-415e-b027-0fc5b0d19b92">

## Where to download
Releases can be found over in the [releases](https://github.com/fiddyschmitt/SimpleKVM/releases) section.
(Currenly only for Windows)

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
1. Open the run window, using `win + r`
2. Open the startup folder by typing: `shell:startup`
3. Paste a shortcut to `SimpleKVM.exe` into that folder
4. Open the properties for the shortcut, and select Run: Minimized

## Thanks to
This program was inspired by [haimgel's display-switch program](https://github.com/haimgel/display-switch).
