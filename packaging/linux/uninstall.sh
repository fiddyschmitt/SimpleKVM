#!/bin/sh
# Removes what install.sh put in place: the program, its applications-menu entry and icon,
# and its start-at-login entry. Your rules and settings are left alone.
set -eu

target="$HOME/.local/bin/simplekvm"

if [ -x "$target" ]; then
    "$target" --quit >/dev/null 2>&1 || true
    "$target" --set-startup off >/dev/null 2>&1 || true
    "$target" --set-menu-entry off >/dev/null 2>&1 || true
    rm -f "$target"
    echo "Simple KVM has been removed."
else
    echo "Simple KVM is not installed at $target."
fi

config="${XDG_CONFIG_HOME:-$HOME/.config}/simplekvm"
[ -d "$config" ] && echo "Your rules and settings are still in $config; delete that folder to remove them too."
exit 0
