using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using SolutionEvents = Microsoft.VisualStudio.Shell.Events.SolutionEvents;
using Task = System.Threading.Tasks.Task;

namespace SolutionColor
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Solution Color", "Per-solution title bar and icon color for Visual Studio.", "1.0.0")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.FolderOpened_string, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid(PackageGuids.PackageString)]
    public sealed class SolutionColorPackage : AsyncPackage
    {
        private IVsSolution _solution;
        private Logger _log;
        private WindowPainter _painter;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            var solution = await GetServiceAsync(typeof(SVsSolution));
            var outputWindow = await GetServiceAsync(typeof(SVsOutputWindow));
            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _solution = solution as IVsSolution;

            _log = new Logger(outputWindow as IVsOutputWindow);
            _painter = new WindowPainter(Application.Current.MainWindow, _log);

            if (commandService != null)
            {
                var id = new CommandID(PackageGuids.CommandSet, PackageIds.SetSolutionColor);
                var command = new OleMenuCommand((s, e) => ShowColorDialog(), id);
                command.BeforeQueryStatus += (s, e) =>
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    command.Visible = command.Enabled = GetStore() != null;
                };
                commandService.AddCommand(command);
            }

            SolutionEvents.OnAfterOpenSolution += (s, e) => ApplyForCurrentSolution();
            SolutionEvents.OnAfterOpenFolder += (s, e) => ApplyForCurrentSolution();
            SolutionEvents.OnAfterRenameSolution += (s, e) => ApplyForCurrentSolution();
            SolutionEvents.OnAfterCloseSolution += (s, e) => ClearColor();
            SolutionEvents.OnAfterCloseFolder += (s, e) => ClearColor();

            // We are auto-loaded after a solution is already open, so apply right away.
            ApplyForCurrentSolution();
        }

        private SolutionColorStore GetStore()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_solution == null)
                return null;

            if (ErrorHandler.Failed(_solution.GetSolutionInfo(out string directory, out string file, out _)) ||
                string.IsNullOrEmpty(directory))
            {
                return null;
            }

            return new SolutionColorStore(directory, file);
        }

        private void ApplyForCurrentSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var store = GetStore();
                var settings = store?.Load();
                if (store != null)
                    _log.Write(settings == null ? $"'{store.SolutionName}' has no color." : $"Applying {settings.Color} for '{store.SolutionName}'.");

                _painter.Apply(settings);
            }
            catch (Exception ex)
            {
                _log.Write("Failed to apply solution color: " + ex);
            }
        }

        private void ClearColor()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                _painter.Clear();
            }
            catch (Exception ex)
            {
                _log.Write("Failed to clear solution color: " + ex);
            }
        }

        private void ShowColorDialog()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var store = GetStore();
            if (store == null)
                return;

            var current = store.Load();
            var dialog = new ColorPickerDialog(store.SolutionName, current, Application.Current.MainWindow.Icon, preview =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _painter.Apply(preview);
            });

            dialog.ShowModal();

            try
            {
                switch (dialog.Result)
                {
                    case ColorDialogResult.Save:
                        var settings = dialog.Settings;
                        store.Save(settings);
                        _log.Write($"Saved {settings.Color} to {(settings.Shared ? store.SharedFilePath : store.UserFilePath)}");
                        _painter.Apply(settings);
                        break;

                    case ColorDialogResult.Remove:
                        store.Delete();
                        _log.Write($"Removed color for '{store.SolutionName}'.");
                        _painter.Clear();
                        break;

                    default:
                        _painter.Apply(current); // undo the live preview
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Write("Failed to save solution color: " + ex);
                VsShellUtilities.ShowMessageBox(this, ex.Message, "Solution Color",
                    OLEMSGICON.OLEMSGICON_CRITICAL, OLEMSGBUTTON.OLEMSGBUTTON_OK, OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
    }
}
