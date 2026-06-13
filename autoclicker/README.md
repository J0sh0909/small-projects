# Autoclicker

A configurable auto-clicker with human-like randomized timing. Runs as a background process and is controlled entirely via hotkeys.

## How it works

1. **Toggle on** — running the script/exe starts a click loop in a background thread.
2. **Click loop** — clicks at the rate defined in `config.json`, with randomized intervals to simulate natural input (uniform, gaussian, and beta-distribution strategies are chosen at random each interval).
3. **Toggle off** — running the script/exe a second time detects the existing PID file and kills the running instance.
4. A `click_log.txt` is written next to the exe on every session, recording each click number and the delay used.

## Configuration (`config.json`)

Set **exactly one** rate field to a non-zero value; the other two must be `0`.

| Field | Description |
|---|---|
| `clicks_per_second` | Click rate in CPS |
| `clicks_per_minute` | Click rate in CPM |
| `clicks_per_hour` | Click rate in CPH |
| `x` / `y` | Screen coordinates to click. Set both to `null` to click at the current cursor position. |
| `button` | Mouse button: `"left"`, `"right"`, or `"middle"` |
| `stop_key` | Hotkey that immediately stops the clicker (e.g. `"ctrl+shift+f9"`) |

## Dependencies

```
pip install pyautogui keyboard psutil
```

> PyAutoGUI's failsafe is enabled — move the mouse to the **top-left corner** of the screen to abort if the clicker gets stuck.

## Build standalone exe (optional)

```
pip install pyinstaller
pyinstaller autoclicker.spec
```

The exe is output to the project root. It reads `config.json` from the same directory.

## Run on startup with AutoHotkey (recommended)

The included `launcher.ahk` binds `Ctrl+Shift+F8` to launch the exe. Running it at Windows startup means the hotkey is always available.

### Step-by-step

1. **Install AutoHotkey v2** from [autohotkey.com](https://www.autohotkey.com/).

2. **Update the path** in `launcher.ahk` if you moved the project folder:
   ```ahk
   Run('"C:\path\to\autoclicker\autoclicker.exe"')
   ```

3. **Add `launcher.ahk` to the Windows startup folder:**
   - Press `Win+R`, type `shell:startup`, press Enter.
   - Copy (or create a shortcut to) `launcher.ahk` into that folder.

4. **Reboot or double-click `launcher.ahk`** to activate it immediately.

5. Press `Ctrl+Shift+F8` to start clicking. Press it again to stop.

> The in-app stop key (`stop_key` in config) also stops the clicker immediately without re-launching the exe.
