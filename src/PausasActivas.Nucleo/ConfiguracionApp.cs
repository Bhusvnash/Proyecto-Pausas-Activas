namespace PausasActivas.Nucleo;

public sealed record ConfiguracionApp(
    IReadOnlyList<TimeOnly> HorasPausa,
    IReadOnlyList<DayOfWeek> DiasLaborables);
