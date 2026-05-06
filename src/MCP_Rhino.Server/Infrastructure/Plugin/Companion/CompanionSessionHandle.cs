using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MCP_Rhino.Server.Infrastructure.Plugin.Companion;

internal sealed class CompanionSessionHandle : IDisposable
{
    private const int RestoreWindow = 9;
    private readonly Process _process;

    public CompanionSessionHandle(CompanionLaunchSpec spec, Process process)
    {
        Spec = spec;
        _process = process;
    }

    public CompanionLaunchSpec Spec { get; }

    public bool IsRunning => !_process.HasExited;

    public void Focus()
    {
        try
        {
            if (_process.HasExited)
            {
                return;
            }

            _process.Refresh();
            IntPtr handle = _process.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                _process.WaitForInputIdle(1000);
                _process.Refresh();
                handle = _process.MainWindowHandle;
            }

            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, RestoreWindow);
                SetForegroundWindow(handle);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.CloseMainWindow();
                if (!_process.WaitForExit(1000))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _process.Dispose();
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
