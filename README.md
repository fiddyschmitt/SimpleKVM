# Simple KVM
Share your monitor with multiple computers.

Full KVMs are quite expensive, particularly ones which supports high resolution / high framerate. Using this program and a cheap USB switch (see below), you can achieve the same without spending hundreds of dollars.

<img width="800" alt="SimpleKVM" src="https://user-images.githubusercontent.com/15338956/210045381-d75bd9ca-f3f6-46a2-8c02-a11ee0977785.PNG">

## Where to download
Releases can be found over in the [releases](https://github.com/fiddyschmitt/SimpleKVM/releases) section.
(Currenly only for Windows)

## What you need
All you need is a USB switch such as the following (no affiliation).

<br />
<br />

This one supports 4 computers. [SABRENT 4 Port USB Switch for $27 USD](https://www.amazon.com/Sabrent-Computers-Peripherals-Indicators-USB-USS4/dp/B07RC8F2L3).

<img src="https://github.com/fiddyschmitt/SimpleKVM/assets/15338956/e18b938e-7b8c-4515-9d63-78c858ba2fad" width="400">

<br />
<br />
<br />
<br />

This one supports 2 computers, but only has one input USB. [2 ports for $8 USD](https://www.ebay.com.au/itm/USB-Sharing-Share-Switch-Box-Hub-2-Ports-PC-Computer-Scanner-Printer-Manual/122620877900).

<img src="https://i.imgur.com/Wj8rLt8l.jpg" width="400">

<br />
<br />

This one supports 2 computers, but only has one input USB. [2 ports for for $8 USD](https://www.aliexpress.com/item/1005005372231623.html)

<img src="https://github.com/fiddyschmitt/SimpleKVM/assets/15338956/69acf3fd-f5f8-4522-9c08-63f2242d4021" width="400">

<br />
<br />

## How does it work?
The program detects when USB devices connect or disconnect, or when hotkeys are pressed. It then tells the monitor to change its input source using a DDC/CI command, which many monitors support.

## Run at startup
1. Open the run window, using `win + r`
2. Open the startup folder by typing: `shell:startup`
3. Paste a shortcut to `SimpleKVM.exe` into that folder
4. Open the properties for the shortcut, and select Run: Minimized

## License

```
A work by Fidel Perez-Smith.

This software is provided 'as-is', without any express or implied
warranty. In no event will the authors be held liable for any damages
arising from the use of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it
freely, subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not
   claim that you wrote the original software. If you use this software
   in a product, an acknowledgment in the product documentation would be
   appreciated but is not required.
2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.
3. This notice may not be removed or altered from any source distribution.
```

## Thanks to
This program was inspired by [haimgel's display-switch program](https://github.com/haimgel/display-switch).
