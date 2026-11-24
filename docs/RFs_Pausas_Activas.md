# Requisitos funcionales — App de Pausas Activas

**Versión:** 1.0 (borrador para implementación) · **Fecha:** 2026-11-20
**Stack:** C# · .NET 10 · Windows Forms · Windows 11 · un solo monitor

---

## 1. Resumen

Aplicación de escritorio que vive en la bandeja del sistema y, a horas fijas del reloj dentro de la jornada laboral, abre una ventana con una imagen de personajes haciendo estiramientos o ejercicios de pausa activa, junto con un contador regresivo de 5 minutos. Las imágenes se generan antes con Gemini y se incrustan en el ejecutable. La app no usa internet ni servicio de Windows.

## 2. Decisiones cerradas

| Tema | Decisión |
|---|---|
| Arquitectura | Una sola app WinForms en bandeja del sistema. **Sin servicio de Windows** (un servicio no puede mostrar ventanas en el escritorio del usuario). |
| Imágenes | Generadas a mano con Gemini antes de compilar. Incrustadas en el .exe. La app no llama a ninguna API. |
| Horario | 08:00 a 16:00, cada 2 horas: **08:00, 10:00, 12:00, 14:00, 16:00** (5 pausas por día). |
| Definición del horario | Archivo JSON editado a mano, junto al ejecutable. |
| Duración del contador | Fija: 5 minutos. |
| Aparición | Ventana directa, siempre al frente, sin notificación previa. |
| Rigor | La pausa se puede posponer (máx. 2 veces, 10 min fijos) o saltar. |
| Orden de imágenes | Secuencial, orden fijo. |
| Fin del contador | Sonido + botón "Terminé" para cerrar. |
| Pausa con PC bloqueado, suspendido o app cerrada | Se pierde; se espera a la siguiente. |
| Inicio con Windows | Automático al iniciar sesión. |
| Menú de bandeja | Solo "Salir". |
| Monitores | Uno. |

## 3. Actores

- **Usuario:** persona que trabaja frente al PC y recibe las pausas.
- **Sistema:** la propia aplicación.
- **Editor de configuración:** quien modifica el JSON a mano. En la práctica es el mismo usuario o el desarrollador.

## 4. Mapa de tareas

| Tarea | Contenido | RFs | Casos de uso |
|---|---|---|---|
| T1 | Configuración y estado | RF-01 a RF-04 | CU-01, CU-02, CU-03 |
| T2 | Arranque y ciclo de vida | RF-05 a RF-08 | CU-04, CU-05 |
| T3 | Programación y disparo | RF-09 a RF-12 | CU-06, CU-07 |
| T4 | Presentación de la pausa | RF-13 a RF-18 | CU-08, CU-09 |
| T5 | Interacción del usuario | RF-19 a RF-23 | CU-10, CU-11, CU-12 |
| T6 | Recursos (imágenes y sonido) | RF-24 a RF-26 | (transversal) |

---

## 5. Tareas, requisitos y casos de uso

## T1. Configuración y estado

### Requisitos funcionales

- **RF-01.** Al arrancar, la app lee un archivo `config.json` ubicado junto al ejecutable. Contiene:
  - `horasPausa`: lista de horas en formato `HH:mm`.
  - `diasLaborables`: lista de días de la semana.
- **RF-02.** La app valida el JSON al arrancar (formato de horas, lista no vacía, días válidos). Si el archivo no existe o es inválido, muestra un mensaje que indica la causa y usa los **valores por defecto**: horas `08:00, 10:00, 12:00, 14:00, 16:00` y días lunes a viernes. La app no se cierra ni falla en silencio.
- **RF-03.** El JSON se lee **solo al arrancar**. Para que un cambio surta efecto, el usuario reinicia la app.
- **RF-04.** La app guarda un archivo de estado (`estado.json`) en `%LOCALAPPDATA%\PausasActivas\` con el índice de la última imagen mostrada. Si el archivo no existe o está corrupto, la secuencia comienza en la imagen 1.

**Ejemplo de `config.json`:**

```json
{
  "horasPausa": ["08:00", "10:00", "12:00", "14:00", "16:00"],
  "diasLaborables": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
}
```

Los días usan los nombres en inglés de `System.DayOfWeek`. Los valores de 5 minutos de pausa, 10 minutos de posposición y máximo de 2 posposiciones son constantes del programa y no van en el JSON.

### CU-01 — Configurar el horario de pausas

- **Historia:** Como editor de configuración, quiero definir las horas y los días de pausa en un archivo para ajustar la app sin recompilar.
- **Precondiciones:** existe `config.json` junto al ejecutable.
- **Flujo principal:**
  1. El editor abre `config.json` y modifica las horas o los días.
  2. Guarda el archivo y reinicia la app (menú "Salir" y volver a abrirla).
  3. La app lee el archivo, lo valida y programa las pausas con los nuevos valores.
- **Flujos alternos:**
  - 2a. Si el editor no reinicia la app, esta sigue con la configuración anterior (RF-03).
- **Criterios de aceptación:**
  - Dado un JSON válido con `"11:30"`, cuando se reinicia la app un día laborable antes de las 11:30, entonces la pausa aparece a las 11:30.
  - Dado un cambio en el JSON sin reiniciar, entonces el comportamiento no cambia.
- **RF relacionados:** RF-01, RF-03.

### CU-02 — Arrancar con un JSON inválido o ausente

- **Historia:** Como usuario, quiero que la app me avise si su configuración está mal para no creer que funciona cuando no lo hace.
- **Precondiciones:** `config.json` no existe, está vacío o tiene formato incorrecto (ej. coma sobrante, hora `"25:99"`).
- **Flujo principal:**
  1. La app intenta leer y validar el archivo y detecta el error.
  2. Muestra un mensaje con la causa del error.
  3. Carga los valores por defecto y sigue funcionando.
- **Criterios de aceptación:**
  - Dado un JSON con una hora inválida, cuando arranca la app, entonces aparece un mensaje con la causa y la app programa las 5 pausas por defecto.
  - La app nunca termina por una excepción de lectura de configuración.
- **RF relacionados:** RF-02.

### CU-03 — Continuar la secuencia de imágenes tras reiniciar

- **Historia:** Como usuario, quiero que las imágenes sigan en orden aunque reinicie el PC para no ver siempre las primeras.
- **Precondiciones:** existe `estado.json` con el índice de la última imagen mostrada.
- **Flujo principal:**
  1. La app arranca y lee `estado.json`.
  2. En la siguiente pausa muestra la imagen siguiente a la guardada.
  3. Tras mostrarla, actualiza el índice en `estado.json`.
- **Flujos alternos:**
  - 1a. Si `estado.json` no existe o está corrupto, comienza en la imagen 1.
- **Criterios de aceptación:**
  - Dado el índice 3 guardado, cuando se muestra una pausa nueva, entonces aparece la imagen 4.
  - Dado el último índice del banco, la siguiente pausa muestra la imagen 1.
- **RF relacionados:** RF-04, RF-17.

## T2. Arranque y ciclo de vida

### Requisitos funcionales

- **RF-05.** Solo puede haber **una instancia** de la app en ejecución. Si se abre una segunda, se cierra sin mostrar nada.
- **RF-06.** La app se registra a sí misma para iniciar al iniciar sesión en Windows (clave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`), en el primer arranque.
- **RF-07.** La app muestra un icono en la bandeja del sistema. Su menú contextual tiene **una sola opción: Salir**.
- **RF-08.** "Salir" cierra la app por completo, incluida cualquier ventana de pausa abierta. La app vuelve a ejecutarse en el próximo inicio de sesión o cuando el usuario la abra a mano.

### CU-04 — Iniciar la app con Windows

- **Historia:** Como usuario, quiero que la app arranque sola cuando inicio sesión para no tener que acordarme de abrirla.
- **Precondiciones:** la app se ejecutó al menos una vez, de modo que se registró en el inicio.
- **Flujo principal:**
  1. El usuario inicia sesión en Windows.
  2. Windows lanza la app.
  3. La app se coloca en la bandeja del sistema sin mostrar ventanas y programa las pausas del día.
- **Flujos alternos:**
  - 1a. Si ya hay una instancia en ejecución, la segunda se cierra sin mostrar nada (RF-05).
- **Criterios de aceptación:**
  - Tras reiniciar sesión, el icono aparece en la bandeja sin intervención del usuario.
  - Abrir la app dos veces deja un único proceso y un único icono.
- **RF relacionados:** RF-05, RF-06, RF-07.

### CU-05 — Salir de la app

- **Historia:** Como usuario, quiero poder cerrar la app para que no me interrumpa si lo necesito.
- **Flujo principal:**
  1. El usuario hace clic derecho en el icono de la bandeja.
  2. Elige "Salir".
  3. La app se cierra y desaparece el icono.
- **Flujos alternos:**
  - 3a. Si hay una ventana de pausa abierta, también se cierra.
- **Criterios de aceptación:**
  - Después de "Salir", no aparece ninguna pausa hasta el próximo arranque de la app.
- **Nota:** como no hay opción de "desactivar por hoy", salir es la única forma de evitar las pausas del día, y la app vuelve a arrancar en el siguiente inicio de sesión.
- **RF relacionados:** RF-07, RF-08.

## T3. Programación y disparo

### Requisitos funcionales

- **RF-09.** La app dispara una pausa a cada hora de `horasPausa`, solo en los días de `diasLaborables`. El sistema evalúa la hora periódicamente; una pausa programada a `HH:mm` se considera a tiempo si se detecta dentro de los **60 segundos** posteriores a esa hora.
- **RF-10.** La pausa solo se muestra si, a la hora programada, la sesión de Windows está **desbloqueada y activa**. Si el equipo está bloqueado o suspendido, o la app está cerrada, esa pausa **se pierde** y no se recupera ni se muestra después.
- **RF-11.** Valores por defecto: de 08:00 a 16:00 cada 2 horas, lunes a viernes (5 pausas por día).
- **RF-12.** Si llega la hora de una pausa mientras hay otra ventana abierta (por ejemplo, el usuario nunca pulsó "Terminé"), la ventana anterior se cierra como saltada y se muestra la nueva.

### CU-06 — Recibir la pausa programada

- **Historia:** Como usuario, quiero que la pausa aparezca a las horas fijas para recordar hacer mis ejercicios.
- **Precondiciones:** app en ejecución; hoy es día laborable; la sesión está desbloqueada.
- **Flujo principal:**
  1. Llega una hora de `horasPausa`.
  2. La app confirma que la sesión está desbloqueada.
  3. Abre la ventana de pausa (CU-08).
- **Flujos alternos:**
  - 2a. Sesión bloqueada o suspendida: no se muestra nada (CU-07).
  - 1a. Hoy no es día laborable: no se muestra nada.
- **Criterios de aceptación:**
  - Dado el JSON por defecto, un martes con sesión desbloqueada, a las 10:00 aparece la ventana en un máximo de 60 s.
  - Dado un sábado, no aparece ninguna ventana.
- **RF relacionados:** RF-09, RF-11.

### CU-07 — Pausa perdida por equipo bloqueado, suspendido o app cerrada

- **Historia:** Como usuario, no quiero que me aparezca una pausa atrasada al volver al PC.
- **Precondiciones:** llega la hora de una pausa y el equipo está bloqueado, suspendido, o la app no está en ejecución.
- **Flujo principal:**
  1. Llega la hora programada.
  2. La condición de bloqueo, suspensión o cierre impide mostrar la pausa.
  3. La pausa se descarta. La app espera a la siguiente hora.
- **Criterios de aceptación:**
  - Dado el equipo bloqueado a las 12:00, cuando el usuario desbloquea a las 12:20, entonces no aparece la pausa de las 12:00.
  - Dado que el usuario inicia sesión a las 08:05, la pausa de las 08:00 no aparece, y la primera pausa del día es la de las 10:00.
- **RF relacionados:** RF-10.

## T4. Presentación de la pausa

### Requisitos funcionales

- **RF-13.** La ventana de pausa aparece directamente, **siempre al frente**, centrada en el monitor principal, sin notificación previa.
- **RF-14.** La ventana no tiene botón de cierre del sistema (la "X"). Alt+F4 equivale a "Saltar".
- **RF-15.** Al abrirse, ningún botón tiene el foco inicial, para que un Enter o una barra espaciadora pulsados por casualidad no cierren la pausa.
- **RF-16.** La ventana muestra una imagen fija del banco incrustado. Las imágenes se muestran en **orden secuencial fijo** (ordenadas por nombre de archivo). Al terminar el banco, se vuelve a la primera.
- **RF-17.** El índice de imagen avanza solo cuando se muestra una pausa **nueva**, no al posponer. Se guarda en `estado.json` (RF-04).
- **RF-18.** Un contador regresivo de **5:00** (formato `mm:ss`, fijo) está siempre visible. Inicia al mostrarse la ventana.

### CU-08 — Ver la imagen y el contador

- **Historia:** Como usuario, quiero ver el ejercicio que debo hacer y cuánto tiempo falta para saber cuándo termina mi pausa.
- **Precondiciones:** se disparó una pausa (CU-06).
- **Flujo principal:**
  1. Aparece la ventana al frente, con la siguiente imagen de la secuencia.
  2. El contador comienza en 5:00 y baja cada segundo.
  3. El usuario hace los ejercicios mientras el contador corre.
- **Criterios de aceptación:**
  - La ventana aparece en el monitor principal y encima de otras ventanas.
  - Ningún botón aparece seleccionado al abrir la ventana.
  - El contador baja de 5:00 a 0:00 en 5 minutos.
  - La ventana no tiene botón de cierre del sistema.
- **RF relacionados:** RF-13 a RF-18.

### CU-09 — Cerrar la ventana con Alt+F4

- **Historia:** Como usuario, no quiero quedar atrapado en una ventana que no se deja cerrar.
- **Flujo principal:**
  1. Con la ventana de pausa activa, el usuario pulsa Alt+F4.
  2. La app lo trata como "Saltar" (CU-11).
- **Criterios de aceptación:**
  - Alt+F4 no cierra la app completa. Solo cierra la pausa actual.
- **RF relacionados:** RF-14.

## T5. Interacción del usuario

### Requisitos funcionales

- **RF-19.** El botón **Posponer** cierra la ventana y la reabre 10 minutos después, con la **misma imagen** y el contador reiniciado en 5:00. Se permiten **máximo 2 posposiciones** por pausa. Tras la segunda, el botón queda deshabilitado. El botón muestra cuántas posposiciones quedan.
- **RF-20.** "Posponer" y "Saltar" están disponibles solo mientras el contador corre.
- **RF-21.** El botón **Saltar** cierra la ventana sin completar la pausa. No hay reintento: la siguiente pausa se dispara a su hora normal.
- **RF-22.** Cuando el contador llega a 0, suena un sonido, los botones "Posponer" y "Saltar" se ocultan y aparece el botón **Terminé**. La ventana permanece abierta hasta que el usuario lo pulse.
- **RF-23.** El botón **Terminé** cierra la ventana y completa la pausa.

### CU-10 — Posponer la pausa

- **Historia:** Como usuario, quiero retrasar la pausa unos minutos cuando estoy en medio de algo importante.
- **Precondiciones:** ventana de pausa abierta con el contador corriendo y menos de 2 posposiciones usadas.
- **Flujo principal:**
  1. El usuario pulsa "Posponer".
  2. La ventana se cierra.
  3. A los 10 minutos, la ventana reaparece con la misma imagen y el contador en 5:00.
- **Flujos alternos:**
  - 3a. Si al reaparecer el equipo está bloqueado o suspendido, la pausa se pierde (misma regla que RF-10).
  - 1a. Si ya se usaron 2 posposiciones, el botón está deshabilitado.
- **Criterios de aceptación:**
  - Tras pulsar "Posponer" a las 10:00, la ventana reaparece a las 10:10, con la imagen que ya se había mostrado.
  - Después de la segunda posposición, el botón "Posponer" no se puede pulsar.
  - La pausa pospuesta no avanza el índice de imagen.
- **RF relacionados:** RF-17, RF-19, RF-20.

### CU-11 — Saltar la pausa

- **Historia:** Como usuario, quiero poder omitir la pausa cuando de verdad no puedo hacerla.
- **Precondiciones:** ventana abierta con el contador corriendo.
- **Flujo principal:**
  1. El usuario pulsa "Saltar" (o Alt+F4).
  2. La ventana se cierra.
  3. La app espera a la siguiente hora programada.
- **Criterios de aceptación:**
  - Tras saltar, no aparece ninguna ventana hasta la siguiente hora de `horasPausa`.
- **RF relacionados:** RF-14, RF-20, RF-21.

### CU-12 — Completar la pausa

- **Historia:** Como usuario, quiero saber cuándo terminó mi pausa para volver al trabajo.
- **Precondiciones:** ventana abierta con el contador corriendo.
- **Flujo principal:**
  1. El contador llega a 0:00.
  2. Suena un sonido y los botones "Posponer" y "Saltar" desaparecen.
  3. Aparece el botón "Terminé".
  4. El usuario pulsa "Terminé" y la ventana se cierra.
- **Flujos alternos:**
  - 3a. Si el usuario no está frente al PC, la ventana permanece abierta hasta que regrese y pulse "Terminé".
  - 3b. Si llega la hora de la siguiente pausa con la ventana todavía abierta, se reemplaza por la nueva (RF-12).
- **Criterios de aceptación:**
  - Al llegar a 0:00 suena el sonido una vez y solo se ve el botón "Terminé".
  - La ventana no se cierra sola.
- **RF relacionados:** RF-12, RF-22, RF-23.

## T6. Recursos (imágenes y sonido)

### Requisitos funcionales

- **RF-24.** Las imágenes se incrustan en el ejecutable como recursos y se ordenan por nombre de archivo (`01.png`, `02.png`, …). Agregar o cambiar imágenes requiere recompilar.
- **RF-25.** El sonido de fin de pausa es un recurso corto incrustado en el ejecutable.
- **RF-26.** La app no usa internet en ejecución. La generación de imágenes con Gemini ocurre fuera de la app.

---

## 6. Requisitos no funcionales

| ID | Requisito |
|---|---|
| RNF-01 | C# con .NET 10 y Windows Forms, para Windows 11. |
| RNF-02 | Sin servicio de Windows. |
| RNF-03 | Sin dependencias de red ni API keys en ejecución. |
| RNF-04 | Soporte de un solo monitor. La ventana aparece en el monitor principal. |
| RNF-05 | Consumo mínimo en reposo: solo un temporizador periódico mientras no hay pausa activa. |
| RNF-06 | La app no debe terminar por excepciones al leer `config.json` o `estado.json`. |

## 7. Trazabilidad RF ↔ casos de uso

| RF | CU | | RF | CU |
|---|---|---|---|---|
| RF-01 | CU-01 | | RF-14 | CU-08, CU-09, CU-11 |
| RF-02 | CU-02 | | RF-15 | CU-08 |
| RF-03 | CU-01 | | RF-16 | CU-08 |
| RF-04 | CU-03 | | RF-17 | CU-03, CU-10 |
| RF-05 | CU-04 | | RF-18 | CU-08 |
| RF-06 | CU-04 | | RF-19 | CU-10 |
| RF-07 | CU-04, CU-05 | | RF-20 | CU-10, CU-11 |
| RF-08 | CU-05 | | RF-21 | CU-11 |
| RF-09 | CU-06 | | RF-22 | CU-12 |
| RF-10 | CU-07, CU-10 | | RF-23 | CU-12 |
| RF-11 | CU-02, CU-06 | | RF-24 a RF-26 | transversales |
| RF-12 | CU-12 | | | |
| RF-13 | CU-08 | | | |

## 8. Fuera de alcance (versión 1.0)

- Generar imágenes con Gemini desde la app.
- Múltiples monitores.
- Ventana de ajustes o edición de la configuración desde la interfaz.
- Opciones de menú adicionales (desactivar por hoy, probar una pausa, abrir carpeta).
- Estadísticas, historial de pausas o registro de cumplimiento.
- Secuencia de varias imágenes dentro de una misma pausa de 5 minutos.
- Recuperar pausas perdidas.

## 9. Supuestos que decidí yo (el usuario no los confirmó)

1. **Días laborables:** lunes a viernes por defecto.
2. **Horario inclusivo:** "de 8 am a 4 pm cada 2 horas" se interpreta como 08:00, 10:00, 12:00, 14:00 y 16:00 (ambos extremos incluidos).
3. **Tolerancia de 60 s** para considerar una pausa "a tiempo" (RF-09).
4. **La posposición reabre la misma imagen** con el contador reiniciado en 5:00, y no avanza el índice.
5. **La configuración solo se lee al arrancar** (RF-03).
6. **La app se registra sola en el inicio** de Windows en el primer arranque.
7. **Una pausa nueva reemplaza a la anterior** si esta sigue abierta (RF-12).
8. **Alt+F4 equivale a Saltar** y no hay botón de cierre del sistema.
9. **"Posponer" y "Saltar" solo existen mientras el contador corre.**
10. **El orden de las imágenes** es por nombre de archivo.
11. **El estado vive en `%LOCALAPPDATA%\PausasActivas\`**, aparte de la configuración.
12. **Una pausa pospuesta puede aparecer fuera del horario laboral** (ej. la de las 16:00 reaparece a las 16:10).

## 10. Pendientes por decidir

- Cantidad, formato y resolución de las imágenes del banco.
- Cuál es el sonido de fin de pausa (propuesta: un `.wav` corto incrustado).
- Si se quiere un registro de errores en archivo.

## 11. Riesgos reales

- **La pausa de las 08:00 se perderá casi siempre.** La app arranca al iniciar sesión. Si el usuario entra a las 08:03, esa pausa ya pasó y se descarta (RF-10). En la práctica, la primera pausa útil del día será la de las 10:00. Si esto no es lo que quieres, hay que cambiar la hora de la primera pausa o permitir recuperar pausas atrasadas por un margen corto.
- **Tamaño del banco de imágenes:** son 5 pausas por día, 25 por semana laboral. Con 10 imágenes, la secuencia se repite cada 2 días. Con 20, cada 4 días. Con pocas imágenes, el usuario las memorizará pronto y dejará de mirarlas.
- **Una imagen fija durante 5 minutos** es la parte más débil del diseño. Es muy probable que el usuario pulse "Saltar" tras unas pocas semanas. Es una decisión consciente, pero el riesgo existe.
- **Menú solo con "Salir":** para probar una pausa hay que editar el JSON con una hora cercana y reiniciar la app.
- **Siempre al frente sin aviso previo:** interrumpe lo que el usuario esté haciendo (videollamada, presentación). No hay detección de pantalla completa ni de reuniones en esta versión.
