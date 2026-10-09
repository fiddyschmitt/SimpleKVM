# Linux port: work list

Tracks the items from the review of PR #40 (the Linux backend, taken into
`feature/linux-port`) plus the follow-on goals. Tick items off as they land;
each should be its own commit. Delete this file before the branch merges.

Test rig: the Linux VMs in `..\SimpleKVM local\provisioning\linux` (Vagrant +
VirtualBox), driven by the `SimpleKVM.SystemTests` project.

## Defects (must fix before merge)

- [x] **D1** D-Bus idle fallback always reports 64 ms: the regex reads the "64" in the
      `uint64` type annotation gdbus prints. No-Longer-Idle rules never fire without the
      `input` group. Also spawns `gdbus` on every 100 ms poll.
- [x] **D2** GNOME layout path can never succeed: the monitor regex stops inside the first
      mode (each mode ends with `[scales], {props}`), and the first logical monitor's
      transform prints as `uint32 0`. Always falls back to the left-to-right guess.
- [x] **D3** X11 sessions: compositor output names come from the X driver, not DRM
      (`HDMI-1` vs `HDMI-A-1`; NVIDIA counts from 0). Unmatched connectors are dropped and
      an off-by-one match pairs a monitor with its neighbour's geometry.
- [x] **D4** Two monitors with identical EDIDs share one bus in the NVIDIA fallback, so
      switching one switches the other. Ambiguous matches must leave the transport unset.
- [x] **D5** A failed/slow `kscreen-doctor` call drops to the guessed layout, re-keying every
      monitor and re-probing them, then flapping back. Keep the last good layout.

## Desktop support goals

- [x] **G1** GNOME (Wayland and X11) works end to end: layout, idle, tray/relaunch story.
      All 21 system tests green on Ubuntu 24.04 GNOME, Wayland and Xorg (2026-09-30).
- [x] **G2** KDE Plasma (Wayland) keeps working (the contributor's tested setup).
      Green on the Fedora 42 KDE VM: kscreen-doctor layout, GUI, hotkeys, evdev idle.
      Finding: Plasma on Wayland refuses ScreenSaver.GetSessionIdleTime, so without the
      input group there is no idle source there; the app now says so (CLI note, editor hint).
- [x] **G3** X11 sessions on any desktop: layout and EDID pairing via `xrandr`.
- [x] **G4** Single-instance guard: a second launch shows the running instance's window
      (needed on GNOME, which has no tray without an extension; helps Windows too).

## Design items

- [x] **A1** Duplication: third copy of monitor building / EDID name parsing / `Monitor`
      subclass / capabilities loop. Extract a shared DDC transport interface + one builder
      (keep the persisted `$type` names mapping through the binder).
- [x] **A2** External programs: cache/throttle layout queries; read stderr or don't redirect
      it; don't block the UI thread; note Tmds.DBus as the long-term route.
      Done: ExternalTool drains both streams with a timeout; the compositor is re-asked
      only on connector changes or every 10 s; idle asks D-Bus once a second. Still open
      as a follow-up: the first layout query can run on the UI thread (rule editor). The
      `gdbus` spawns went with L4; `kscreen-doctor` and `xrandr` are still processes.
- [x] **A3** Hotkeys via evdev: document non-exclusivity and the `input` group trade-off;
      note the XDG GlobalShortcuts portal as the sanctioned exclusive route (follow-up).
      Documented in the README's Linux notes. The portal arrived with L3, which leaves
      evdev as the route of last resort.
- [x] **A4** I2C probing: allow-list adapters by parent display controller (ddcutil's rule)
      instead of a name blocklist; don't re-scan every 30 s for a monitor that never matched.
- [x] **A5** Bus lock per bus (keyed by device path), not per transport object.

## Gaps

- [x] **T1** Unit tests for the pure logic: kscreen JSON, Mutter GVariant text, gdbus values,
      xrandr output, USB sysfs snapshot diff, key-code map, desktop-entry escaping,
      EDID keying / duplicate detection, layout join rules. (86 -> 149 unit tests.)
- [x] **T2** System tests against real desktops in VMs (Ubuntu GNOME Wayland, Ubuntu GNOME
      X11, Fedora GNOME, Fedora KDE): list-monitors, get-caps, watch-idle both paths,
      test-hotkey via uinput, set-startup, GUI launch, verify-rules (trim smoke).
      Every VM has two virtual screens side by side; list-monitors must report both from
      the compositor, adjacent, numbered left to right (and match `xrandr` on X11).
      `SimpleKVM.SystemTests`: 52 runs across all four VMs, 47 passed, 5 skipped by
      design (input-group variants; KDE's absent D-Bus idle), 0 failed (2026-09-30).
- [x] **T3** VM provisioning in `SimpleKVM local` (Vagrant + Ansible), mirroring condeco.
- [x] **R1** Release pipeline: linux-x64 artifact in `publish-release.ps1` (+ pubxml), README
      download note. Native libs must travel with the binary (self-extract).
- [x] **U1** Permission feedback in the GUI when I2C or input access is missing.
- [x] **S1** Smaller items: accelerometer-style devices pin idle at zero; USB watcher's first
      snapshot outside try; `Exec=` escaping in the autostart entry; PrintScreen/CapsLock/
      media keys unmapped; CLI help still says macOS-only; `LinuxIdle` file placement;
      README Fedora permission claim (ddcutil's udev rule).

## Round two (from the second review)

- [x] **L1** Closing the window keeps the app running (as on macOS); an explicit Quit exits:
      a Quit button in the window, the tray menu, the launcher's right-click menu, `--quit`.
      A later launch is listened for before the rules start, so one made while the app is
      still starting finds it.
- [x] **L2** Launcher entry and icon (`io.github.fiddyschmitt.simplekvm.desktop`): written by
      install.sh, or by the user through the Settings tick box, not by the app on its own
      (opt-in, as is the convention on Linux). Untick keeps it hidden rather than removing it,
      because the entry is also the identity the desktop portals know the app by, and it is
      written hidden wherever the portal route is in use. `--set-menu-entry
      on|hidden|off|status`. The autostart
      entry carries the same name. Icon: `packaging/linux/simplekvm.png`, drawn from the
      32x32 .ico by `make-icon.py`; a higher-resolution original would look better.
- [x] **L3** Hotkeys without the `input` group: key grabs on X11 (libxcb), the
      GlobalShortcuts portal on Wayland (GNOME 48+, KDE Plasma 6), evdev only where neither
      exists (GNOME 46). The hotkey chooser shows what the route in use means for the user.
      `SIMPLEKVM_HOTKEYS` pins a route.
- [x] **L4** D-Bus spoken directly (Tmds.DBus.Protocol, which Avalonia already ships) instead
      of spawning `gdbus`: Mutter's layout with its MonitorsChanged signal (no more 10 s
      re-asking on GNOME), idle time through Mutter's idle and user-active watches (no
      polling), the ScreenSaver interface. A desktop that is slow to answer is asked again
      rather than written off. `SIMPLEKVM_IDLE_SOURCE` pins a source.
- [x] **L5** Packaging: `simplekvm-linux-x64.tar.gz` and `-arm64` with the executable bit
      (`scripts/package-linux.ps1`: Windows' tar fed an mtree specification), `install.sh`
      (`--startup`), `uninstall.sh`. The arm64 build is started under emulation by one
      system test; it has not run on ARM hardware.

System tests after round two: 20 tests on each of the four VMs.

Still open from the second review (not chosen for this round): `xrandr` should be asked
with `--current`; layouts differing from QWERTY on the evdev route; the monitor cache is
keyed by position only; name-before-EDID pairing on NVIDIA under X11; the kernel `ddcci`
driver holding the bus address; game controllers counting as activity; the I2C hint on
Fedora; desktops without a layout source (Sway, Hyprland, COSMIC); CI; a fake bus for the
DDC framing. And before release: DDC on real hardware.
