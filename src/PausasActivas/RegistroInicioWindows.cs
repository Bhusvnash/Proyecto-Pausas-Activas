using Microsoft.Win32;

namespace PausasActivas;

internal static class RegistroInicioWindows
{
    private const string Clave = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Nombre = "PausasActivas";

    public static void Asegurar()
    {
        var rutaExe = Environment.ProcessPath ?? Application.ExecutablePath;
        if (string.IsNullOrEmpty(rutaExe))
            return;

        using var clave = Registry.CurrentUser.OpenSubKey(Clave, writable: true)
            ?? throw new IOException("No se encontró la clave Run del usuario.");

        clave.SetValue(Nombre, $"\"{rutaExe}\"");
    }
}
