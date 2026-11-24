#!/bin/sh
# Installs Simple KVM for the current user: the program goes to ~/.local/bin/simplekvm and
# a launcher with its icon into the applications menu. Nothing needs root, and running it
# again over an older version upgrades in place.
#
#   ./install.sh              install
#   ./install.sh --startup    install, and start Simple KVM at every login
#
# To remove it again: ./uninstall.sh
set -eu

startup=no
for arg in "$@"; do
    case "$arg" in
        --startup) startup=yes ;;
        -h|--help) sed -n '2,9p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "install.sh: unknown option $arg (try --help)" >&2; exit 2 ;;
    esac
done

if [ "$(id -u)" -eq 0 ]; then
    echo "Run this as yourself, not as root: Simple KVM installs into your home folder." >&2
    exit 1
fi

here=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
bin_dir="$HOME/.local/bin"
target="$bin_dir/simplekvm"

[ -f "$here/simplekvm" ] || { echo "install.sh: simplekvm is missing next to this script" >&2; exit 1; }

# An older copy that is running is asked to leave, and started again afterwards
was_running=no
if [ -x "$target" ] && "$target" --quit >/dev/null 2>&1; then
    was_running=yes
    sleep 1
fi

# Copied under another name and renamed over the old program, so there is never half a program at the path
mkdir -p "$bin_dir"
cp "$here/simplekvm" "$target.new"
chmod 755 "$target.new"
mv -f "$target.new" "$target"

"$target" --set-menu-entry on >/dev/null
[ "$startup" = yes ] && "$target" --set-startup on >/dev/null

echo "Simple KVM is installed: $target"
echo "Start it from the applications menu, or run: simplekvm"
case ":$PATH:" in
    *":$bin_dir:"*) ;;
    *) echo "($bin_dir is not on your PATH, so from a terminal use the full path.)" ;;
esac
[ "$startup" = yes ] && echo "It will start at every login."

# What stands between Simple KVM and the monitors, if anything (the app says the same in its rule editor)
"$target" --list-monitors 2>/dev/null | sed -n 's/^Note: /Note: /p' || true

if [ "$was_running" = yes ] && [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then
    nohup "$target" --minimized >/dev/null 2>&1 &
    echo "The copy that was running has been restarted."
fi
