namespace PausasActivas.Nucleo;

public static class ConstantesPausa
{
		public const int DuracionSegundos = 15;   // era 5 * 60
		public const int PosponerMinutos = 1;   // era 10
		public const int MaxPosposiciones = 2;
		public static readonly TimeSpan Tolerancia = TimeSpan.FromSeconds(60);

		public static ConfiguracionApp PorDefecto { get; } = new(
				[
						new TimeOnly(8, 0),
						new TimeOnly(10, 0),
						new TimeOnly(12, 0),
						new TimeOnly(14, 0),
						new TimeOnly(16, 0)
				],
				[
						DayOfWeek.Monday,
						DayOfWeek.Tuesday,
						DayOfWeek.Wednesday,
						DayOfWeek.Thursday,
						DayOfWeek.Friday
				]);
}