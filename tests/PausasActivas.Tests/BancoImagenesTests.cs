using System.Reflection;
using PausasActivas.Recursos;

namespace PausasActivas.Tests;

public class BancoImagenesTests : IDisposable
{
    private readonly string _directorioTemporal = Path.Combine(
        Path.GetTempPath(),
        "pausas-banco-" + Guid.NewGuid().ToString("N"));

    public BancoImagenesTests()
    {
        Directory.CreateDirectory(_directorioTemporal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorioTemporal))
            Directory.Delete(_directorioTemporal, recursive: true);
    }

    [Fact]
    public void Banco_carga_las_7_nuevas_imagenes_incrustadas_jpg()
    {
        var ensambladoApp = typeof(BancoImagenes).Assembly;
        using var banco = new BancoImagenes(ensambladoApp);

        // Debe haber cargado las 7 imágenes .jpg incrustadas
        Assert.Equal(7, banco.Cantidad);

        // Verificar que cada una de las 7 imágenes es válida y de alta resolución
        for (var i = 1; i <= banco.Cantidad; i++)
        {
            var imagen = banco.Obtener(i);
            Assert.NotNull(imagen);
            Assert.True(imagen.Width > 1500, $"La imagen {i} tiene un ancho menor a 1500px: {imagen.Width}");
            Assert.True(imagen.Height > 1000, $"La imagen {i} tiene un alto menor a 1000px: {imagen.Height}");
        }
    }

    [Fact]
    public void Banco_obtener_con_indice_fuera_de_rango_hace_clamp_seguro()
    {
        var ensambladoApp = typeof(BancoImagenes).Assembly;
        using var banco = new BancoImagenes(ensambladoApp);

        // Índice menor a 1 retorna la primera
        var primera = banco.Obtener(0);
        var primeraEsperada = banco.Obtener(1);
        Assert.Same(primeraEsperada, primera);

        // Índice mayor a Cantidad retorna la última
        var ultima = banco.Obtener(999);
        var ultimaEsperada = banco.Obtener(banco.Cantidad);
        Assert.Same(ultimaEsperada, ultima);
    }

    [Fact]
    public void Banco_carga_imagenes_desde_directorio_en_disco_si_no_hay_incrustadas()
    {
        // Crear 3 imágenes de prueba en disco con distintas extensiones válidas (.png, .jpg)
        var ruta1 = Path.Combine(_directorioTemporal, "01.png");
        var ruta2 = Path.Combine(_directorioTemporal, "02.jpg");

        using (var bmp1 = new Bitmap(200, 200))
        {
            bmp1.Save(ruta1, System.Drawing.Imaging.ImageFormat.Png);
        }
        using (var bmp2 = new Bitmap(300, 300))
        {
            bmp2.Save(ruta2, System.Drawing.Imaging.ImageFormat.Jpeg);
        }

        // Usamos un assembly vacío (el de tests, que no tiene imágenes incrustadas)
        var ensambladoVacio = typeof(BancoImagenesTests).Assembly;
        using var banco = new BancoImagenes(ensambladoVacio, _directorioTemporal);

        Assert.Equal(2, banco.Cantidad);
        Assert.Equal(200, banco.Obtener(1).Width);
        Assert.Equal(300, banco.Obtener(2).Width);
    }

    [Fact]
    public void Banco_sin_imagenes_incrustadas_ni_en_disco_crea_marcador_por_defecto()
    {
        var ensambladoVacio = typeof(BancoImagenesTests).Assembly;
        var carpetaVacia = Path.Combine(_directorioTemporal, "vacia");
        Directory.CreateDirectory(carpetaVacia);

        using var banco = new BancoImagenes(ensambladoVacio, carpetaVacia);

        Assert.Equal(1, banco.Cantidad);
        var marcador = banco.Obtener(1);
        Assert.NotNull(marcador);
        Assert.Equal(1200, marcador.Width);
        Assert.Equal(800, marcador.Height);
    }

    [Fact]
    public void Banco_dispose_vacia_coleccion_de_imagenes()
    {
        var ensambladoApp = typeof(BancoImagenes).Assembly;
        var banco = new BancoImagenes(ensambladoApp);
        Assert.Equal(7, banco.Cantidad);

        banco.Dispose();
        Assert.Equal(0, banco.Cantidad);
    }
}
