using PausasActivas.Nucleo;

namespace PausasActivas;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instancia = InstanciaUnica.IntentarAdquirir();
        if (instancia is null)
            return;

        ApplicationConfiguration.Initialize();

        var ruta = Path.Combine(AppContext.BaseDirectory, "config.json");
        var resultado = LectorConfiguracion.Leer(ruta);
        if (resultado.Advertencia is not null)
        {
            MessageBox.Show(
                resultado.Advertencia,
                "Pausas Activas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        try
        {
            RegistroInicioWindows.Asegurar();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(
                "No se pudo registrar el inicio con Windows: " + ex.Message,
                "Pausas Activas",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        Application.Run(new AplicacionBandeja(resultado.Configuracion, AlmacenEstado.Cargar()));
    }
}
