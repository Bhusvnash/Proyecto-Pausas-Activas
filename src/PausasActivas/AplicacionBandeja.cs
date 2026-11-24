using PausasActivas.Nucleo;
using PausasActivas.Presentacion;
using PausasActivas.Recursos;

namespace PausasActivas;

internal sealed class AplicacionBandeja : ApplicationContext
{
    private readonly ConfiguracionApp _config;
    private readonly AlmacenEstado _estado;
    private readonly MotorPausas _motor;
    private readonly BancoImagenes _banco;
    private readonly IPresentadorPausa _presentador;
    private readonly NotifyIcon _icono;
    private readonly System.Windows.Forms.Timer _timer;

    public AplicacionBandeja(
        ConfiguracionApp config,
        AlmacenEstado estado,
        IPresentadorPausa? presentador = null,
        MotorPausas? motor = null,
        BancoImagenes? banco = null)
    {
        _config = config;
        _estado = estado;
        _motor = motor ?? new MotorPausas();
        _banco = banco ?? new BancoImagenes();
        _presentador = presentador ?? new VentanaPausa(new ReproductorSonido());

        _presentador.PosponerSolicitado += (_, _) =>
        {
            if (_motor.Posponer(DateTime.Now))
                _presentador.Cerrar();
        };
        _presentador.SaltarSolicitado += (_, _) =>
        {
            _motor.Saltar();
            _presentador.Cerrar();
        };
        _presentador.TermineSolicitado += (_, _) =>
        {
            _motor.Terminar();
            _presentador.Cerrar();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Salir", null, (_, _) => Salir());

        _icono = new NotifyIcon
        {
            Icon = (Icon)SystemIcons.Application.Clone(),
            Visible = true,
            Text = "Pausas Activas",
            ContextMenuStrip = menu
        };

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => Evaluar();
        _timer.Start();
    }

    private void Evaluar()
    {
        try
        {
            var ahora = DateTime.Now;
            var laborable = _config.DiasLaborables.Contains(ahora.DayOfWeek);
            var indice = _estado.PrevisualizarSiguiente(_banco.Cantidad);
            var decision = _motor.Tick(
                ahora,
                DetectorSesion.EstaDesbloqueada(),
                laborable,
                _config.HorasPausa,
                indice);

            if (decision.Tipo == TipoDecision.Ninguna)
                return;

            if (decision.ConsumirIndice)
                _estado.ConfirmarMostrada(decision.IndiceImagen);

            _presentador.Mostrar(_banco.Obtener(decision.IndiceImagen), decision.PosposicionesRestantes);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    private void Salir()
    {
        _timer.Stop();
        _presentador.Cerrar();
        (_presentador as IDisposable)?.Dispose();
        (_banco as IDisposable)?.Dispose();
        _icono.Visible = false;
        _icono.Dispose();
        ExitThread();
    }
}
