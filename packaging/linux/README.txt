Simple KVM for Linux
====================

Switches your monitors' inputs when your USB switch changes computers, or on a hotkey.
Project page, documentation and newer versions: https://github.com/fiddyschmitt/SimpleKVM

Install it (no root needed):

    ./install.sh              puts the program in ~/.local/bin and adds it to the applications menu
    ./install.sh --startup    the same, and starts it at every login

Or just run it from this folder: ./simplekvm
Remove it again with ./uninstall.sh (your rules and settings are kept).

Monitors are controlled over DDC/CI through /dev/i2c-*. If Simple KVM says it has no access,
install your distribution's ddcutil package (its udev rules grant the access) or add yourself
to the i2c group, and make sure the i2c-dev kernel module is loaded. The project page's
"Linux notes" have the details, including how hotkeys work on each desktop.
