# SimpleKVM
Use multiple computers using one monitor, mouse and keyboard.

It is quite expensive to buy a full KVM, particularly one which supports high resolution / high framerate. Using this program and a cheap USB switch (see below), you can achieve the same without spending hundreds of dollars.

# Where to download
Releases can be found over in the [releases](https://github.com/fiddyschmitt/SimpleKVM/releases) section.
(Currenly only for Windows, but Linux and Mac in future)

# What you need
All you need is a USB switch, which you can find on eBay/AliExpress etc.

I've tried two types and they both work well (no affiliation):

[2 ports for $8 (USD)](https://www.ebay.com.au/itm/USB-Sharing-Share-Switch-Box-Hub-2-Ports-PC-Computer-Scanner-Printer-Manual/122620877900):

<img src="https://i.imgur.com/Wj8rLt8l.jpg" width="400">



[4 ports for $11 (USD)](https://www.ebay.com.au/itm/4-Ports-USB2-0-Sharing-Device-Switch-Switcher-Adapter-Box-for-PC-Scanner-P-N1S8/293680413168):

<img src="https://i.imgur.com/xAsG3hLl.jpg" width="400">

If you have two computers, you only need to run SimpleKVM on one of them. For example:

![alt text](https://i.imgur.com/2mLcZX9.png)

# How does it work?
The program detects when USB devices connect or disconnect, or when hotkeys are pressed. It then tells the monitor to change its input source using DDC/CI.

# Todo
As the program uses .NET Core, it can be run on Windows, Linux and Mac.
However it only works on Windows at the moment. Contributions welcome :)

# Thanks to
This program was inspired by [haimgel's display-switch program](https://github.com/haimgel/display-switch).
