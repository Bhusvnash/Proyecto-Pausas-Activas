using PausasActivas.Nucleo;

namespace PausasActivas.Tests;

/// <summary>
/// Tests de integración end-to-end (E2E) que simulan jornadas completas
/// integrando LectorConfiguracion, AlmacenEstado y MotorPausas.
/// </summary>
public class E2EIntegracionTests : IDisposable
{
    private static readonly IReadOnlyList<TimeOnly> HorasDefecto =
    [
        new TimeOnly(8, 0),
        new TimeOnly(10, 0),
        new TimeOnly(12, 0),
        new TimeOnly(14, 0),
        new TimeOnly(16, 0)
    ];

    private readonly string _directorio = Path.Combine(
        Path.GetTempPath(),
        "pausas-e2e-" + Guid.NewGuid().ToString("N"));

    private readonly string _rutaConfig;
    private readonly string _rutaEstado;

    public E2EIntegracionTests()
    {
        Directory.CreateDirectory(_directorio);
        _rutaConfig = Path.Combine(_directorio, "config.json");
        _rutaEstado = Path.Combine(_directorio, "estado.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directorio))
            Directory.Delete(_directorio, recursive: true);
    }

    // ──────────────────────────────────────────────
    // Grupo 1: Simulación de Jornada Completa
    // ──────────────────────────────────────────────

    [Fact]
    public void Jornada_completa_cinco_pausas_en_dia_laborable()
    {
        // Lunes 17/nov/2026
        var motor = new MotorPausas();
        var estado = AlmacenEstado.Cargar(_rutaEstado);
        var cantidadImagenes = 10;
        var pausasMostradas = new List<DecisionPausa>();

        foreach (var hora in HorasDefecto)
        {
            var ahora = new DateTime(2026, 11, 17, hora.Hour, hora.Minute, 5);
            var indice = estado.PrevisualizarSiguiente(cantidadImagenes);
            var decision = motor.Tick(ahora, sesionActiva: true, diaLaborable: true, HorasDefecto, indice);

            Assert.Equal(TipoDecision.MostrarNueva, decision.Tipo);
            Assert.True(decision.ConsumirIndice);
            pausasMostradas.Add(decision);

            if (decision.ConsumirIndice)
                estado.ConfirmarMostrada(decision.IndiceImagen);

            motor.Terminar();
        }

        Assert.Equal(5, pausasMostradas.Count);
        // Imágenes: 1, 2, 3, 4, 5 (secuencial desde estado fresco)
        Assert.Equal(1, pausasMostradas[0].IndiceImagen);
        Assert.Equal(2, pausasMostradas[1].IndiceImagen);
        Assert.Equal(3, pausasMostradas[2].IndiceImagen);
        Assert.Equal(4, pausasMostradas[3].IndiceImagen);
        Assert.Equal(5, pausasMostradas[4].IndiceImagen);

        // El estado persiste correctamente
        var recargado = AlmacenEstado.Cargar(_rutaEstado);
        Assert.Equal(5, recargado.UltimoIndice);
        Assert.Equal(6, recargado.PrevisualizarSiguiente(cantidadImagenes));
    }

    [Fact]
    public void Jornada_sin_pausas_en_fin_de_semana()
    {
        // Sábado 21/nov/2026
        var motor = new MotorPausas();

        foreach (var hora in HorasDefecto)
        {
            var ahora = new DateTime(2026, 11, 21, hora.Hour, hora.Minute, 5);
            var decision = motor.Tick(ahora, sesionActiva: true, diaLaborable: false, HorasDefecto, 1);

            Assert.Equal(TipoDecision.Ninguna, decision.Tipo);
        }
    }

    [Fact]
    public void Inicio_sesion_tarde_pierde_pausas_anteriores()
    {
        // Usuario inicia a las 10:05 — se pierden 08:00 y 10:00
        var motor = new MotorPausas();

        // 08:00 ya pasó (estamos a las 10:05, fuera de ventana de 08:00)
        var d1 = motor.Tick(new DateTime(2026, 11, 17, 10, 5, 0), true, true, HorasDefecto, 1);
        Assert.Equal(TipoDecision.Ninguna, d1.Tipo);

        // 10:00 también pasó (10:05 > 10:00+60s = 10:01)
        // Ya se evaluó en el tick anterior — las dos pasaron

        // 12:00 sí dispara
        var d12 = motor.Tick(new DateTime(2026, 11, 17, 12, 0, 10), true, true, HorasDefecto, 1);
        Assert.Equal(TipoDecision.MostrarNueva, d12.Tipo);
    }

    // ──────────────────────────────────────────────
    // Grupo 2: Cadenas Posponer/Saltar/Terminar
    // ──────────────────────────────────────────────

    [Fact]
    public void Posponer_dos_veces_luego_terminar_completa_ciclo()
    {
        var motor = new MotorPausas();
        var estado = AlmacenEstado.Cargar(_rutaEstado);
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);

        // Pausa inicial
        var indice = estado.PrevisualizarSiguiente(5);
        var primera = motor.Tick(ahora, true, true, HorasDefecto, indice);
        Assert.Equal(TipoDecision.MostrarNueva, primera.Tipo);
        Assert.Equal(1, primera.IndiceImagen);
        Assert.Equal(2, primera.PosposicionesRestantes);
        estado.ConfirmarMostrada(primera.IndiceImagen);

        // Posponer 1
        Assert.True(motor.Posponer(ahora));
        var reabrir1 = motor.Tick(ahora.AddMinutes(10), true, true, HorasDefecto, 99);
        Assert.Equal(TipoDecision.Reabrir, reabrir1.Tipo);
        Assert.Equal(1, reabrir1.IndiceImagen); // misma imagen
        Assert.False(reabrir1.ConsumirIndice);
        Assert.Equal(1, reabrir1.PosposicionesRestantes);

        // Posponer 2
        Assert.True(motor.Posponer(ahora.AddMinutes(10)));
        var reabrir2 = motor.Tick(ahora.AddMinutes(20), true, true, HorasDefecto, 99);
        Assert.Equal(TipoDecision.Reabrir, reabrir2.Tipo);
        Assert.Equal(1, reabrir2.IndiceImagen); // misma imagen
        Assert.Equal(0, reabrir2.PosposicionesRestantes);

        // No se puede posponer más
        Assert.False(motor.Posponer(ahora.AddMinutes(20)));

        // Terminar
        motor.Terminar();

        // Verificar que la siguiente pausa es a las 12:00
        var d12 = motor.Tick(new DateTime(2026, 11, 17, 12, 0, 5), true, true, HorasDefecto,
            estado.PrevisualizarSiguiente(5));
        Assert.Equal(TipoDecision.MostrarNueva, d12.Tipo);
        Assert.Equal(2, d12.IndiceImagen); // avanza a imagen 2
    }

    [Fact]
    public void Posponer_saltar_terminar_secuencia_mixta()
    {
        var motor = new MotorPausas();
        var estado = AlmacenEstado.Cargar(_rutaEstado);
        var cantImg = 5;

        // Pausa 1 (10:00): posponer una vez, luego saltar
        var ahora1 = new DateTime(2026, 11, 17, 10, 0, 0);
        var idx1 = estado.PrevisualizarSiguiente(cantImg);
        var d1 = motor.Tick(ahora1, true, true, HorasDefecto, idx1);
        Assert.Equal(TipoDecision.MostrarNueva, d1.Tipo);
        estado.ConfirmarMostrada(d1.IndiceImagen); // imagen 1 consumida

        motor.Posponer(ahora1);
        motor.Tick(ahora1.AddMinutes(10), true, true, HorasDefecto, 99); // reabre
        motor.Saltar(); // saltar después de posponer

        // Pausa 2 (12:00): completar normalmente
        var ahora2 = new DateTime(2026, 11, 17, 12, 0, 5);
        var idx2 = estado.PrevisualizarSiguiente(cantImg);
        Assert.Equal(2, idx2); // imagen 2 (la 1 ya se consumió)
        var d2 = motor.Tick(ahora2, true, true, HorasDefecto, idx2);
        Assert.Equal(TipoDecision.MostrarNueva, d2.Tipo);
        estado.ConfirmarMostrada(d2.IndiceImagen);
        motor.Terminar();

        Assert.Equal(2, estado.UltimoIndice);
    }

    [Fact]
    public void Saltar_pausa_no_avanza_siguiente_hasta_proxima_hora()
    {
        var motor = new MotorPausas();
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);

        motor.Tick(ahora, true, true, HorasDefecto, 1);
        motor.Saltar();

        // Nada hasta las 12:00
        var d1 = motor.Tick(ahora.AddMinutes(30), true, true, HorasDefecto, 2);
        Assert.Equal(TipoDecision.Ninguna, d1.Tipo);

        var d2 = motor.Tick(new DateTime(2026, 11, 17, 11, 0, 0), true, true, HorasDefecto, 2);
        Assert.Equal(TipoDecision.Ninguna, d2.Tipo);

        var d3 = motor.Tick(new DateTime(2026, 11, 17, 12, 0, 5), true, true, HorasDefecto, 2);
        Assert.Equal(TipoDecision.MostrarNueva, d3.Tipo);
    }

    // ──────────────────────────────────────────────
    // Grupo 3: Configuración → Motor → Estado
    // ──────────────────────────────────────────────

    [Fact]
    public void Configuracion_personalizada_afecta_horario_del_motor()
    {
        File.WriteAllText(_rutaConfig, """
            {
              "horasPausa": ["09:30", "15:00"],
              "diasLaborables": ["Monday", "Wednesday", "Friday"]
            }
            """);

        var resultado = LectorConfiguracion.Leer(_rutaConfig);
        Assert.Null(resultado.Advertencia);

        var motor = new MotorPausas();

        // 09:30 dispara
        var d1 = motor.Tick(new DateTime(2026, 11, 18, 9, 30, 5), true, true,
            resultado.Configuracion.HorasPausa, 1);
        Assert.Equal(TipoDecision.MostrarNueva, d1.Tipo);
        motor.Terminar();

        // 10:00 NO dispara (no está en config personalizado)
        var d2 = motor.Tick(new DateTime(2026, 11, 18, 10, 0, 5), true, true,
            resultado.Configuracion.HorasPausa, 2);
        Assert.Equal(TipoDecision.Ninguna, d2.Tipo);

        // 15:00 dispara
        var d3 = motor.Tick(new DateTime(2026, 11, 18, 15, 0, 5), true, true,
            resultado.Configuracion.HorasPausa, 2);
        Assert.Equal(TipoDecision.MostrarNueva, d3.Tipo);
    }

    [Fact]
    public void Config_invalido_usa_valores_por_defecto_que_funcionan_en_motor()
    {
        File.WriteAllText(_rutaConfig, "{ esto no es JSON válido");

        var resultado = LectorConfiguracion.Leer(_rutaConfig);
        Assert.NotNull(resultado.Advertencia);
        Assert.Equal(ConstantesPausa.PorDefecto.HorasPausa, resultado.Configuracion.HorasPausa);

        // Los defaults funcionan en el motor
        var motor = new MotorPausas();
        var d1 = motor.Tick(new DateTime(2026, 11, 17, 8, 0, 10), true, true,
            resultado.Configuracion.HorasPausa, 1);
        Assert.Equal(TipoDecision.MostrarNueva, d1.Tipo);
    }

    [Fact]
    public void Estado_persiste_entre_reinicios_simulados()
    {
        // "Primera ejecución": mostrar imágenes 1, 2, 3
        var estado1 = AlmacenEstado.Cargar(_rutaEstado);
        for (var i = 1; i <= 3; i++)
        {
            var idx = estado1.PrevisualizarSiguiente(10);
            Assert.Equal(i, idx);
            estado1.ConfirmarMostrada(idx);
        }

        // "Reinicio" — nueva instancia de AlmacenEstado
        var estado2 = AlmacenEstado.Cargar(_rutaEstado);
        Assert.Equal(3, estado2.UltimoIndice);

        var siguiente = estado2.PrevisualizarSiguiente(10);
        Assert.Equal(4, siguiente);

        // Usar en motor
        var motor = new MotorPausas();
        var decision = motor.Tick(new DateTime(2026, 11, 17, 10, 0, 5), true, true, HorasDefecto, siguiente);
        Assert.Equal(TipoDecision.MostrarNueva, decision.Tipo);
        Assert.Equal(4, decision.IndiceImagen);
    }

    // ──────────────────────────────────────────────
    // Grupo 4: Casos Límite
    // ──────────────────────────────────────────────

    [Fact]
    public void Bloqueo_durante_posposicion_pierde_la_pausa()
    {
        var motor = new MotorPausas();
        var ahora = new DateTime(2026, 11, 17, 10, 0, 0);

        // Mostrar pausa y posponer
        motor.Tick(ahora, true, true, HorasDefecto, 1);
        motor.Posponer(ahora);

        // A los 10 min, sesión bloqueada
        var dBloqueada = motor.Tick(ahora.AddMinutes(10), false, true, HorasDefecto, 8);
        Assert.Equal(TipoDecision.Ninguna, dBloqueada.Tipo);

        // Desbloquea después — la pausa ya se perdió
        var dDespues = motor.Tick(ahora.AddMinutes(11), true, true, HorasDefecto, 8);
        Assert.Equal(TipoDecision.Ninguna, dDespues.Tipo);

        // La siguiente pausa programada (12:00) sí funciona
        var d12 = motor.Tick(new DateTime(2026, 11, 17, 12, 0, 5), true, true, HorasDefecto, 2);
        Assert.Equal(TipoDecision.MostrarNueva, d12.Tipo);
    }

    [Fact]
    public void Cambio_de_dia_reinicia_todo_y_nueva_jornada_funciona()
    {
        var motor = new MotorPausas();
        var estado = AlmacenEstado.Cargar(_rutaEstado);

        // Día 1: atender 08:00 y 10:00
        var dia1_08 = new DateTime(2026, 11, 17, 8, 0, 5);
        var idx1 = estado.PrevisualizarSiguiente(5);
        motor.Tick(dia1_08, true, true, HorasDefecto, idx1);
        estado.ConfirmarMostrada(idx1);
        motor.Terminar();

        var dia1_10 = new DateTime(2026, 11, 17, 10, 0, 5);
        var idx2 = estado.PrevisualizarSiguiente(5);
        motor.Tick(dia1_10, true, true, HorasDefecto, idx2);
        estado.ConfirmarMostrada(idx2);
        motor.Terminar();

        // Día 2: las 5 pausas están disponibles de nuevo
        var dia2_08 = new DateTime(2026, 11, 18, 8, 0, 5);
        var idx3 = estado.PrevisualizarSiguiente(5);
        Assert.Equal(3, idx3); // El estado de imágenes SÍ persiste entre días
        var d = motor.Tick(dia2_08, true, true, HorasDefecto, idx3);
        Assert.Equal(TipoDecision.MostrarNueva, d.Tipo);
        Assert.Equal(3, d.IndiceImagen);
    }

    [Fact]
    public void Pausa_reemplaza_anterior_si_sigue_abierta()
    {
        var horas = new TimeOnly[] { new(10, 0), new(10, 0, 30) };
        var motor = new MotorPausas();
        var estado = AlmacenEstado.Cargar(_rutaEstado);

        // Primera pausa a las 10:00
        var idx1 = estado.PrevisualizarSiguiente(5);
        var d1 = motor.Tick(new DateTime(2026, 11, 17, 10, 0, 5), true, true, horas, idx1);
        Assert.Equal(TipoDecision.MostrarNueva, d1.Tipo);
        estado.ConfirmarMostrada(d1.IndiceImagen);

        // Segunda pausa a las 10:00:30 — usuario no interactuó
        var idx2 = estado.PrevisualizarSiguiente(5);
        var d2 = motor.Tick(new DateTime(2026, 11, 17, 10, 0, 35), true, true, horas, idx2);
        Assert.Equal(TipoDecision.Reemplazar, d2.Tipo);
        Assert.Equal(idx2, d2.IndiceImagen);
        Assert.True(d2.ConsumirIndice);
    }

    [Fact]
    public void Tolerancia_exacta_de_60_segundos_dispara_a_los_59_no_a_los_61()
    {
        var motor1 = new MotorPausas();
        var motor2 = new MotorPausas();

        // 59 segundos después: DEBE disparar
        var dOk = motor1.Tick(new DateTime(2026, 11, 17, 10, 0, 59), true, true, HorasDefecto, 1);
        Assert.Equal(TipoDecision.MostrarNueva, dOk.Tipo);

        // 60 segundos después: NO debe disparar (ventana es [10:00:00, 10:01:00))
        var dNo = motor2.Tick(new DateTime(2026, 11, 17, 10, 1, 0), true, true, HorasDefecto, 1);
        Assert.Equal(TipoDecision.Ninguna, dNo.Tipo);
    }

    [Fact]
    public void Estado_wrap_around_vuelve_a_imagen_1()
    {
        var estado = AlmacenEstado.Cargar(_rutaEstado);
        var banco = 3;

        // Mostrar las 3 imágenes
        for (var i = 1; i <= banco; i++)
        {
            var idx = estado.PrevisualizarSiguiente(banco);
            Assert.Equal(i, idx);
            estado.ConfirmarMostrada(idx);
        }

        // La siguiente vuelve a 1
        var siguiente = estado.PrevisualizarSiguiente(banco);
        Assert.Equal(1, siguiente);

        // Recargado persiste el wrap
        var recargado = AlmacenEstado.Cargar(_rutaEstado);
        Assert.Equal(1, recargado.PrevisualizarSiguiente(banco));
    }

    // ──────────────────────────────────────────────
    // Grupo 5: Stress y Robustez
    // ──────────────────────────────────────────────

    [Fact]
    public void Muchos_ticks_sin_pausas_no_cambian_estado()
    {
        var motor = new MotorPausas();
        var estado = AlmacenEstado.Cargar(_rutaEstado);

        // 1000 ticks fuera de horas de pausa
        for (var i = 0; i < 1000; i++)
        {
            var ahora = new DateTime(2026, 11, 17, 7, 0, 0).AddSeconds(i);
            var d = motor.Tick(ahora, true, true, HorasDefecto, 1);
            Assert.Equal(TipoDecision.Ninguna, d.Tipo);
        }

        Assert.Equal(0, estado.UltimoIndice);
    }

    [Fact]
    public void Config_y_estado_archivos_corruptos_simultaneos()
    {
        File.WriteAllText(_rutaConfig, "{ basura total sin json");
        File.WriteAllText(_rutaEstado, "más basura aquí también");

        var resultado = LectorConfiguracion.Leer(_rutaConfig);
        Assert.NotNull(resultado.Advertencia);
        Assert.Equal(ConstantesPausa.PorDefecto, resultado.Configuracion);

        var estado = AlmacenEstado.Cargar(_rutaEstado);
        Assert.Equal(0, estado.UltimoIndice);
        Assert.Equal(1, estado.PrevisualizarSiguiente(10));

        // A pesar de ambos archivos corruptos, el sistema funciona
        var motor = new MotorPausas();
        var idx = estado.PrevisualizarSiguiente(10);
        var d = motor.Tick(new DateTime(2026, 11, 17, 10, 0, 5), true, true,
            resultado.Configuracion.HorasPausa, idx);
        Assert.Equal(TipoDecision.MostrarNueva, d.Tipo);
        Assert.Equal(1, d.IndiceImagen);
    }

    [Fact]
    public void Dos_posposiciones_mas_bloqueo_pierde_pausa_y_continua_siguiente()
    {
        var motor = new MotorPausas();
        var ahora = new DateTime(2026, 11, 17, 14, 0, 0);

        // Mostrar y posponer 1
        motor.Tick(ahora, true, true, HorasDefecto, 3);
        motor.Posponer(ahora);

        // Reabre a los 10 min, posponer 2
        motor.Tick(ahora.AddMinutes(10), true, true, HorasDefecto, 99);
        motor.Posponer(ahora.AddMinutes(10));

        // A los 20 min la sesión está bloqueada
        var dBloq = motor.Tick(ahora.AddMinutes(20), false, true, HorasDefecto, 99);
        Assert.Equal(TipoDecision.Ninguna, dBloq.Tipo);

        // Pausa de las 16:00 funciona normalmente
        var d16 = motor.Tick(new DateTime(2026, 11, 17, 16, 0, 5), true, true, HorasDefecto, 4);
        Assert.Equal(TipoDecision.MostrarNueva, d16.Tipo);
    }

    [Fact]
    public void Jornada_completa_con_mix_de_acciones_avanza_indices_correctamente()
    {
        var motor = new MotorPausas();
        var estado = AlmacenEstado.Cargar(_rutaEstado);
        var banco = 5;

        // 08:00 — Terminar → consume imagen 1
        var idx1 = estado.PrevisualizarSiguiente(banco);
        var d08 = motor.Tick(new DateTime(2026, 11, 17, 8, 0, 5), true, true, HorasDefecto, idx1);
        Assert.Equal(1, d08.IndiceImagen);
        estado.ConfirmarMostrada(d08.IndiceImagen);
        motor.Terminar();

        // 10:00 — Saltar → consume imagen 2 (la pausa se muestra, luego se salta)
        var idx2 = estado.PrevisualizarSiguiente(banco);
        var d10 = motor.Tick(new DateTime(2026, 11, 17, 10, 0, 5), true, true, HorasDefecto, idx2);
        Assert.Equal(2, d10.IndiceImagen);
        estado.ConfirmarMostrada(d10.IndiceImagen);
        motor.Saltar();

        // 12:00 — Posponer + Terminar → consume imagen 3 (solo se consume al mostrar por primera vez)
        var idx3 = estado.PrevisualizarSiguiente(banco);
        var d12 = motor.Tick(new DateTime(2026, 11, 17, 12, 0, 5), true, true, HorasDefecto, idx3);
        Assert.Equal(3, d12.IndiceImagen);
        estado.ConfirmarMostrada(d12.IndiceImagen);
        motor.Posponer(new DateTime(2026, 11, 17, 12, 0, 5));

        var d12r = motor.Tick(new DateTime(2026, 11, 17, 12, 10, 5), true, true, HorasDefecto, 99);
        Assert.Equal(TipoDecision.Reabrir, d12r.Tipo);
        Assert.Equal(3, d12r.IndiceImagen); // misma imagen
        Assert.False(d12r.ConsumirIndice);  // NO consume
        motor.Terminar();

        // 14:00 — imagen 4
        var idx4 = estado.PrevisualizarSiguiente(banco);
        Assert.Equal(4, idx4);

        // 16:00 (tras 14:00) — imagen 5
        var d14 = motor.Tick(new DateTime(2026, 11, 17, 14, 0, 5), true, true, HorasDefecto, idx4);
        estado.ConfirmarMostrada(d14.IndiceImagen);
        motor.Terminar();

        var idx5 = estado.PrevisualizarSiguiente(banco);
        Assert.Equal(5, idx5);

        // Siguiente sería imagen 1 (wrap)
        var d16 = motor.Tick(new DateTime(2026, 11, 17, 16, 0, 5), true, true, HorasDefecto, idx5);
        estado.ConfirmarMostrada(d16.IndiceImagen);
        motor.Terminar();

        var idxWrap = estado.PrevisualizarSiguiente(banco);
        Assert.Equal(1, idxWrap); // vuelve a 1
    }
}
