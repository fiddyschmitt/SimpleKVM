# SimpleKVM
Share your monitor with multiple computers.

It is quite expensive to buy a full KVM, particularly one which supports high resolution / high framerate. Using this program and a cheap USB switch (see below), you can achieve the same without spending hundreds of dollars.

<img width="442" alt="SimpleKVM" src="https://user-images.githubusercontent.com/15338956/210045381-d75bd9ca-f3f6-46a2-8c02-a11ee0977785.PNG">

## Where to download
Releases can be found over in the [releases](https://github.com/fiddyschmitt/SimpleKVM/releases) section.
(Currenly only for Windows, but Linux and Mac in future)

## What you need
All you need is a USB switch.

I've tried the following and they all work well (no affiliation):

[Unnlink 4 Port USB Switch for $21 (USD)](https://www.aliexpress.com/item/32980548420.html). Supports 4 computers. I recommend this one, because it lets you switch between computers using a hotkey (Windows Key + Numpad 1 through 4)

<img src="https://i.imgur.com/t5bLQp1.jpg" width="400">

[2 ports for $8 (USD)](https://www.ebay.com.au/itm/USB-Sharing-Share-Switch-Box-Hub-2-Ports-PC-Computer-Scanner-Printer-Manual/122620877900). Supports 2 computers.

<img src="https://i.imgur.com/Wj8rLt8l.jpg" width="400">

[AVMTON USB 3.0 Switch Selector for $23 (USD)](https://www.amazon.com/dp/B08JCNFVHR). Supports 2 computers. This one has a special feature - it automatically switches to the computer that was just powered on, or switches to the other computer when the current one powers off. It's also USB 3.0. Thanks for the recommendation neon-dev!

<img src="https://user-images.githubusercontent.com/15338956/210045747-e53cb070-87fd-4922-a25d-661a4d1c9f6f.png" width="400">

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
