using System.Reflection;

namespace PausasActivas.Recursos;

internal sealed class BancoImagenes : IDisposable
{
    private static readonly HashSet<string> ExtensionesValidas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".webp"
    };

    private readonly List<Image> _imagenes = [];

    public BancoImagenes(Assembly? ensamblado = null, string? rutaDirectorio = null)
    {
        ensamblado ??= Assembly.GetExecutingAssembly();

        // 1. Cargar desde recursos incrustados en el ensamblado (.png, .jpg, .jpeg, .bmp, .webp)
        CargarDesdeRecursosIncrustados(ensamblado);

        // 2. Si no hay recursos incrustados, intentar cargar desde directorio en disco
        if (_imagenes.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(rutaDirectorio) && Directory.Exists(rutaDirectorio))
            {
                CargarDesdeDirectorio(rutaDirectorio);
            }
            else
            {
                var rutaRecursos = Path.Combine(AppContext.BaseDirectory, "Recursos", "Imagenes");
                var rutaImagenes = Path.Combine(AppContext.BaseDirectory, "Imagenes");

                if (Directory.Exists(rutaRecursos))
                    CargarDesdeDirectorio(rutaRecursos);
                else if (Directory.Exists(rutaImagenes))
                    CargarDesdeDirectorio(rutaImagenes);
            }
        }

        // 3. Fallback de contingencia si no se encontró ninguna imagen
        if (_imagenes.Count == 0)
            _imagenes.Add(CrearMarcador());
    }

    public int Cantidad => _imagenes.Count;

    public Image Obtener(int indiceUnoBased)
    {
        var indice = Math.Clamp(indiceUnoBased, 1, _imagenes.Count) - 1;
        return _imagenes[indice];
    }

    public void Dispose()
    {
        foreach (var imagen in _imagenes)
        {
            imagen.Dispose();
        }
        _imagenes.Clear();
    }

    private void CargarDesdeRecursosIncrustados(Assembly ensamblado)
    {
        var recursos = ensamblado.GetManifestResourceNames()
            .Where(EsRecursoImagen)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        foreach (var nombre in recursos)
        {
            using var stream = ensamblado.GetManifestResourceStream(nombre);
            if (stream is null)
                continue;

            try
            {
                using var cargada = Image.FromStream(stream);
                _imagenes.Add(new Bitmap(cargada));
            }
            catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.ExternalException)
            {
                System.Diagnostics.Debug.WriteLine($"No se pudo cargar la imagen incrustada {nombre}: {ex.Message}");
            }
        }
    }

    private void CargarDesdeDirectorio(string ruta)
    {
        var archivos = Directory.EnumerateFiles(ruta)
            .Where(f => ExtensionesValidas.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var archivo in archivos)
        {
            try
            {
                using var stream = File.OpenRead(archivo);
                using var cargada = Image.FromStream(stream);
                _imagenes.Add(new Bitmap(cargada));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"No se pudo cargar la imagen desde disco {archivo}: {ex.Message}");
            }
        }
    }

    private static bool EsRecursoImagen(string nombre) =>
        ExtensionesValidas.Contains(Path.GetExtension(nombre));

    private static Bitmap CrearMarcador()
    {
        var imagen = new Bitmap(1200, 800);
        using var lienzo = Graphics.FromImage(imagen);
        lienzo.Clear(Color.FromArgb(232, 240, 234));
        using var titulo = new Font("Segoe UI", 36F, FontStyle.Bold);
        using var detalle = new Font("Segoe UI", 18F);
        using var pincel = new SolidBrush(Color.FromArgb(28, 69, 50));
        lienzo.DrawString("Pausa activa", titulo, pincel, new PointF(80, 280));
        lienzo.DrawString("Estira cuello, hombros y espalda durante 5 minutos.", detalle, pincel, new PointF(80, 380));
        return imagen;
    }
}
