using PausasActivas.Nucleo;

namespace PausasActivas.Tests;

public class AlmacenEstadoTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), "pausas-estado-" + Guid.NewGuid().ToString("N") + ".json");

    public void Dispose()
    {
        if (File.Exists(_ruta))
            File.Delete(_ruta);
    }

    [Fact]
    public void Sin_archivo_empieza_en_la_imagen_1()
    {
        var estado = AlmacenEstado.Cargar(_ruta);

        Assert.Equal(1, estado.PrevisualizarSiguiente(10));
    }

    [Fact]
    public void El_indice_3_muestra_despues_la_imagen_4_y_al_final_vuelve_a_la_1()
    {
        File.WriteAllText(_ruta, """{ "ultimoIndice": 3 }""");
        var estado = AlmacenEstado.Cargar(_ruta);

        Assert.Equal(4, estado.PrevisualizarSiguiente(10));
        Assert.Equal(1, estado.PrevisualizarSiguiente(3));
    }

    [Fact]
    public void Un_archivo_corrupto_vuelve_a_empezar()
    {
        File.WriteAllText(_ruta, "{ esto no es json");

        var estado = AlmacenEstado.Cargar(_ruta);

        Assert.Equal(0, estado.UltimoIndice);
        Assert.Equal(1, estado.PrevisualizarSiguiente(5));
    }

    [Fact]
    public void Valor_negativo_en_archivo_se_trata_como_corrupto()
    {
        File.WriteAllText(_ruta, """{ "ultimoIndice": -5 }""");

        var estado = AlmacenEstado.Cargar(_ruta);

        Assert.Equal(0, estado.UltimoIndice);
        Assert.Equal(1, estado.PrevisualizarSiguiente(5));
    }

    [Fact]
    public void Previsualizar_con_cantidad_cero_o_menor_retorna_1()
    {
        File.WriteAllText(_ruta, """{ "ultimoIndice": 2 }""");

        var estado = AlmacenEstado.Cargar(_ruta);

        Assert.Equal(1, estado.PrevisualizarSiguiente(0));
        Assert.Equal(1, estado.PrevisualizarSiguiente(-1));
    }

    [Fact]
    public void Confirmar_guarda_el_indice_mostrado()
    {
        var estado = AlmacenEstado.Cargar(_ruta);
        estado.ConfirmarMostrada(4);

        var recargado = AlmacenEstado.Cargar(_ruta);
        Assert.Equal(4, recargado.UltimoIndice);
        Assert.Equal(5, recargado.PrevisualizarSiguiente(10));
    }

    [Fact]
    public void Confirmar_crea_directorio_si_no_existe()
    {
        var subdirectorio = Path.Combine(Path.GetTempPath(), "pausas-subdir-" + Guid.NewGuid().ToString("N"));
        var ruta = Path.Combine(subdirectorio, "estado.json");
        try
        {
            var estado = AlmacenEstado.Cargar(ruta);
            estado.ConfirmarMostrada(2);

            Assert.True(File.Exists(ruta));
            var recargado = AlmacenEstado.Cargar(ruta);
            Assert.Equal(2, recargado.UltimoIndice);
        }
        finally
        {
            if (Directory.Exists(subdirectorio))
                Directory.Delete(subdirectorio, recursive: true);
        }
    }
}
