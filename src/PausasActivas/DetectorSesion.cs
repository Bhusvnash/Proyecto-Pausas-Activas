using System.Runtime.InteropServices;

namespace PausasActivas;

internal static class DetectorSesion
{
    private const uint DesktopSwitchDesktop = 0x0100;

    public static bool EstaDesbloqueada()
    {
        var escritorio = OpenInputDesktop(0, false, DesktopSwitchDesktop);
        if (escritorio == IntPtr.Zero)
            return false;

        CloseDesktop(escritorio);
        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr hDesktop);
}
