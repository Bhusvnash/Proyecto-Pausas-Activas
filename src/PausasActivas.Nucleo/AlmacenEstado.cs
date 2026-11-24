using System.Text.Json;

namespace PausasActivas.Nucleo;

public sealed class AlmacenEstado
{
    private readonly string _ruta;

    private AlmacenEstado(string ruta, int ultimoIndice)
    {
        _ruta = ruta;
        UltimoIndice = ultimoIndice;
    }

    public int UltimoIndice { get; private set; }

    public static string RutaPorDefecto =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PausasActivas",
            "estado.json");

    public static AlmacenEstado Cargar(string? ruta = null)
    {
        ruta ??= RutaPorDefecto;
        if (!File.Exists(ruta))
            return new AlmacenEstado(ruta, 0);

        try
        {
            using var documento = JsonDocument.Parse(File.ReadAllText(ruta));
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object ||
                !raiz.TryGetProperty("ultimoIndice", out var indice) ||
                !indice.TryGetInt32(out var valor) ||
                valor < 0)
            {
                return new AlmacenEstado(ruta, 0);
            }

            return new AlmacenEstado(ruta, valor);
        }
        catch (JsonException)
        {
            return new AlmacenEstado(ruta, 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new AlmacenEstado(ruta, 0);
        }
    }

    public int PrevisualizarSiguiente(int cantidad)
    {
        if (cantidad < 1 || UltimoIndice < 1)
            return 1;

        return (UltimoIndice % cantidad) + 1;
    }

    public void ConfirmarMostrada(int indice)
    {
        UltimoIndice = indice;
        try
        {
            var directorio = Path.GetDirectoryName(_ruta);
            if (!string.IsNullOrEmpty(directorio))
                Directory.CreateDirectory(directorio);

            var json = JsonSerializer.Serialize(new EstadoDto(UltimoIndice), EstadoDto.Opciones);
            File.WriteAllText(_ruta, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"No se pudo guardar estado.json: {ex.Message}");
        }
    }

    private sealed record EstadoDto(int ultimoIndice)
    {
        public static JsonSerializerOptions Opciones { get; } = new() { WriteIndented = true };
    }
}
