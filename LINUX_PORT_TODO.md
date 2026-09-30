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
- [ ] **G2** KDE Plasma (Wayland) keeps working (the contributor's tested setup).
      Fedora KDE VM being built; run the system tests on it, and confirm the units of
      KDE's ScreenSaver idle reply (the fallback test fails loudly if they're seconds).
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
      as a follow-up: the first layout query can run on the UI thread (rule editor), and
      a managed D-Bus client (Tmds.DBus) would remove the process spawns altogether.
- [x] **A3** Hotkeys via evdev: document non-exclusivity and the `input` group trade-off;
      note the XDG GlobalShortcuts portal as the sanctioned exclusive route (follow-up).
      Documented in the README's Linux notes. The portal remains a follow-up.
- [x] **A4** I2C probing: allow-list adapters by parent display controller (ddcutil's rule)
      instead of a name blocklist; don't re-scan every 30 s for a monitor that never matched.
- [x] **A5** Bus lock per bus (keyed by device path), not per transport object.

## Gaps

- [x] **T1** Unit tests for the pure logic: kscreen JSON, Mutter GVariant text, gdbus values,
      xrandr output, USB sysfs snapshot diff, key-code map, desktop-entry escaping,
      EDID keying / duplicate detection, layout join rules. (86 -> 149 unit tests.)
- [ ] **T2** System tests against real desktops in VMs (Ubuntu GNOME Wayland, Ubuntu GNOME
      X11, Fedora GNOME, Fedora KDE): list-monitors, get-caps, watch-idle both paths,
      test-hotkey via uinput, set-startup, GUI launch, verify-rules (trim smoke).
      `SimpleKVM.SystemTests`: green on both Ubuntu VMs; the Fedora VMs are next.
- [x] **T3** VM provisioning in `SimpleKVM local` (Vagrant + Ansible), mirroring condeco.
- [x] **R1** Release pipeline: linux-x64 artifact in `publish-release.ps1` (+ pubxml), README
      download note. Native libs must travel with the binary (self-extract).
- [x] **U1** Permission feedback in the GUI when I2C or input access is missing.
- [x] **S1** Smaller items: accelerometer-style devices pin idle at zero; USB watcher's first
      snapshot outside try; `Exec=` escaping in the autostart entry; PrintScreen/CapsLock/
      media keys unmapped; CLI help still says macOS-only; `LinuxIdle` file placement;
      README Fedora permission claim (ddcutil's udev rule).
