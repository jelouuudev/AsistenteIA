# ETAPA 16 — Plataforma Multiagente: Documento de Arquitectura

**Versión:** 2.0 — Evolución de V1.0 (Asistente IA → Plataforma Multiagente)

---

## 1. Evolución del flujo

### Versión 1.0
```
Usuario → Asistente IA → [RAG | Tools | SQL | Workflows]
```

### Versión 2.0 (ETAPA 16)
```
Usuario → Agent Manager → [Agente Soporte | Agente Comercial | Agente RRHH | Agente BD]
                              ↓
                       Agent Runtime → [RAG | Tools | Workflows] → Ollama → Modelo IA
```

---

## 2. Componentes

### 2.1 Agent Manager
- Servicio de administración (`AsistenteService`).
- Responsable de CRUD, versionado, publicación, prueba y duplicación de agentes.
- Valida unicidad de `Codigo`.

### 2.2 Agent Runtime
- `ChatService` actúa como el runtime: carga la configuración del agente seleccionado, valida permisos y ejecuta RAG/Tools/Workflows.
- Respeta el **prompt propio del agente** (`PromptSistema`).

### 2.3 Autorización (Regla 1)
- `AutorizacionService.VerificarAsistenteAsync`:
  - Administrador → acceso total.
  - Usuario asignado directamente al agente → acceso.
  - Usuario con rol asignado al agente → acceso.
- El chat filtra los agentes visibles por `/api/agentes/autorizados/{idUsuario}`.

### 2.4 Persistencia
- `AsistenteDbContext` con `AgentesWorkflows`, `AgentesRoles`, `AgentesVersiones`.
- `AuditoriaIA.VersionAgente` para trazabilidad.

---

## 3. Patrón de asignaciones

Cada agente tiene listas de asignación:
- **Fuentes** (`AsistentesFuentes`) → RAG restringido (Regla 2).
- **Herramientas** (`AsistentesHerramientas`) → ToolOrchestrator.
- **Workflows** (`AgentesWorkflows`) → WorkflowEngine.
- **Roles** (`AgentesRoles`) → autorización por rol.
- **Usuarios** (`UsuariosAsistentes`) → autorización directa.

---

## 4. Ciclo de vida y versionado

```
Borrador ──(enviar prueba)──> Prueba ──(publicar)──> Publicado
   ↑                                              │
   └────────────────(desactivar)──────────────────┘
                                                Deshabilitado
```

Cada `CrearVersionAsync` incrementa `Version` y snapshot en `AgentesVersiones`.

---

## 5. Relación con ETAPA 17 (Agent Orchestrator)

La ETAPA 16 es la base: expone agentes especializados, versionados y autorizados.
La ETAPA 17 (Agent Orchestrator) podrá coordinar múltiples agentes para tareas complejas, apoyándose en:
- `ObtenerAutorizadosParaUsuarioAsync` (selección de agentes válidos).
- `AgenteVersion` (ejecución determinista por versión).
- Auditoría con `IdAsistente` + `VersionAgente`.

---

## 6. Stack tecnológico

- **Backend:** ASP.NET Core 8, C#, EF Core 8, arquitectura limpia (Domain/Application/Infrastructure/API/Web).
- **IA:** Ollama (modelos locales, ej. `deepseek-r1:7b`), ChromaDB para RAG.
- **BD:** SQL Server (`asistentesql`).
- **Frontend:** MVC + Bootstrap 5 + JavaScript.
- **Contenedores:** Docker Compose (`--profile prod`).
