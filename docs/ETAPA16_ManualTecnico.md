# ETAPA 16 — Plataforma Multiagente: Manual Técnico

**Proyecto:** Asistente Inteligente Empresarial (IA Local) — Versión 2.0
**Fecha:** 2026-08-19
**Alcance:** Implementación de la arquitectura multiagente sobre la base de la Versión 1.0.

---

## 1. Resumen de la arquitectura

La ETAPA 16 evoluciona el único "Asistente IA" de la V1.0 hacia un **Agent Manager** que administra múltiples **agentes especializados**. Por decisión de diseño (confirmada con el usuario), *Agente = Asistente evolucionado*: no se duplicó la entidad, se extendió `Asistente` y se agregaron las relaciones de asignación.

| Capa | Componente | Rol en ETAPA 16 |
|------|-----------|-----------------|
| Domain | `Asistente`, `AgenteWorkflow`, `AgenteRol`, `AgenteVersion`, `EstadoAgente` | Modelo de datos del agente |
| Application | `AsistenteService` (Agent Manager), `ChatService` (Agent Runtime), `AutorizacionService` | Lógica de negocio |
| Infrastructure | `AsistenteDbContext`, repositorios, migraciones | Persistencia |
| API | `AgentesController` | Endpoints REST del Agent Manager |
| Web | `AsistentesController` + vistas | Administración y dashboard |
| Web (Chat) | `Chat/Index.cshtml` + `chat.js` | Selección y uso del agente |

---

## 2. Modelo de datos

### 2.1 Entidad `Asistente` (extendida)
Nuevos campos (migración `Etapa16Multiagente`):
- `Codigo` (nvarchar(50), requerido): identificador único legible (ej. `COMERCIAL-01`).
- `Objetivo` (nvarchar): propósito del agente.
- `PromptSistema` (nvarchar(max)): instrucciones propias del agente (tiene prioridad sobre el prompt genérico de la plataforma).
- `Estado` (int): `0=Borrador`, `1=Prueba`, `2=Publicado`, `3=Deshabilitado` (enum `EstadoAgente`).
- `Version` (int): versión actual del agente.

### 2.2 Nuevas entidades de relación
- `AgenteWorkflow` (`AgentesWorkflows`): `IdAgenteWorkflow PK`, `IdAsistente FK`, `IdWorkflow FK`, `Activo`.
- `AgenteRol` (`AgentesRoles`): `IdAgenteRol PK`, `IdAsistente FK`, `IdRol FK`, `Activo`.
- `AgenteVersion` (`AgentesVersiones`): `IdAgenteVersion PK`, `IdAsistente FK`, `Version`, `ModeloIA`, `Estado`, `FechaCreacion`, `UsuarioCreacion`, `ConfiguracionJson`.

### 2.3 Auditoría
`AuditoriaIA` ganó `VersionAgente` (int, nullable) — migración `Etapa16AuditoriaVersion`.

---

## 3. Agent Manager (`AsistenteService`)

Métodos principales:
- `CrearAsistenteAsync(CrearAsistenteRequest)`: crea en estado `Borrador`, versión 1, sincroniza asignaciones (fuentes/herramientas/workflows/roles/usuarios).
- `ActualizarAsistenteAsync(...)`: sincroniza asignaciones (reemplaza listas).
- `ActivarAsistenteAsync` / `DesactivarAsistenteAsync`: activa/desactiva (eliminación lógica, Regla 10).
- `PublicarAsync`: `Estado = Publicado`.
- `EnviarAPruebaAsync`: `Estado = Prueba`.
- `CrearVersionAsync`: incrementa `Version`, guarda snapshot en `AgentesVersiones` (modelo, estado, configuración JSON).
- `DuplicarAsync`: copia profunda del agente (asignaciones incluidas) en estado `Borrador` con nuevo `Codigo`.
- `ObtenerAutorizadosParaUsuarioAsync(idUsuario, roles)`: agentes donde el usuario es admin, está asignado directo, o su rol está asignado (Regla 1).

---

## 4. Agent Runtime (`ChatService`)

Antes de procesar un mensaje:
1. **Validación de autorización (Regla 1):** `AutorizacionService.VerificarAsistenteAsync(usuario, agente)` — admin O asignado directo O rol asignado. Si deniega, responde con mensaje de no autorizado.
2. **Carga de configuración del agente:** modelo, temperatura, maxTokens, y **prompt propio** (`PromptSistema`) con prioridad. El `Objetivo` se anexa al contexto.
3. **RAG por fuentes del agente:** `RecuperarContextoConFuentesAsync` filtra por `AsistentesFuentes` (Regla 2).
4. **Herramientas/workflows por asignación:** el `ToolOrchestrator` usa `AsistenteHerramienta`/`AgenteWorkflow`.
5. **Auditoría:** registra `IdAsistente` + `VersionAgente` en `AuditoriaIA`.

---

## 5. API REST (`AgentesController`)

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/agentes` | Todos los agentes |
| GET | `/api/agentes/{id}` | Agente por id |
| GET | `/api/agentes/autorizados/{idUsuario}` | Agentes autorizados para un usuario (Regla 1) |
| POST | `/api/agentes` | Crear |
| PUT | `/api/agentes/{id}` | Actualizar |
| DELETE | `/api/agentes/{id}` | Desactivar (lógico) |
| POST | `/api/agentes/{id}/publicar` | Publicar |
| POST | `/api/agentes/{id}/enviar-prueba` | Enviar a prueba |
| POST | `/api/agentes/{id}/duplicar` | Duplicar |
| POST | `/api/agentes/{id}/versiones` | Crear versión |
| GET | `/api/agentes/{id}/versiones` | Listar versiones |

---

## 6. UI Web

- **`Asistentes/Index`**: tabla con Código, Estado (badge de ciclo de vida), Versión, acciones (Editar, Versiones, Publicar, EnviarPrueba, Duplicar, Probar, Activar/Desactivar).
- **`Asistentes/Crear` / `Editar`**: campos `Codigo`, `Objetivo`, `PromptSistema` + selects multi-selección de Fuentes, Herramientas, Workflows, Roles, Usuarios.
- **`Asistentes/Dashboard`**: tarjetas de estadísticas (total, publicados, en prueba, borradores, deshabilitados, versiones acumuladas) + tabla resumen.
- **`Asistentes/Versiones`**: historial de versiones con botón "Nueva Versión".
- **Chat**: el selector de agentes usa `/api/agentes/autorizados/{idUsuario}` (solo muestra agentes autorizados al usuario).

---

## 7. Reglas de negocio cubiertas

| Regla | Implementación |
|-------|----------------|
| 1. Permisos por rol/usuario | `AutorizacionService.VerificarAsistenteAsync` + endpoint autorizados |
| 2. RAG solo fuentes asignadas | Filtro por `AsistentesFuentes` en recuperación |
| 7. Activar/desactivar | `Activar/DesactivarAsistenteAsync` |
| 10. Eliminación lógica | `DELETE` desactiva, no borra físicamente |
| Versionado | `AgenteVersion` + `CrearVersionAsync` |

---

## 8. Verificación

- `dotnet build` de API, Web, Application, Infrastructure, Tests: **0 errores**.
- `dotnet test`: **181 pruebas pasan** (170 existentes + 11 nuevas de ETAPA 16).
- Migraciones EF aplicadas a `asistentesql` (tablas `AgentesWorkflows`, `AgentesRoles`, `AgentesVersiones`, columnas `Asistente.Codigo/Objetivo/PromptSistema/Estado/Version`, `AuditoriaIA.VersionAgente`).
- **Pendiente E2E en Docker:** requiere Docker Desktop levantado (Ollama en CPU es el bloqueador conocido para la respuesta del LLM; la autorización y el versionado son deterministas y están cubiertos por tests).
