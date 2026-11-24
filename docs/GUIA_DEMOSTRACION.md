# Guía de Demostración — App de Pausas Activas

**Versión:** 1.0 · **Fecha:** 2026-11-20  
**Entorno:** .NET 10 · Windows Forms · Windows 11 · 1 monitor

---

## ¿Qué es esta app?

**Pausas Activas** es una aplicación de escritorio que vive silenciosamente en la bandeja del sistema. A horas fijas de la jornada laboral, abre una ventana con una imagen de ejercicios de estiramiento y un contador regresivo. Su objetivo es recordar al usuario que debe hacer una pausa activa.

**Características clave:**
- 🕐 **Horario configurable** vía `config.json` (sin recompilar)
- 🖼️ **Banco de imágenes** incrustado en el ejecutable, mostradas en secuencia
- 🔒 **Respeta el estado de sesión**: si el PC está bloqueado en la hora de la pausa, esta se descarta
- ⏸️ **Posponer hasta 2 veces** (10 min en producción, 1 min en modo test)
- ⏭️ **Saltar** o **Alt+F4** para omitir una pausa
- 🔔 **Sonido** al finalizar el contador + botón "Terminé"
- 🚀 **Inicio automático** con Windows (se registra en `HKCU\...\Run`)

---

## Modos de operación

| Modo | Duración pausa | Tiempo posponer | Cuándo usarlo |
|---|---|---|---|
| **Producción** | 5 minutos | 10 minutos | Uso real del día a día |
| **Demo / Test** | **15 segundos** | **1 minuto** | Demostrar todos los escenarios en ~18 min |

> [!IMPORTANT]
> Para la demostración, la app corre en **modo test** (15 s / 1 min). Estos valores están definidos en `ConstantesPausa.cs` y el horario se ajusta en `config.json`.

---

## Archivos clave que modificar

### 1. `src/PausasActivas/config.json` — **Horario de pausas**

Es el archivo que la app lee al arrancar. Para la demo, contiene horarios cercanos a la hora actual:

```jsonc
{
  "horasPausa": [
    "HH:MM", // Prueba 1 — disparo normal
    "HH:MM", // Prueba 2 — posponer x2 + Alt+F4
    "HH:MM", // Prueba 3a — reemplazo (posponer)
    "HH:MM", // Prueba 3b — reemplazo (nueva pausa entra)
    "HH:MM", // Prueba 4 — sesión bloqueada > 60 s
    "HH:MM", // Prueba 5 — bloqueo dentro de tolerancia (20 s)
    "HH:MM"  // Prueba 6 — día no laborable
  ],
  "diasLaborables": [
    "Monday", "Tuesday", "Wednesday", "Thursday", "Friday"
  ]
}
```

> **TIP:** Usa el script `scripts/ajustar_tiempos_test.py` para generar automáticamente las horas relativas a la hora actual (ver sección siguiente).

---

### 2. `src/PausasActivas.Nucleo/ConstantesPausa.cs` — **Duración y límites**

Contiene las constantes del motor de pausas. **Requiere recompilar tras cada cambio.**

```csharp
public static class ConstantesPausa
{
    public const int DuracionSegundos = 15;       // ← DEMO: 15 s   | Producción: 5 * 60
    public const int PosponerMinutos  = 1;        // ← DEMO: 1 min  | Producción: 10
    public const int MaxPosposiciones = 2;        // ← Siempre 2
    public static readonly TimeSpan Tolerancia = TimeSpan.FromSeconds(60);
}
```

| Constante | Valor demo | Valor producción | Efecto |
|---|---|---|---|
| `DuracionSegundos` | `15` | `300` (5 min) | Duración del contador regresivo |
| `PosponerMinutos` | `1` | `10` | Tiempo hasta que reabre tras posponer |
| `MaxPosposiciones` | `2` | `2` | Veces que se puede posponer por pausa |
| `Tolerancia` | `60 s` | `60 s` | Margen para considerar la pausa "a tiempo" |

---

## Pasos para preparar la demostración

### Paso 1 — Verificar que `ConstantesPausa.cs` está en modo demo

Abre `src/PausasActivas.Nucleo/ConstantesPausa.cs` y confirma:

```csharp
public const int DuracionSegundos = 15;   // debe ser 15, NO 300
public const int PosponerMinutos  = 1;    // debe ser 1, NO 10
```

Si los valores son los de producción, cámbialos y recompila:

```pwsh
dotnet build "PausasActivas.slnx"
```

---

### Paso 2 — Generar el `config.json` con horarios automáticos

Desde la raíz del proyecto:

```pwsh
python scripts/ajustar_tiempos_test.py --offset 2
```

Esto genera un `config.json` donde la **Prueba 1 empieza en 2 minutos** desde ahora.

**Opciones del script:**

| Argumento | Ejemplo | Efecto |
|---|---|---|
| `--offset N` | `--offset 2` | T0 = ahora + N minutos (por defecto: 2) |
| `--hora HH:MM` | `--hora 14:30` | Fija T0 en la hora especificada |

**Offsets de cada prueba desde T0:**

| Prueba | Offset | Hora relativa |
|---|---|---|
| P1 — Aparición normal | +0 min | T0 |
| P2 — Posponer x2 + Alt+F4 | +2 min | T0 + 2 |
| P3a — Reemplazo: posponer | +6 min | T0 + 6 |
| P3b — Reemplazo: nueva entra | +8 min | T0 + 8 |
| P4 — Bloqueo > 60 s | +11 min | T0 + 11 |
| P5 — Bloqueo ≤ 60 s (20 s) | +14 min | T0 + 14 |
| P6 — Día no laborable | +17 min | T0 + 17 |

---

### Paso 3 — Lanzar la app

```pwsh
Stop-Process -Name "PausasActivas" -Force -ErrorAction SilentlyContinue
Start-Process "src\PausasActivas\bin\Debug\net10.0-windows\PausasActivas.exe"
```

Verifica que aparece el icono en la bandeja del sistema.

---

## Protocolo de demostración (6 pruebas, ~18 minutos)

### P1 — Aparición puntual + completar pausa ✅

**Hora:** T0 · **Acción:** Esperar sin interactuar.

| Qué ocurre | Verificar |
|---|---|
| A T0 (± 60 s) aparece la ventana centrada, al frente | ✔ Visible sobre otras apps |
| Contador inicia en `00:15` y baja cada segundo | ✔ Sin foco en ningún botón al abrir |
| Al llegar a `00:00` suena `fin.wav` | ✔ Se oye el sonido |
| Aparece el botón **Terminé** (Posponer y Saltar se ocultan) | ✔ Solo se ve "Terminé" |
| Pulsar **Terminé** → ventana se cierra | ✔ No reabre |
| `%LOCALAPPDATA%\PausasActivas\estado.json` guarda el índice de imagen | ✔ Índice actualizado |

---

### P2 — Posponer (máx. 2) + Alt+F4 ⏸️

**Hora:** T0 + 2 min · **Acción:** Usar Posponer dos veces, luego Alt+F4.

| Paso | Acción | Resultado esperado |
|---|---|---|
| 1 | Abre la ventana → pulsar **Posponer (2)** | Ventana cierra |
| 2 | Esperar 1 min → ventana reabre | **Misma imagen**, 15 s, botón dice **Posponer (1)** |
| 3 | Pulsar **Posponer (1)** | Ventana cierra |
| 4 | Esperar 1 min → ventana reabre | Botón **Posponer (0)** está **deshabilitado** |
| 5 | Pulsar **Alt+F4** | Ventana cierra como "Saltar" — app sigue en bandeja |

> **Nota:** La imagen en las reaperturas debe ser la **misma** que en la primera apertura (el índice no avanza al posponer).

---

### P3 — Reemplazo por pausa nueva 🔄

**Horas:** T0 + 6 min (P3a) y T0 + 8 min (P3b)

| Paso | Hora | Acción | Resultado esperado |
|---|---|---|---|
| 1 | T0+6 | Aparece ventana imagen N → pulsar **Posponer** | Cierra; reapertura esperada en T0+7 |
| 2 | T0+7 | No hacer nada | — |
| 3 | T0+8 | Llega la nueva pausa programada | Ventana anterior descartada; **imagen N+1** con contador en 15s y **Posponer (2)** habilitado |
| 4 | — | Pulsar **Saltar** | Ventana cierra |

---

### P4 — Sesión bloqueada (fuera de tolerancia) 🔒

**Hora:** T0 + 11 min  
**Acción:** Bloquear con `Win + L` **antes** de T0+11; desbloquear **más de 60 s después**.

| Qué ocurre | Verificar |
|---|---|
| Al desbloquear (> T0+12:00) no aparece ninguna ventana | ✔ Pausa descartada silenciosamente |

---

### P5 — Bloqueo dentro de la tolerancia (20 s) ⚡

**Hora:** T0 + 14 min  
**Acción:** Bloquear con `Win + L` unos segundos antes de T0+14; desbloquear a los **~20 s** de T0+14.

| Qué ocurre | Verificar |
|---|---|
| Al desbloquear (T0+14:20) **no aparece ventana** | ✔ La pausa ya fue marcada en el tick de bloqueo |

> **Nota:** Aunque se desbloqueó dentro de la ventana de tolerancia, el motor ya evaluó el tick con sesión bloqueada y marcó la hora como atendida. No hay recuperación retroactiva.

---

### P6 — Día no laborable 📅

**Hora:** T0 + 17 min  
**Preparación (antes de esta prueba):**

1. Abrir `src/PausasActivas/config.json` y quitar el día actual de `diasLaborables`:
   ```json
   "diasLaborables": ["Monday", "Tuesday", "Wednesday", "Thursday"]
   ```
   *(Si hoy es viernes, elimina `"Friday"`)*

2. Reiniciar la app: clic derecho en bandeja → **Salir** → volver a lanzar.

| Qué ocurre | Verificar |
|---|---|
| A T0+17 no aparece ninguna ventana | ✔ App respeta días laborables del JSON |

---

## Restaurar a producción

### `src/PausasActivas.Nucleo/ConstantesPausa.cs`

```csharp
public const int DuracionSegundos = 5 * 60;  // 300 segundos = 5 minutos
public const int PosponerMinutos  = 10;       // 10 minutos
```

### `src/PausasActivas/config.json`

```json
{
  "horasPausa": ["08:00", "10:00", "12:00", "14:00", "16:00"],
  "diasLaborables": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
}
```

Luego recompila y reinicia:

```pwsh
dotnet build "PausasActivas.slnx"
Stop-Process -Name "PausasActivas" -Force -ErrorAction SilentlyContinue
Start-Process "src\PausasActivas\bin\Debug\net10.0-windows\PausasActivas.exe"
```

---

## Resumen de archivos y valores

| Archivo | Qué cambia | Valor demo | Valor producción | Requiere recompilar |
|---|---|---|---|---|
| `src/PausasActivas.Nucleo/ConstantesPausa.cs` | `DuracionSegundos` | `15` | `300` | ✅ Sí |
| `src/PausasActivas.Nucleo/ConstantesPausa.cs` | `PosponerMinutos` | `1` | `10` | ✅ Sí |
| `src/PausasActivas/config.json` | `horasPausa` | Horas cercanas (script) | `08:00` … `16:00` | ❌ No (reiniciar app) |
| `src/PausasActivas/config.json` | `diasLaborables` | Todos menos uno (P6) | Lun–Vie | ❌ No (reiniciar app) |

> **CAUCIÓN:** Después de modificar `ConstantesPausa.cs` siempre se requiere **recompilar**. El `config.json` es el único que se puede cambiar en caliente, pero requiere **reiniciar la app** para que surta efecto.
