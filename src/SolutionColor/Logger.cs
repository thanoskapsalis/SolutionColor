using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SolutionColor
{
    /// <summary>Writes diagnostics to a "Solution Color" pane in the Output window.</summary>
    internal sealed class Logger
    {
        private static readonly Guid PaneGuid = new Guid("df9d38d1-9662-4794-bae8-c96f57436069");
        private readonly IVsOutputWindow _outputWindow;
        private IVsOutputWindowPane _pane;

        public Logger(IVsOutputWindow outputWindow)
        {
            _outputWindow = outputWindow;
        }

        public void Write(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_outputWindow == null)
                return;

            if (_pane == null)
            {
                var guid = PaneGuid;
                _outputWindow.CreatePane(ref guid, "Solution Color", fInitVisible: 1, fClearWithSolution: 0);
                _outputWindow.GetPane(ref guid, out _pane);
            }

            _pane?.OutputStringThreadSafe($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}
