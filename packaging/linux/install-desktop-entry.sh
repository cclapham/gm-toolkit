#!/usr/bin/env bash
set -euo pipefail

# Installs a .desktop entry and icon for GM Toolkit into the current user's
# applications/icon directories, so it shows up with the correct icon in
# GNOME/KDE/etc taskbars, docks and app menus. The tarball release doesn't
# use an installer (see issue #40), so this is an opt-in step a user runs
# once after extracting it -- without it, the app still runs fine, it just
# has no desktop-menu entry and falls back to a generic icon in the dock,
# since GNOME Shell resolves dock/taskbar icons from an installed .desktop
# file's Icon= key, not from the running window's own icon hint.
#
# Run this script from the directory it was extracted into, alongside
# GmToolkit.Desktop and gmtoolkit.png.

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec_path="$script_dir/GmToolkit.Desktop"
icon_source="$script_dir/gmtoolkit.png"

if [[ ! -x "$exec_path" ]]; then
  echo "error: GmToolkit.Desktop not found (or not executable) next to this script in $script_dir." >&2
  exit 1
fi

if [[ ! -f "$icon_source" ]]; then
  echo "error: gmtoolkit.png not found next to this script in $script_dir." >&2
  exit 1
fi

icon_dir="$HOME/.local/share/icons/hicolor/256x256/apps"
apps_dir="$HOME/.local/share/applications"
mkdir -p "$icon_dir" "$apps_dir"

cp "$icon_source" "$icon_dir/gmtoolkit.png"
sed "s|__EXEC_PATH__|$exec_path|" "$script_dir/gmtoolkit.desktop" > "$apps_dir/gmtoolkit.desktop"
chmod +x "$apps_dir/gmtoolkit.desktop"

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$apps_dir" 2>/dev/null || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -f "$HOME/.local/share/icons/hicolor" 2>/dev/null || true
fi

echo "Installed. GM Toolkit should now appear in your application menu and taskbar with its own icon."
