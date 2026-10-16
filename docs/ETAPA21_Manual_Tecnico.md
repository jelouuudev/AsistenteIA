# ETAPA 21 — Manual técnico: Memoria Empresarial Avanzada

## 1. Arquitectura

```
Agentes IA / Planner (Tool: MemoryTool) ─┐
Agent Runtime / Workflow Engine           ├─> MemoryOrchestrator (contexto por ámbito)
                                          │
MemoryManager (CRUD+versionado+permisos+auditoría)
  ├─> SemanticMemorySearch ─> IVectorStore (ChromaDB, colección compartida)
  │                            + IEmbeddingProvider (mismo que RAG)
  ├─> MemoryRetentionEngine (vencimiento + purga)
  └─> IAutorizacionService (permiso MEMORIA:{Codigo})
```

## 2. Componentes (código)

| Pieza | Archivo |
|---|---|
| Entidades | `Asistente.Domain/Entities/Memory.cs` (Scope, Category, Record, Version, AccessLog, RetentionPolicy) |
| Contratos repos | `Asistente.Application/Interfaces/IMemoryRepositories.cs` |
| EF | `.../Data/Configurations/MemoryConfigurations.cs`, DbSets, migración `Etapa21_MemoriaEmpresarial` |
| Manager | `.../Services/Memoria/MemoryManager.cs` |
| Orchestrator | `.../Services/Memoria/MemoryOrchestrator.cs` (contexto Workflow>Agente>Usuario>Org, con tope) |
| Búsqueda | `.../Services/Memoria/SemanticMemorySearch.cs` |
| Retención | `.../Services/Memoria/MemoryRetentionEngine.cs` |
| Tool | `.../Services/Herramientas/MemoryTool.cs` (Código `MemoryTool`) |
| API | `Asistente.API/Controllers/MemoriasController.cs` (`api/memorias`) |
| Web | `Asistente.Web/Controllers/MemoriasController.cs` + `Views/Memorias/` + menú |
| DI | `Asistente.Application/DependencyInjection.cs` + `Asistente.Infrastructure/DependencyInjection.cs` |
| SQL | `scripts/etapa21_memoria.sql` (idempotente) |
| Tests | `Asistente.Tests/Services/MemoryManagerTests.cs` (16 pruebas con fakes) |

## 3. Decisiones

- **Vector compartido, filtro por permiso después**: cada memoria se indexa con Guid propio y marcador `MEMORIA:{codigo}`; al buscar se filtra a esos Guids y LUEGO se exige permiso. Sin vector (Chroma caído) degrada a texto sin romper.
- **Versionado append-only**: crear→v1; actualizar/revertir agregan versiones; jamás se reescribe.
- **Permisos**: `RequierePermiso` exige `MEMORIA:{Codigo}`; el propietario exime en su ámbito Usuario. Denegado no expone contenido (403 + auditoría).
- **Retención**: vencimiento explícito o por días de la política (ámbito > global); expirar desactiva, purgar elimina vector+registro dejando el evento con el código. Lazy en lectura + `POST retencion/aplicar` explícito.
- **Auditoría total**: Crear/Leer/Actualizar/Eliminar/Buscar/Revertir/Expirar/Purgar con usuario, agente, resultado y motivo.
- **La auditoría nunca tumba**: todo `AddAsync` de logs va en try/catch.
- **Forbid con mensaje es un bug**: `Forbid(string)` lo toma como *scheme* y da 500/400; usar `StatusCode(403, ...)`.
