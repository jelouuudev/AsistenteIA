# ETAPA 19 — Manual de Usuario: Centro de Aprobaciones (Human-in-the-Loop)

## ¿Qué es?
El Centro de Aprobaciones permite que acciones importantes de la IA (publicar un agente, enviar un
reporte, cambiar configuración) sean revisadas y aprobadas por una persona antes de ejecutarse.
Esto garantiza control humano sobre decisiones críticas.

## Acceso
En el menú superior → **Centro de Aprobaciones** (icono de escudo).

## 1. Generar un plan que requiere aprobación
En **Planner Engine**, escribe un objetivo que incluya una acción sensible, por ejemplo:
> "Genera el reporte financiero del trimestre y **publícalo** para gerencia."

El Planner genera el plan; el paso sensible aparece como tipo `Approval`. Al ejecutar, el plan se
**pausa** automáticamente y crea una solicitud de aprobación (estado: *Pendiente*).

## 2. Dashboard del Centro de Aprobaciones
Muestra el conteo por estado:
- Pendientes, En revisión, Aprobadas, Rechazadas, Expiradas, Delegadas, Canceladas.
- Tabla de solicitudes recientes (código, plan, tipo, estado, vencimiento).

## 3. Mi bandeja de aprobaciones
Desde el dashboard → **Ir a mi bandeja**. Aquí ves las solicitudes asignadas a ti.
Para cada una puedes:
- **Aprobar**: la acción continúa (el plan se reanuda automáticamente).
- **Rechazar**: el plan se cancela y se registra el motivo.
- **Comentario**: obligatorio registrar el motivo/observaciones (queda en auditoría).
- **Delegar**: asignar a otro usuario (si la política lo permite).

## 4. Ejemplo de flujo (RF punto 22)
> "Genera el reporte financiero del trimestre y publícalo para gerencia."
1. El Planner construye el plan (consulta SQL → generar PDF → **publicar**).
2. El paso "publicar" requiere aprobación → el plan se pausa.
3. El Gerente recibe la solicitud en su bandeja.
4. El Gerente aprueba (con comentario).
5. El plan se reanuda y completa la publicación con trazabilidad completa.

## 5. Vencimientos
Si la política tiene `TiempoMaximoHoras > 0` y nadie decide a tiempo, la solicitud pasa a
**Expirado** y el plan se cancela (comportamiento configurable).

## 6. Delegación
Si no puedes decidir, delega a un compañero autorizado. La delegación queda registrada en auditoría.

## Notas
- La IA nunca aprueba sus propias acciones (quien solicitó no puede aprobar).
- Todos los comentarios y decisiones se conservan para auditoría.
