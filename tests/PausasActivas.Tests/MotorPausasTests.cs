using PausasActivas.Nucleo;

namespace PausasActivas.Tests;

public class MotorPausasTests
{
    private static readonly IReadOnlyList<TimeOnly> Horas =
    [
        new TimeOnly(8, 0),
        new TimeOnly(10, 0),
        new TimeOnly(12, 0),
        new TimeOnly(14, 0),
        new TimeOnly(16, 0)
    ];

    private readonly MotorPausas _motor = new();

    [Fact]
    public void Dispara_dentro_de_los_60_segundos_y_solo_una_vez()
    {
        var primera = _motor.Tick(new DateTime(2026, 11, 17, 10, 0, 20), true, true, Horas, 4);
        var segunda = _motor.Tick(new DateTime(2026, 11, 17, 10, 0, 50), true, true, Horas, 5);

        Assert.Equal(TipoDecision.MostrarNueva, primera.Tipo);
        Assert.Equal(4, primera.IndiceImagen);
        Assert.True(primera.ConsumirIndice);
        Assert.Equal(2, primera.PosposicionesRestantes);
        Assert.Equal(TipoDecision.Ninguna, segunda.Tipo);
    }

    [Fact]
    public void No_dispara_cuando_ya_pasaron_60_segundos()
    {
        var decision = _motor.Tick(new DateTime(2026, 11, 17, 8, 5, 0), true, true, Horas, 1);

        Assert.Equal(TipoDecision.Ninguna, decision.Tipo);
    }

    [Fact]
    public void No_dispara_en_un_dia_no_laborable()
    {
        var decision = _motor.Tick(new DateTime(2026, 11, 21, 10, 0, 0), true, false, Horas, 1);

        Assert.Equal(TipoDecision.Ninguna, decision.Tipo);
    }

    [Fact]
    public void Si_la_sesion_esta_bloqueada_la_pausa_se_pierde()
    {
        var bloqueada = _motor.Tick(new DateTime(2026, 11, 17, 12, 0, 10), false, true, Horas, 2);
        var despues = _motor.Tick(new DateTime(2026, 11, 17, 12, 0, 40), true, true, Horas, 2);

        Assert.Equal(TipoDecision.Ninguna, bloqueada.Tipo);
        Assert.Equal(TipoDecision.Ninguna, despues.Tipo);
    }

    [Fact]
    public void Posponer_reabre_la_misma_imagen_a_los_10_minutos_sin_avanzar_el_indice()
    {
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);
        _motor.Tick(ahora, true, true, Horas, 3);
        Assert.True(_motor.Posponer(ahora));

        var antes = _motor.Tick(ahora.AddMinutes(9).AddSeconds(59), true, true, Horas, 99);
        var reapertura = _motor.Tick(ahora.AddMinutes(10), true, true, Horas, 99);

        Assert.Equal(TipoDecision.Ninguna, antes.Tipo);
        Assert.Equal(TipoDecision.Reabrir, reapertura.Tipo);
        Assert.Equal(3, reapertura.IndiceImagen);
        Assert.False(reapertura.ConsumirIndice);
        Assert.Equal(1, reapertura.PosposicionesRestantes);
    }

    [Fact]
    public void Solo_permite_dos_posposiciones()
    {
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);
        _motor.Tick(ahora, true, true, Horas, 1);
        Assert.True(_motor.Posponer(ahora));
        _motor.Tick(ahora.AddMinutes(10), true, true, Horas, 1);

        Assert.True(_motor.Posponer(ahora.AddMinutes(10)));
        var terceraVentana = _motor.Tick(ahora.AddMinutes(20), true, true, Horas, 1);

        Assert.Equal(0, terceraVentana.PosposicionesRestantes);
        Assert.False(_motor.Posponer(ahora.AddMinutes(20)));
    }

    [Fact]
    public void Si_al_reabrir_la_sesion_esta_bloqueada_la_pausa_se_pierde()
    {
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);
        _motor.Tick(ahora, true, true, Horas, 1);
        _motor.Posponer(ahora);

        var decision = _motor.Tick(ahora.AddMinutes(10), false, true, Horas, 8);

        Assert.Equal(TipoDecision.Ninguna, decision.Tipo);
        Assert.Equal(TipoDecision.Ninguna, _motor.Tick(ahora.AddMinutes(11), true, true, Horas, 8).Tipo);
    }

    [Fact]
    public void Si_al_reabrir_se_pasa_de_la_tolerancia_la_pausa_se_descarta()
    {
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);
        _motor.Tick(ahora, true, true, Horas, 1);
        _motor.Posponer(ahora);

        // Simulamos que el PC estuvo suspendido o bloqueado y despierta a los 15 minutos (5 min después de la reapertura)
        var decisionTardia = _motor.Tick(ahora.AddMinutes(15), true, true, Horas, 8);

        Assert.Equal(TipoDecision.Ninguna, decisionTardia.Tipo);

        // Ya descartada, en el siguiente tick tampoco debe aparecer
        Assert.Equal(TipoDecision.Ninguna, _motor.Tick(ahora.AddMinutes(16), true, true, Horas, 8).Tipo);
    }

    [Fact]
    public void Una_pausa_nueva_reemplaza_a_la_que_sigue_abierta()
    {
        var horas = new[] { new TimeOnly(10, 0), new TimeOnly(10, 1) };
        _motor.Tick(new DateTime(2026, 11, 17, 10, 0, 0), true, true, horas, 2);

        var decision = _motor.Tick(new DateTime(2026, 11, 17, 10, 1, 0), true, true, horas, 3);

        Assert.Equal(TipoDecision.Reemplazar, decision.Tipo);
        Assert.Equal(3, decision.IndiceImagen);
        Assert.True(decision.ConsumirIndice);
        Assert.Equal(2, decision.PosposicionesRestantes);
    }

    [Fact]
    public void Pausa_nueva_cancela_posposicion_en_espera_y_muestra_la_nueva()
    {
        var horas = new[] { new TimeOnly(10, 0), new TimeOnly(10, 5) };
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);
        _motor.Tick(ahora, true, true, horas, 2);
        _motor.Posponer(ahora); // Esperando hasta las 10:10

        // A las 10:05 llega una nueva pausa programada
        var nuevaPausa = _motor.Tick(ahora.AddMinutes(5), true, true, horas, 3);

        Assert.Equal(TipoDecision.Reemplazar, nuevaPausa.Tipo);
        Assert.Equal(3, nuevaPausa.IndiceImagen);
        Assert.Equal(2, nuevaPausa.PosposicionesRestantes);

        // A las 10:10 ya no debe reabrir la anterior
        var despues = _motor.Tick(ahora.AddMinutes(10), true, true, horas, 99);
        Assert.Equal(TipoDecision.Ninguna, despues.Tipo);
    }

    [Fact]
    public void Saltar_no_reabre_la_pausa()
    {
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);
        _motor.Tick(ahora, true, true, Horas, 1);
        _motor.Saltar();

        var decision = _motor.Tick(ahora.AddMinutes(30), true, true, Horas, 2);

        Assert.Equal(TipoDecision.Ninguna, decision.Tipo);
    }

    [Fact]
    public void Terminar_cierra_el_ciclo_y_no_permite_posponer()
    {
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);
        _motor.Tick(ahora, true, true, Horas, 1);
        _motor.Terminar();

        Assert.False(_motor.Posponer(ahora));
        Assert.Equal(TipoDecision.Ninguna, _motor.Tick(ahora.AddMinutes(10), true, true, Horas, 2).Tipo);
    }

    [Fact]
    public void Posponer_cuando_la_ventana_no_esta_abierta_retorna_false()
    {
        Assert.False(_motor.Posponer(DateTime.Now));
    }

    [Fact]
    public void Cambio_de_dia_reinicia_horas_atendidas_y_cancela_posposicion_pendiente()
    {
        var hoy = new DateTime(2026, 11, 17, 16, 0, 0);
        _motor.Tick(hoy, true, true, Horas, 1);
        _motor.Posponer(hoy);

        // Pasa al día siguiente a las 08:00
        var manana = new DateTime(2026, 11, 18, 8, 0, 10);
        var decision = _motor.Tick(manana, true, true, Horas, 2);

        Assert.Equal(TipoDecision.MostrarNueva, decision.Tipo);
        Assert.Equal(2, decision.IndiceImagen);
    }
}
