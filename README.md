# Solution Color

A Visual Studio 2022/2026 extension that gives each solution its own color:

- **Title bar**: the main window title bar (including the integrated menu bar) is painted with the solution color.
- **Icon**: the Visual Studio logo in the title bar and the window/taskbar icon are tinted to the solution color.
- **Taskbar badge**: a small colored dot is shown on the taskbar button.
- **Window border** (Windows 11): the window border uses the solution color.

## Install

Double-click `dist/SolutionColor.vsix` (with Visual Studio closed), then start Visual Studio.

## Use

Right-click the **Solution** node in Solution Explorer and choose **Solution Color...** (it's also under **Tools**).
Pick a preset, type a hex value, open the full color picker, or click **From name** to get a stable color generated from the solution name.
The title bar updates live while the dialog is open. Click **Cancel** to undo.

Settings are saved per solution:

| Option | File |
|---|---|
| Default (per user) | `<solution dir>\.vs\<SolutionName>\SolutionColor.json` |
| "Share with the team" | `<solution dir>\<SolutionName>.slncolor.json` (commit it) |

If both files exist, the per-user file wins.

