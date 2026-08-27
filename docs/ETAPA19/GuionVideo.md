# ETAPA 19 — Guion de Video Demostrativo: Centro de Aprobaciones (Human-in-the-Loop)

**Duración estimada:** 8-10 min. **Entorno:** Docker (`docker compose --profile prod up -d`), navegador en Web (puerto 5206).

## 0. Preparación (fuera de cámara)
- Levantar stack: `docker compose --profile prod up -d`.
- Verificar Dashboard de ejecuciones y Planes en 0 (empezar limpio).
- Tener un usuario "Gerente" y un usuario "Solicitante" con roles adecuados.

## 1. Generación de un plan (RF video punto 1) — 0:00
- Ir a **Planner Engine** → escribir: *"Genera el reporte financiero del trimestre y publícalo para gerencia."*
- Clic **Generar plan**. Mostrar el DAG resultante: pasos de consulta SQL, generar PDF y **publicar (tipo Approval)**.

## 2. Solicitud de aprobación (RF punto 2) — 1:30
- Clic **Ejecutar**. El plan se pausa en el paso de publicación.
- Ir a **Centro de Aprobaciones** → mostrar la nueva solicitud en estado *Pendiente*, tipo *Financiera/Publicación*, asignada al Gerente.

## 3. Bandeja personal (RF punto 3) — 3:00
- Iniciar sesión como **Gerente**.
- **Centro de Aprobaciones → Mi bandeja**: mostrar la solicitud pendiente con su descripción.

## 4. Aprobación (RF punto 4) — 4:00
- En la bandeja, escribir comentario ("Aprobado para gerencia") y clic **Aprobar**.
- Volver al **Planner** / Dashboard de ejecuciones: el plan reanuda y completa la publicación (trazabilidad completa).

## 5. Rechazo (RF punto 5) — 5:30
- Repetir flujo con otro plan. En la bandeja, clic **Rechazar** con motivo.
- Verificar que el plan pasa a *Cancelado* y el motivo queda en auditoría.

## 6. Delegación (RF punto 6) — 6:30
- En la bandeja, clic **Delegar**, ingresar ID de otro usuario, comentario.
- Verificar que la solicitud pasa a *Delegado* y aparece en la bandeja del usuario destino (auditoría de delegación).

## 7. Vencimiento (RF punto 7) — 7:30
- Crear una política con `TiempoMaximoHoras` pequeño (o editar la solicitud para vencer pronto).
- Esperar / forzar expiración → la solicitud pasa a *Expirado* y el plan se cancela.

## 8. Reanudación automática (RF punto 8) — 8:00
- (Cubierto en punto 4) Re-enfatizar: al aprobar, el plan continúa desde donde quedó pausado sin reconstruirse.

## 9. Dashboard (RF punto 9) — 8:30
- **Centro de Aprobaciones**: mostrar conteos por estado (Pendientes, Aprobadas, Rechazadas, Expiradas, Delegadas) y la tabla histórica.

## 10. Auditoría (RF punto 10) — 9:00
- Mostrar `ApprovalDecision` (quién decidió, cuándo, comentario) y `PlanExecutionLog` (registro de pausa/reanudación/cancelación).
- Cerrar resaltando: *"La IA prepara acciones complejas, pero las decisiones críticas permanecen bajo control humano."*
