# ETAPA 19 — Manual Técnico: Centro de Aprobaciones (Human-in-the-Loop)

## 1. Objetivo
Implementar un Centro de Aprobaciones que permita intervención humana dentro de los procesos
ejecutados por la IA. Determinadas acciones (sensibles) quedan pausadas hasta ser aprobadas
por usuarios autorizados. El sistema pausa planes, solicita aprobaciones, gestiona múltiples
aprobadores, controla vencimientos, registra decisiones y reanuda automáticamente.

## 2. Componentes creados

### 2.1 Dominio (`Asistente.Domain/Entities/Aprobaciones/`)
- `ApprovalEnums.cs`: `EstadoAprobacion` (Pendiente, EnRevision, Aprobado, Rechazado, Cancelado, Expirado, Delegado) y `TipoAprobacion` (Operacional, Financiera, Administrativa, Seguridad, Publicacion, Manual).
- `ApprovalRequest.cs`: solicitud de aprobación (IdPlan, Tipo, Estado, FechaSolicitud, FechaVencimiento, Solicitante, Observaciones, IdPolicy).
- `ApprovalDecision.cs`: decisión individual (IdUsuario, Decision, Comentario, FechaDecision).
- `ApprovalAssignee.cs`: aprobador asignado (IdUsuario, EsPrincipal, Estado).
- `ApprovalPolicy.cs`: política configurable (CantidadMinimaAprobaciones, RequiereUnanimidad, PermiteDelegacion, TiempoMaximoHoras, Activo).

### 2.2 Infraestructura
- `Configurations/AprobacionesConfiguration.cs`: mapeo EF (tablas `ApprovalRequest`, `ApprovalDecision`, `ApprovalAssignee`, `ApprovalPolicy`; enums como `int`).
- `AsistenteDbContext.cs`: 4 DbSets nuevos.
- `Repositories/ApprovalRepositories.cs`: implementación de `IApprovalRequestRepository`, `IApprovalDecisionRepository`, `IApprovalAssigneeRepository`, `IApprovalPolicyRepository` (en `Asistente.Application/Interfaces/IApprovalRepositories.cs`).
- DI: registrados en `Asistente.Infrastructure/DependencyInjection.cs`.

### 2.3 Aplicación — Approval Manager (`Asistente.Application/Aprobaciones/ApprovalManager.cs`)
Responsable de TODO el ciclo de vida (punto 8 del RF):
- `CrearSolicitudAsync`: crea la solicitud y asigna aprobadores (el primero = principal). Calcula vencimiento desde la política.
- `EsperarResolucionAsync`: bloquea (polling 2s) hasta que la solicitud se resuelva o venza (timeout configurable).
- `DecidirAsync`: valida permisos (Regla 2/7: el asignado pendiente; Regla 6: el solicitante no aprueba lo suyo). Evalúa política (unanimidad / mayoría / simple). Si se aprueba → reanuda el plan; si se rechaza → cancela.
- `DelegarAsync`: delega a otro usuario si la política lo permite (Regla 5: queda auditada).
- `ExpirarAsync`: marca Expirado y cancela el plan (Regla 4: respeta la política).

### 2.4 Integración con Planner y Orchestrator
- `PlannerEngine.EjecutarPlanAsync`: si el plan tiene pasos `Tipo == "Approval"`, crea la `ApprovalRequest`, pone el plan en `EnEsperaAprobacion` y **espera** la resolución vía `ApprovalManager`. Solo si se aprueba delega al Orchestrator.
- `PlanBuilder`: ya genera pasos `Approval` cuando la intención es sensible (ej. "eliminar", "enviar", "publicar").
- `ExecutionGraphBuilder`: marca los nodos de tipo `Approval` con `EsAprobacion = true`.
- `AgentOrchestrator`: omite (estado `Omitido`) los nodos `EsAprobacion` — los gestiona el ApprovalManager, no el grafo.

### 2.5 API (`Asistente.API/Controllers/AprobacionesController.cs`)
- `GET /api/aprobaciones/dashboard` — conteos por estado.
- `GET /api/aprobaciones/bandeja` — pendientes del usuario autenticado.
- `POST /api/aprobaciones/{id}/decidir` — Aprobar/Rechazar + comentario.
- `POST /api/aprobaciones/{id}/delegar` — delegar a otro usuario.
- `POST /api/aprobaciones/{id}/expirar` — forzar expiración.
- `GET|POST /api/aprobaciones/politicas` — consultar/crear políticas.

### 2.6 UI Web (`Asistente.Web/Controllers/AprobacionesController.cs` + vistas)
- `Index.cshtml`: Centro de Aprobaciones (dashboard por estado + tabla de solicitudes).
- `Bandeja.cshtml`: mis aprobaciones pendientes (aprobar/rechazar/delegar con comentario).
- Enlace en `_Layout.cshtml` → "Centro de Aprobaciones".

## 3. Modelo de datos (SQL Server)
Tablas: `ApprovalRequest`, `ApprovalDecision`, `ApprovalAssignee`, `ApprovalPolicy` (script: `etapa19_migration.sql`).
Migración EF: `20260827153826_Etapa19CentroAprobaciones`.

## 4. Pruebas
- `Asistente.Tests/Services/ApprovalManagerTests.cs`: 7 tests (crear, aprobar simple+reanudar, rechazar+cancelar, solicitante no aprueba lo suyo, unanimidad, delegar, expirar).
- Total suite: **204 tests OK** (eran 197 antes de ETAPA 19).

## 5. Reglas de negocio aplicadas (RF punto 12)
1. Acciones sensibles nunca se ejecutan sin aprobación.
2. Toda aprobación respeta permisos.
3. Los comentarios se conservan en auditoría.
4. Las aprobaciones vencidas siguen la política configurada.
5. La delegación queda auditada.
6. La IA nunca aprueba sus propias acciones (Solicitante ≠ Aprobador).
7. Un usuario no aprueba solicitudes para las que no está autorizado.

## 6. Cómo ejecutar
```bash
docker compose --profile prod up -d   # levanta mssql, chroma, ollama, api, web
# Generar un plan con acción sensible (ej. "publica el reporte") → queda pausado
# Ir a Centro de Aprobaciones → Bandeja → Aprobar/Rechazar
```
