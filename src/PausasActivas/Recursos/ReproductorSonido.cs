using System.Media;
using System.Reflection;

namespace PausasActivas.Recursos;

internal interface IReproductorSonido
{
    void Reproducir();
}

internal sealed class ReproductorSonido : IReproductorSonido
{
    private readonly byte[]? _wav;

    public ReproductorSonido()
    {
        var ensamblado = Assembly.GetExecutingAssembly();
        var nombre = ensamblado.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("fin.wav", StringComparison.OrdinalIgnoreCase));
        if (nombre is null)
            return;

        using var stream = ensamblado.GetManifestResourceStream(nombre);
        if (stream is null)
            return;

        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        _wav = memoria.ToArray();
    }

    public void Reproducir()
    {
        if (_wav is null)
        {
            SystemSounds.Asterisk.Play();
            return;
        }

        var bytes = _wav;
        Task.Run(() =>
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reproductor = new SoundPlayer(stream);
            reproductor.PlaySync();
        });
    }
}
