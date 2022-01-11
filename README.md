# SimpleKVM
Share your monitor with multiple computers.

It is quite expensive to buy a full KVM, particularly one which supports high resolution / high framerate. Using this program and a cheap USB switch (see below), you can achieve the same without spending hundreds of dollars.

## Where to download
Releases can be found over in the [releases](https://github.com/fiddyschmitt/SimpleKVM/releases) section.
(Currenly only for Windows, but Linux and Mac in future)

## What you need
All you need is a USB switch, which you can find on eBay/AliExpress etc.

I've tried the following and they all work well (no affiliation):

[Unnlink 4 Port USB Switch for $21 (USD)](https://www.aliexpress.com/item/32980548420.html). I recommend this one, because it lets you switch between computers using a single hotkey (Windows Key + Numpad 1 through 4)

<img src="https://i.imgur.com/t5bLQp1.jpg" width="400">

[2 ports for $8 (USD)](https://www.ebay.com.au/itm/USB-Sharing-Share-Switch-Box-Hub-2-Ports-PC-Computer-Scanner-Printer-Manual/122620877900). Ideal for two computers:

<img src="https://i.imgur.com/Wj8rLt8l.jpg" width="400">


## How does it work?
The program detects when USB devices connect or disconnect, or when hotkeys are pressed. It then tells the monitor to change its input source using a DDC/CI command, which many monitors support.

## Todo
As the program uses .NET Core, it can be run on Windows, Linux and Mac.
However it only works on Windows at the moment. Contributions welcome :)

## Thanks to
This program was inspired by [haimgel's display-switch program](https://github.com/haimgel/display-switch).
