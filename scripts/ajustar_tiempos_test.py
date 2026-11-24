#!/usr/bin/env python3
"""
Script utilitario para ajustar los horarios de config.json en base a la hora actual.
Permite a un Agente o evaluador configurar las horas relativas para ejecutar la batería de pruebas.

Uso:
    python scripts/ajustar_tiempos_test.py
    python scripts/ajustar_tiempos_test.py --offset 3   # Iniciar 3 minutos después de la hora actual
    python scripts/ajustar_tiempos_test.py --hora 10:25 # Fijar hora inicial específica
"""

import argparse
from datetime import datetime, timedelta
from pathlib import Path

# Offsets relativos recomendados en minutos desde la hora base T0
OFFSETS_MINUTOS = [
    (0, "Prueba 1: Aparición a tiempo + Esperar 15s -> Sonido y botón Terminé"),
    (2, "Prueba 2: Posponer x2 (reabre a T0+3m y T0+4m; 3a vez deshabilitado) + Alt+F4"),
    (6, "Prueba 3a: Reemplazo - Abrir y pulsar Posponer (espera reapertura a T0+7m)"),
    (8, "Prueba 3b: Reemplazo - Nueva hora programada entra y reemplaza la pospuesta con imagen nueva"),
    (11, "Prueba 4: Sesión bloqueada - Win+L antes de hora y desbloqueo posterior (>60s) -> Se descarta"),
    (14, "Prueba 5: Bloqueo en tolerancia - Win+L antes y desbloqueo a los 20s -> Se descarta igual"),
    (17, "Prueba 6: Día no laborable - Para probar, quitar el día actual de diasLaborables")
]

DIAS_SEMANA = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]

def generar_config_json(t0: datetime) -> str:
    lineas_horas = []
    for offset, descripcion in OFFSETS_MINUTOS:
        hora_obj = t0 + timedelta(minutes=offset)
        hora_str = hora_obj.strftime("%H:%M")
        lineas_horas.append(f'    "{hora_str}", // +{offset:02d}m: {descripcion}')

    # Eliminar coma final para compatibilidad estricta
    lineas_horas[-1] = lineas_horas[-1].replace('", //', '"  //')

    horas_bloque = "\n".join(lineas_horas)
    dias_bloque = ",\n    ".join([f'"{d}"' for d in DIAS_SEMANA])

    return f"""{{
  // ==============================================================================
  // CONFIGURACIÓN DINÁMICA DE PRUEBAS — Generada el {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
  // Hora base T0: {t0.strftime('%H:%M')} | Duración pausa: 15s | Posponer: 1m
  // ==============================================================================
  "horasPausa": [
{horas_bloque}
  ],
  "diasLaborables": [
    {dias_bloque}
  ]
}}
"""

def main():
    parser = argparse.ArgumentParser(description="Ajusta los horarios de prueba de config.json.")
    parser.add_argument("--offset", type=int, default=2, help="Minutos a sumar a la hora actual para T0 (por defecto: 2)")
    parser.add_argument("--hora", type=str, default=None, help="Hora inicial T0 en formato HH:mm (ej: 10:25)")
    args = parser.parse_args()

    ahora = datetime.now()
    if args.hora:
        h, m = map(int, args.hora.split(":"))
        t0 = ahora.replace(hour=h, minute=m, second=0, microsecond=0)
    else:
        t0 = ahora + timedelta(minutes=args.offset)

    contenido = generar_config_json(t0)

    base_dir = Path(__file__).resolve().parent.parent
    rutas = [
        base_dir / "src" / "PausasActivas" / "config.json",
        base_dir / "src" / "PausasActivas" / "bin" / "Debug" / "net10.0-windows" / "config.json"
    ]

    for ruta in rutas:
        if ruta.parent.exists():
            ruta.write_text(contenido, encoding="utf-8")
            print(f"[OK] Archivo actualizado: {ruta}")

    print("\n--- Horario de Pruebas Configurado ---")
    for offset, descripcion in OFFSETS_MINUTOS:
        hora_str = (t0 + timedelta(minutes=offset)).strftime("%H:%M")
        print(f"[{hora_str}] (+{offset:02d}m) -> {descripcion}")

if __name__ == "__main__":
    main()
