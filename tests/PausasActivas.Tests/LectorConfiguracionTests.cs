using PausasActivas.Nucleo;

namespace PausasActivas.Tests;

public class LectorConfiguracionTests : IDisposable
{
    private readonly string _directorio = Path.Combine(Path.GetTempPath(), "pausas-config-" + Guid.NewGuid().ToString("N"));

    public LectorConfiguracionTests() => Directory.CreateDirectory(_directorio);

    public void Dispose() => Directory.Delete(_directorio, recursive: true);

    [Fact]
    public void Lee_un_json_valido()
    {
        var ruta = Escribir("""
            {
              "horasPausa": ["11:30"],
              "diasLaborables": ["Monday", "Wednesday"]
            }
            """);

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.Null(resultado.Advertencia);
        Assert.Equal([new TimeOnly(11, 30)], resultado.Configuracion.HorasPausa);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday], resultado.Configuracion.DiasLaborables);
    }

    [Fact]
    public void Lee_un_json_con_comentarios_y_comas_finales()
    {
        var ruta = Escribir("""
            {
              // Comentario de una línea
              "horasPausa": [
                "10:00", // pausa de la mañana
                "14:00",
              ],
              /* Comentario multilínea
                 de días laborables */
              "diasLaborables": [
                "Monday",
                "Tuesday",
              ],
            }
            """);

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.Null(resultado.Advertencia);
        Assert.Equal([new TimeOnly(10, 0), new TimeOnly(14, 0)], resultado.Configuracion.HorasPausa);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday], resultado.Configuracion.DiasLaborables);
    }

    [Fact]
    public void Si_no_existe_el_archivo_usa_los_valores_por_defecto()
    {
        var resultado = LectorConfiguracion.Leer(Path.Combine(_directorio, "config.json"));

        Assert.NotNull(resultado.Advertencia);
        Assert.Equal(ConstantesPausa.PorDefecto.HorasPausa, resultado.Configuracion.HorasPausa);
        Assert.Equal(ConstantesPausa.PorDefecto.DiasLaborables, resultado.Configuracion.DiasLaborables);
    }

    [Fact]
    public void Una_hora_invalida_explica_la_causa_y_no_lanza()
    {
        var ruta = Escribir("""
            {
              "horasPausa": ["25:99"],
              "diasLaborables": ["Monday"]
            }
            """);

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.Contains("25:99", resultado.Advertencia, StringComparison.Ordinal);
        Assert.Equal(5, resultado.Configuracion.HorasPausa.Count);
    }

    [Fact]
    public void Un_json_roto_no_tumba_la_lectura()
    {
        var ruta = Escribir("{ \"horasPausa\": [");

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.NotNull(resultado.Advertencia);
        Assert.Equal(ConstantesPausa.PorDefecto, resultado.Configuracion);
    }

    [Fact]
    public void Lista_horas_vacia_usa_por_defecto_y_advierte()
    {
        var ruta = Escribir("""
            {
              "horasPausa": [],
              "diasLaborables": ["Monday"]
            }
            """);

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.NotNull(resultado.Advertencia);
        Assert.Contains("vacía", resultado.Advertencia, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConstantesPausa.PorDefecto, resultado.Configuracion);
    }

    [Fact]
    public void Lista_dias_vacia_usa_por_defecto_y_advierte()
    {
        var ruta = Escribir("""
            {
              "horasPausa": ["10:00"],
              "diasLaborables": []
            }
            """);

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.NotNull(resultado.Advertencia);
        Assert.Contains("vacía", resultado.Advertencia, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConstantesPausa.PorDefecto, resultado.Configuracion);
    }

    [Fact]
    public void Dia_invalido_explica_la_causa_y_usa_por_defecto()
    {
        var ruta = Escribir("""
            {
              "horasPausa": ["10:00"],
              "diasLaborables": ["DomingoInvalido"]
            }
            """);

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.NotNull(resultado.Advertencia);
        Assert.Contains("DomingoInvalido", resultado.Advertencia, StringComparison.Ordinal);
        Assert.Equal(ConstantesPausa.PorDefecto, resultado.Configuracion);
    }

    [Fact]
    public void Falta_propiedad_horas_usa_por_defecto()
    {
        var ruta = Escribir("""
            {
              "diasLaborables": ["Monday"]
            }
            """);

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.NotNull(resultado.Advertencia);
        Assert.Contains("horasPausa", resultado.Advertencia, StringComparison.Ordinal);
        Assert.Equal(ConstantesPausa.PorDefecto, resultado.Configuracion);
    }

    [Fact]
    public void Falta_propiedad_dias_usa_por_defecto()
    {
        var ruta = Escribir("""
            {
              "horasPausa": ["09:00"]
            }
            """);

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.NotNull(resultado.Advertencia);
        Assert.Contains("diasLaborables", resultado.Advertencia, StringComparison.Ordinal);
        Assert.Equal(ConstantesPausa.PorDefecto, resultado.Configuracion);
    }

    [Fact]
    public void Raiz_no_objeto_usa_por_defecto()
    {
        var ruta = Escribir("[\"no\", \"un\", \"objeto\"]");

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.NotNull(resultado.Advertencia);
        Assert.Contains("objeto", resultado.Advertencia, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConstantesPausa.PorDefecto, resultado.Configuracion);
    }

    [Fact]
    public void Elimina_duplicados_en_horas_y_dias()
    {
        var ruta = Escribir("""
            {
              "horasPausa": ["10:00", "10:00", "14:00"],
              "diasLaborables": ["Monday", "monday", "Tuesday"]
            }
            """);

        var resultado = LectorConfiguracion.Leer(ruta);

        Assert.Null(resultado.Advertencia);
        Assert.Equal([new TimeOnly(10, 0), new TimeOnly(14, 0)], resultado.Configuracion.HorasPausa);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Tuesday], resultado.Configuracion.DiasLaborables);
    }

    private string Escribir(string contenido)
    {
        var ruta = Path.Combine(_directorio, "config.json");
        File.WriteAllText(ruta, contenido);
        return ruta;
    }
}
