<img src="images/icon.png" width="96" align="right" alt="Solution Color icon" />

# Solution Color

**Give every solution its own color, and stop guessing which Visual Studio window is which.**

If you keep several Visual Studio instances open (a backend, a frontend, a hotfix branch), they all look the same in the taskbar and on screen. Solution Color paints each instance with a color you choose per solution, so you can tell them apart at a glance.

## Features

- **Colored title bar.** The Visual Studio title bar, including the integrated menu bar, uses the solution's color.
- **Tinted Visual Studio icon.** The logo in the title bar and the window icon are recolored to match.
- **Taskbar badge.** A small colored dot on the taskbar button helps you pick the right instance from the taskbar.
- **Colored window border.** On Windows 11 the window border uses the solution color too.
- **Live preview.** You see the change on the real window while you pick, and Cancel puts it back.
- **Automatic.** The color is applied when a solution opens and removed when it closes.
- **Per user or shared with your team.** Keep the color to yourself, or commit it with the repository so everyone gets the same color.
- **Open Folder support.** Works for folders opened with *File > Open > Folder* too.

## Getting started

1. Open a solution.
2. Right-click the **Solution** node in Solution Explorer and choose **Solution Color...**
   (it's also available under the **Tools** menu).
3. Pick a color:
   - click one of the **presets**,
   - type a **hex** value such as `#2563EB`,
   - open **More colors...** for the full color picker, or
   - click **From name** to generate a color from the solution name. The same name always gives the same color.
4. Choose what to color: title bar, icon, taskbar badge and window border can each be turned on or off.
5. Click **OK**.

To go back to the default look, open the dialog again and click **Remove color**.

## Where the color is stored

| Option | Location | Use it for |
|---|---|---|
| Default | `<solution folder>\.vs\<SolutionName>\SolutionColor.json` | Your own preference. It isn't committed because `.vs` is normally git-ignored. |
| **Share with the team** | `<solution folder>\<SolutionName>.slncolor.json` | Commit it so every team member gets the same color. |

If both files exist, your personal setting wins, so you can override a team color locally.

Example file:

```json
{
  "color": "#2563EB",
  "colorTitleBar": true,
  "tintIcon": true,
  "taskbarBadge": true,
  "colorWindowBorder": true
}
```

## Supported versions

- Visual Studio 2026 (18.x)
- Visual Studio 2022 (17.x)
- Community, Professional and Enterprise, on x64 and Arm64

## Known limitations

- Visual Studio doesn't offer an official API for title bar colors, so Solution Color finds the title bar in the window's UI tree. A future Visual Studio update could change that layout. If coloring stops working, check **View > Output > Solution Color** for diagnostics and open an issue.
- When Windows groups Visual Studio under a pinned taskbar icon, the pinned icon itself stays purple. The colored badge is still shown.
- Menu text keeps its theme color. With very dark colors in the Light theme (or very light colors in the Dark theme), menu text can be harder to read, so pick a color with enough contrast.
- The window border color requires Windows 11.

## Feedback

Found a bug or have an idea? Use the **Q & A** tab on this page. When reporting a coloring problem, include your Visual Studio version and the contents of the **Solution Color** Output pane.
