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

## Build

Requires Visual Studio 2026 (or 2022) MSBuild. You don't need the VS SDK workload because the build tools come from NuGet.

```
"C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe" SolutionColor.sln -restore -p:Configuration=Release
```

Output: `src\SolutionColor\bin\Release\net48\SolutionColor.vsix`

## Publish to the Visual Studio Marketplace

The Marketplace page text is in [`marketplace/overview.md`](marketplace/overview.md) and the publish settings are in [`marketplace/publishManifest.json`](marketplace/publishManifest.json).
You can upload `dist\SolutionColor.vsix` at https://marketplace.visualstudio.com/manage and paste the overview, or publish from the command line:

```
"D:\.nuget\microsoft.vssdk.buildtools\18.9.820\tools\VSSDK\bin\VsixPublisher.exe" publish -payload dist\SolutionColor.vsix -publishManifest marketplace\publishManifest.json -personalAccessToken <PAT>
```

`VsixPublisher.exe` comes with the `Microsoft.VSSDK.BuildTools` NuGet package (under `tools\VSSDK\bin`). The path above is your local NuGet cache.

## Troubleshooting

Visual Studio has no public API for the title bar, so the extension finds it in the WPF visual tree.
Diagnostics go to **View > Output > "Solution Color"**. Check them if the title bar isn't colored.
