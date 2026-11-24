# Requisitos Funcionales y Plan de Pruebas para Agente / Usuario

**Proyecto:** App de Pausas Activas  
**Versión:** 1.0 — Guía de Validación Funcional y Ejecución de Pruebas  
**Fecha:** 2026-11-20  
**Entorno de prueba:** .NET 10 · Windows 11 · 1 Monitor · Duración pausa = 15 s · Posponer = 1 min · Tolerancia = 60 s  

---

## 1. Objetivo y Resumen

Este documento define la especificación de requisitos de prueba (**RF-TEST**) y el protocolo paso a paso para que un **agente automatizado** o un **usuario evaluador** valide todos los comportamientos críticos del ciclo de vida de la aplicación de Pausas Activas.

Los tiempos temporales han sido ajustados para permitir una ejecución continua en una ventana de ~18 minutos, sin necesidad de esperar horas reales.

---

## 2. Horarios y Matriz de Pruebas

### Tiempos ajustados para la sesión de prueba

| Caso / Prueba | Hora base de prueba | Acción esperada | Resultado esperado | Criterio de Éxito |
|---|---|---|---|---|
| **P1. Que salga la pausa + Terminé** | `10:34` (T0) | Esperar la hora exacta con sesión activa. | A las 10:34 abre ventana centrada con imagen y contador en 15s. Al llegar a 0s suena `fin.wav` y aparece el botón **Terminé**. Pulsar "Terminé". | La ventana cierra, no reabre y guarda el índice en `estado.json`. |
| **P2. Posponer (máx. 2) y Alt+F4** | `10:36` (T0+2m) | A las 10:36 abre pausa. Pulsar **Posponer (2)**. Esperar 1m (`10:37`), reabre y pulsar **Posponer (1)**. Esperar 1m (`10:38`), reabre. Probar **Alt+F4**. | Reabre en 10:37 con la **misma imagen**. En 10:38 reabre con botón `Posponer (0)` deshabilitado. Al pulsar `Alt+F4`, la ventana cierra inmediatamente como si fuera "Saltar". | Respeta límite de 2 posposiciones y atajo Alt+F4. |
| **P3. Reemplazo de pausa** | `10:40` (T0+6m) y `10:42` (T0+8m) | A las 10:40 abre imagen N. Pulsar **Posponer** (reapertura esperada a las 10:41). Esperar hasta las 10:42 sin cerrar. | A las 10:42 llega la nueva pausa programada: la ventana anterior se descarta/reemplaza y aparece la **imagen nueva** (N+1) con contador reiniciado a 15s. Pulsar "Saltar". | La pausa programada más reciente tiene prioridad y sustituye a la anterior pendiente. |
| **P4. Sesión bloqueada fuera de tolerancia** | `10:45` (T0+11m) | Bloquear la sesión con `Win + L` a las `10:44:30`. Desbloquear a las `10:46:30` (>60 s de tolerancia). | Al desbloquear, **no aparece ninguna ventana**. La pausa de las 10:45 se descartó por estar la máquina bloqueada. | Pausa perdida confirmada. |
| **P5. Bloqueo dentro de la tolerancia (+20 s)** | `10:48` (T0+14m) | Bloquear la sesión con `Win + L` a las `10:47:50`. Desbloquear a las `10:48:20` (20 s después de la hora exacta, aún dentro de los 60 s). | Al desbloquear a las 10:48:20, **no aparece ninguna ventana**. El motor evaluó la pausa en el tick de las 10:48:00 con sesión bloqueada y la marcó como atendida/descartada. | No se muestran pausas tardías aún desbloqueando a los 20s. |
| **P6. Día no laborable** | `10:51` (T0+17m) | Editar `config.json` quitando el día actual (`Friday`) de `diasLaborables`. Reiniciar la app desde bandeja ("Salir" y relanzar). Esperar a las 10:51. | A las 10:51 **no aparece ninguna pausa**. | La app respeta estrictamente los días laborables configurados. |

---

## 3. Especificación de Requisitos Funcionales de Prueba (RF-TEST)

### RF-TEST-01: Disparo puntual y presentación limpia
- **Descripción:** Cuando el reloj del sistema alcanza una hora en `horasPausa` en un día laborable con sesión desbloqueada:
  1. Se abre la ventana en el monitor principal, en primer plano (`TopMost`), sin foco inicial en ningún botón.
  2. El contador inicia en la duración configurada (`15` segundos de prueba o `05:00` en producción).
  3. Muestra los botones "Posponer (2)" y "Saltar".
- **Criterio de aceptación:** Aparece en un máximo de 60 segundos posteriores a la hora programada.

### RF-TEST-02: Completar pausa ("Terminé" y sonido)
- **Descripción:** Al completarse la cuenta regresiva:
  1. El contador llega a `00:00`.
  2. Se reproduce el sonido `fin.wav` incrustado.
  3. Los botones "Posponer" y "Saltar" se ocultan.
  4. Aparece el botón "Terminé".
  5. Al hacer clic en "Terminé", la ventana se cierra y no vuelve a abrir hasta la siguiente hora.

### RF-TEST-03: Posponer y límite de 2 posposiciones
- **Descripción:**
  1. Al presionar "Posponer", la ventana se cierra de inmediato.
  2. Trascurrido el tiempo de posposición (`1` minuto en test, `10` min en producción), la ventana reabre.
  3. La imagen mostrada **es idéntica** a la de la primera apertura (no avanza el índice).
  4. El botón actualiza su texto a "Posponer (1)".
  5. Si se vuelve a posponer, a la tercera reapertura el botón "Posponer (0)" se encuentra **deshabilitado**.

### RF-TEST-04: Atajo Alt+F4 y botón Saltar
- **Descripción:**
  1. Al presionar `Alt + F4` con la ventana activa, o al hacer clic en "Saltar", la ventana se cierra inmediatamente.
  2. La aplicación permanece activa en la bandeja del sistema.
  3. No se produce ninguna reapertura hasta la siguiente hora programada.

### RF-TEST-05: Reemplazo por nueva pausa programada
- **Descripción:** Si una pausa está en curso o en espera de reapertura por posposición y llega la hora de la siguiente pausa programada en `horasPausa`:
  1. La pausa pospuesta o anterior se descarta automáticamente.
  2. Se presenta la nueva pausa con la **siguiente imagen** del banco secuencial.
  3. El contador de posposiciones se reinicia a 2.

### RF-TEST-06: Descarte por bloqueo fuera de tolerancia
- **Descripción:** Si el equipo está bloqueado (`Win + L`) a la hora de disparo y permanece bloqueado durante toda la ventana de tolerancia de 60 segundos:
  1. Al desbloquear el equipo posteriormente, la pausa no se recupera ni aparece.

### RF-TEST-07: Descarte por bloqueo dentro de la tolerancia (20 segundos)
- **Descripción:** Si el equipo está bloqueado a la hora exacta de disparo (ej. 10:34:00) y se desbloquea dentro de los 60 segundos de tolerancia (ej. 10:34:20):
  1. La pausa ya fue evaluada y marcada como atendida durante el estado bloqueado.
  2. No se dispara ninguna pausa retroactiva al desbloquear.

### RF-TEST-08: Exclusión de días no laborables
- **Descripción:** Si el día actual de la semana no figura en `diasLaborables` de `config.json`:
  1. Al llegar cualquiera de las horas programadas, el sistema descarta el tick y no abre ninguna ventana.

---

## 4. Archivos de Configuración

### 4.1. Configuración de Producción (`src/PausasActivas/config.produccion.json`)
```json
{
  "horasPausa": [
    "08:00",
    "10:00",
    "12:00",
    "14:00",
    "16:00"
  ],
  "diasLaborables": [
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday"
  ]
}
```

### 4.2. Configuración de Test con Comentarios (`src/PausasActivas/config.test.json`)
El lector soporta comentarios en formato `//` y `/* */`:
```jsonc
{
  // ==============================================================================
  // CONFIGURACIÓN DE PRUEBAS — PAUSAS ACTIVAS
  // Horarios ajustados para batería de pruebas secuenciales (Pausa: 15s | Posponer: 1m)
  // ==============================================================================
  "horasPausa": [
    "10:20", // [Prueba 1] Disparo a tiempo + Esperar 15s -> Reproducción sonido + Botón "Terminé"
    "10:22", // [Prueba 2] Posponer x2 (reabre a las 10:23 y 10:24 con misma imagen; luego deshabilitado) + Probar Alt+F4
    "10:26", // [Prueba 3a] Reemplazo: Abrir y presionar "Posponer"
    "10:28", // [Prueba 3b] Reemplazo: Dispara nueva pausa que reemplaza la pospuesta con nueva imagen
    "10:31", // [Prueba 4] Sesión bloqueada: Bloquear con Win+L antes de 10:31 y desbloquear a las 10:32:30 -> Se descarta
    "10:34", // [Prueba 5] Bloqueo dentro de tolerancia: Bloquear antes de 10:34 y desbloquear a las 10:34:20 (+20s) -> Se descarta igual
    "10:37"  // [Prueba 6] Día no laborable: Quitar "Friday" de diasLaborables, reiniciar y verificar que a las 10:37 no abre
  ],
  "diasLaborables": [
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday"
  ]
}
```

---

## 5. Instrucciones para Ejecución Automática por un Agente

Si un agente autónomo ejecuta la batería de pruebas:

1. **Ajuste automático de horarios:**
   Ejecutar el script Python para generar los horarios relativos a la hora actual:
   ```pwsh
   python scripts/ajustar_tiempos_test.py --offset 2
   ```
2. **Reinicio de la aplicación:**
   Cerrar la instancia previa si existe y arrancar el ejecutable para que cargue la nueva configuración:
   ```pwsh
   Stop-Process -Name "PausasActivas" -Force -ErrorAction SilentlyContinue
   Start-Process "src\PausasActivas\bin\Debug\net10.0-windows\PausasActivas.exe"
   ```
3. **Monitoreo y Verificación:**
   - Supervisar en cada marca de tiempo la presencia del proceso y las ventanas activas.
   - Registrar el resultado de cada caso en la bitácora de ejecución.
