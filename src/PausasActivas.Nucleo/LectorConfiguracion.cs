using System.Globalization;
using System.Text.Json;

namespace PausasActivas.Nucleo;

public sealed record ResultadoConfiguracion(ConfiguracionApp Configuracion, string? Advertencia);

public static class LectorConfiguracion
{
    private static readonly HashSet<string> NombresDia = new(Enum.GetNames<DayOfWeek>(), StringComparer.OrdinalIgnoreCase);
    private static readonly string[] FormatosHora = ["HH:mm", "H:mm"];

    public static ResultadoConfiguracion Leer(string ruta)
    {
        if (!File.Exists(ruta))
        {
            return Fallo("No se encontró config.json junto al ejecutable.");
        }

        try
        {
            using var stream = File.OpenRead(ruta);
            using var documento = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
                return Fallo("config.json debe ser un objeto.");

            if (!raiz.TryGetProperty("horasPausa", out var horas))
                return Fallo("Falta la propiedad horasPausa.");
            if (!raiz.TryGetProperty("diasLaborables", out var dias))
                return Fallo("Falta la propiedad diasLaborables.");

            var configuracion = new ConfiguracionApp(LeerHoras(horas), LeerDias(dias));
            return new ResultadoConfiguracion(configuracion, null);
        }
        catch (FormatoConfigException ex)
        {
            return Fallo(ex.Message);
        }
        catch (JsonException ex)
        {
            return Fallo($"config.json no es JSON válido: {ex.Message}");
        }
        catch (IOException ex)
        {
            return Fallo($"No se pudo leer config.json: {ex.Message}");
        }
    }

    private static ResultadoConfiguracion Fallo(string causa) =>
        new(ConstantesPausa.PorDefecto, causa + " Se usarán los valores por defecto.");

    private static List<TimeOnly> LeerHoras(JsonElement elemento)
    {
        if (elemento.ValueKind != JsonValueKind.Array)
            throw new FormatoConfigException("horasPausa debe ser una lista.");

        var horas = new List<TimeOnly>();
        var vistas = new HashSet<TimeOnly>();
        foreach (var item in elemento.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new FormatoConfigException("Cada hora de horasPausa debe ser un texto HH:mm.");

            var texto = item.GetString() ?? "";
            if (!TimeOnly.TryParseExact(texto, FormatosHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
                throw new FormatoConfigException($"La hora \"{texto}\" no tiene formato HH:mm.");

            if (vistas.Add(hora))
                horas.Add(hora);
        }

        if (horas.Count == 0)
            throw new FormatoConfigException("horasPausa está vacía.");

        return horas;
    }

    private static List<DayOfWeek> LeerDias(JsonElement elemento)
    {
        if (elemento.ValueKind != JsonValueKind.Array)
            throw new FormatoConfigException("diasLaborables debe ser una lista.");

        var dias = new List<DayOfWeek>();
        var vistos = new HashSet<DayOfWeek>();
        foreach (var item in elemento.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new FormatoConfigException("Cada día de diasLaborables debe ser un texto.");

            var texto = item.GetString() ?? "";
            if (!NombresDia.Contains(texto) || !Enum.TryParse(texto, ignoreCase: true, out DayOfWeek dia))
                throw new FormatoConfigException($"El día \"{texto}\" no es un DayOfWeek válido. Usa el nombre en inglés, por ejemplo \"Monday\".");

            if (vistos.Add(dia))
                dias.Add(dia);
        }

        if (dias.Count == 0)
            throw new FormatoConfigException("diasLaborables está vacía.");

        return dias;
    }

    private sealed class FormatoConfigException(string mensaje) : Exception(mensaje);
}
