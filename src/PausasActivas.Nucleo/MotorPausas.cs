namespace PausasActivas.Nucleo;

public enum TipoDecision
{
    Ninguna,
    MostrarNueva,
    Reabrir,
    Reemplazar
}

public readonly record struct DecisionPausa(
    TipoDecision Tipo,
    int IndiceImagen,
    int PosposicionesRestantes,
    bool ConsumirIndice)
{
    public static DecisionPausa Ninguna { get; } = new(TipoDecision.Ninguna, 0, 0, false);
}

public sealed class MotorPausas
{
    private DateOnly? _dia;
    private readonly HashSet<TimeOnly> _atendidas = [];
    private bool _esperandoReapertura;
    private DateTime _reaperturaEn;
    private int _posposicionesUsadas;
    private int _indiceEnCurso;
    private bool _ventanaAbierta;

    public DecisionPausa Tick(
        DateTime ahora,
        bool sesionActiva,
        bool diaLaborable,
        IReadOnlyList<TimeOnly> horas,
        int indiceNuevo)
    {
        var hoy = DateOnly.FromDateTime(ahora);
        if (_dia != hoy)
        {
            _dia = hoy;
            _atendidas.Clear();
            _esperandoReapertura = false;
            _ventanaAbierta = false;
        }

        if (diaLaborable)
        {
            foreach (var hora in horas.OrderBy(h => h))
            {
                if (_atendidas.Contains(hora) || !EnVentana(ahora, hora))
                    continue;

                _atendidas.Add(hora);
                if (!sesionActiva)
                    continue;

                var reemplazo = _ventanaAbierta || _esperandoReapertura;
                _esperandoReapertura = false;
                _posposicionesUsadas = 0;
                _indiceEnCurso = indiceNuevo;
                _ventanaAbierta = true;
                return new DecisionPausa(
                    reemplazo ? TipoDecision.Reemplazar : TipoDecision.MostrarNueva,
                    indiceNuevo,
                    ConstantesPausa.MaxPosposiciones,
                    ConsumirIndice: true);
            }
        }

        if (_esperandoReapertura)
        {
            if (ahora >= _reaperturaEn)
            {
                _esperandoReapertura = false;
                if (!sesionActiva || ahora > _reaperturaEn + ConstantesPausa.Tolerancia)
                {
                    _ventanaAbierta = false;
                    return DecisionPausa.Ninguna;
                }

                _ventanaAbierta = true;
                return new DecisionPausa(
                    TipoDecision.Reabrir,
                    _indiceEnCurso,
                    ConstantesPausa.MaxPosposiciones - _posposicionesUsadas,
                    ConsumirIndice: false);
            }
        }

        return DecisionPausa.Ninguna;
    }

    public bool Posponer(DateTime ahora)
    {
        if (!_ventanaAbierta || _posposicionesUsadas >= ConstantesPausa.MaxPosposiciones)
            return false;

        _posposicionesUsadas++;
        _ventanaAbierta = false;
        _esperandoReapertura = true;
        _reaperturaEn = ahora.AddMinutes(ConstantesPausa.PosponerMinutos);
        return true;
    }

    public void Saltar()
    {
        _ventanaAbierta = false;
        _esperandoReapertura = false;
    }

    public void Terminar()
    {
        _ventanaAbierta = false;
        _esperandoReapertura = false;
    }

    private static bool EnVentana(DateTime ahora, TimeOnly hora)
    {
        var actual = TimeOnly.FromDateTime(ahora);
        var fin = hora.Add(ConstantesPausa.Tolerancia);
        if (fin > hora)
            return actual >= hora && actual < fin;

        return actual >= hora || actual < fin;
    }
}
