using System;
using System.Diagnostics;
using System.IO;

static class StartupLauncher {
    [STAThread]
    static int Main() {
        try {
            Process.Start(new ProcessStartInfo {
                FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BNBU-DDL.exe"),
                Arguments = "--background", UseShellExecute = false
            });
            return 0;
        } catch { return 1; }
    }
}
