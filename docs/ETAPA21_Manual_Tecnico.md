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
| Cifrado | `Asistente.Domain/Interfaces/IMemoryCifrador.cs` + `Asistente.Infrastructure/Services/MemoryCifrador.cs` |
| Panel | `.../Services/Memoria/DashboardMemoriaService.cs` + `Asistente.Shared/DashboardMemoriaDto.cs` + `Views/Memorias/Dashboard.cshtml` |
| API | `Asistente.API/Controllers/MemoriasController.cs` (`api/memorias`) |
| Web | `Asistente.Web/Controllers/MemoriasController.cs` + `Views/Memorias/` + menú |
| DI | `Asistente.Application/DependencyInjection.cs` + `Asistente.Infrastructure/DependencyInjection.cs` |
| SQL | `scripts/etapa21_memoria.sql` (idempotente) |
| Tests | `Asistente.Tests/Services/MemoryManagerTests.cs` (36 pruebas con fakes), `MemoryCifradorTests.cs` (8), `MemoryBackfillCifradoTests.cs` (3), `Web/MemoriasControllerRolesTests.cs` (5), `Web/MemoriasControllerCrearTests.cs` (2) |

## 3. Decisiones

- **Vector compartido, filtro por permiso después**: cada memoria se indexa con Guid propio y marcador `MEMORIA:{codigo}`; al buscar se filtra a esos Guids y LUEGO se exige permiso. Sin vector (Chroma caído) degrada a texto sin romper.
- **La frontera la impone el almacén, no cada consumidor** (`MemorySafeVectorStore`): como documentos y memorias comparten colección, y el RAG solo autoriza por documento, un vector de memoria devuelto por una consulta de RAG saltaría el permiso `MEMORIA:{codigo}` y llegaría al prompt sin autorizar. Que la exclusión esté en `IVectorStore` y no en cada servicio evita que un consumidor nuevo la reabra en silencio: solo `SemanticMemorySearch` recibe el almacén CRUDO, como excepción explícita en el DI.
- **Versionado append-only**: crear→v1; actualizar/revertir agregan versiones; jamás se reescribe.
- **Permisos**: el acceso a una memoria lo decide su **ámbito**, no un permiso por código. `Usuario`, `Agente` y `Workflow` solo la ven su dueño (y el dueño de un ámbito Usuario queda exento de pedir permiso); `Organizacion` es compartida. Escribir en los ámbitos compartidos exige `MEMORIA_ADMINISTRAR` ("la memoria organizacional no podrá modificarse sin autorización"). Denegado no expone contenido (403 + auditoría).
- **`RequierePermiso` es un flag de nivel API, y la UI no lo ofrece**: la API puede crear una memoria reservada (uso programático, donde el permiso `MEMORIA:{código}` ya está concedido por otra vía) y por defecto protege cuando no se indica. El formulario web, en cambio, **crea siempre memorias abiertas** y no expone la casilla: el catálogo de permisos es fijo y no admite crear `MEMORIA:{código}`, así que una memoria creada con esa exigencia quedaba invisible e indestructible para todos, incluido el administrador, sin ninguna salida desde la app. Por eso el Web fuerza el valor en vez de omitirlo: omitirlo haría que la API lo protegiera por defecto.
- **Retención**: vencimiento explícito o por días de la política (ámbito > global); expirar desactiva, purgar elimina vector+registro dejando el evento con el código. Lazy en lectura + `POST retencion/aplicar` explícito.
- **Auditoría total**: Crear/Leer/Actualizar/Eliminar/Buscar/Revertir/Expirar/Purgar con usuario, agente, resultado y motivo.
- **La auditoría nunca tumba**: todo `AddAsync` de logs va en try/catch.
- **Forbid con mensaje es un bug**: `Forbid(string)` lo toma como *scheme* y da 500/400; usar `StatusCode(403, ...)`.

## 4. Cifrado del contenido (seccion 16)

El contenido de `MemoryRecords` y `MemoryVersions` se guarda cifrado con AES-256. La columna no cambia (`nvarchar(max)`), así que la migración que lo activa no toca el esquema.

**Dónde está el punto único.** En el conversor de valor de EF Core (`AsistenteDbContext.CifrarContenidoDeMemoria`): cada lectura materializa ya descifrado y cada escritura llega cifrada a la columna. No en las llamadas de `MemoryManager`, que tiene diez puntos de lectura y escritura repartidos entre Manager, Orquestador, VersionManager y el panel: saltarse uno deja contenido en claro **sin que nada falle ni avise**, que es exactamente el tipo de hueco que este proyecto ya ha tenido.

**Formato.** Los valores llevan el prefijo `enc:v1:`. `Descifrar` devuelve sin tocar cualquier valor que no empiece por él, así que los contenidos anteriores al cifrado se siguen pudiendo leer y no rompen el arranque.

**Backfill.** `DbInitializer.CifrarContenidoPreexistenteAsync` reescribe las filas que quedan en claro. Tres detalles que no son opcionales:

- La detección va por **SQL crudo con `{0}`**: un LINQ sobre `Contenido` no sirve porque EF aplica el mismo conversor al argumento de la consulta que a la columna, así que el predicado viaja **cifrado** y compara `enc:v1:%` contra `enc(v1:...)`. Con el `NOT` de la detección eso no da cero filas, da **todas**: se re-cifraría la memoria entera en cada arranque. Solo se buscan los Id y luego se cargan por Id.
- Ese SQL tiene que pasar el valor por parámetro (`SqlQueryRaw(sql, prefijo + "%")`), no por interpolación. Un `"... NOT LIKE '{prefijo}%'"` hace que EF deje el marcador *dentro* de las comillas y el SQL acaba siendo `NOT LIKE '@p0%'`: texto literal que no casa con nada, con el mismo resultado de antes (todo seleccionado, todo reescrito) y con el aviso `EF1002` de fondo, que justamente avisa que ahí no parametriza.
- Se marca la propiedad como modificada y se deja que guarde el conversor. Escribir el texto a mano saltaría el mismo punto único.

Idempotente: al volver a arrancar no queda ninguna fila sin prefijo y no escribe nada.

**La clave no está en el código.** Sale de `Memoria:ClaveCifrado` (env `MEMORIA_CLAVE_CIFRADO`) y, si no existe, se cae sobre `JwtSettings:SecretKey`. Si no hay ninguna, el servicio **falla al construirse** en vez de guardar en claro.

> **NUNCA cambiar la clave una vez cifrada la memoria.** No destruye datos, pero deja el contenido ilegible: solo se recupera con la clave anterior o desde un respaldo. El error lo dice explícitamente.

**IV aleatorio.** A diferencia de `ConexionCifrador` y `CredencialCifrador`, que derivan el IV de la clave y usan el mismo siempre, aquí cada operación genera su IV y viaja pegado al cifrado. Reutilizar el IV con contenido de memoria dejaría adivinar: esta tabla llena de "Si"/"No" mostraria que dos filas son iguales.

## 5. Panel de memoria (secciones 7, 13 y 17)

`GET /api/memorias/dashboard` y `/Memorias/Dashboard`. Compone las tres cosas que antes había que pedir una a una: métricas, rastro de accesos y retención pendiente. No tiene repositorios propios ni copia de los contadores: usa `MemoryManager` y `MemoryRetentionEngine`.

| Zona | Contenido |
|---|---|
| Tarjetas | activas, versiones, accesos, consultadas, tiempo de recuperación, expiradas, pendientes de purgar, accesos recientes |
| Desgloses | memorias por ámbito, uso por agente, uso por usuario, accesos por tipo |
| Retención | códigos que el próximo barrido apagaría |
| Auditoría | últimos 50 accesos con fecha, operación, memoria, tipo, usuario, agente, resultado y duración |

**Permisos.** `AUDITORIA_CONSULTAR` en la API; `Administrador,Supervisor` en la Web. No exige `MEMORIA:{codigo}` por fila: quien llega ya superó el permiso de auditoría, y exigir además el de cada memoria dejaría la tabla en blanco salvo para el dueño de todo.

**Roles por acción, no por controlador.** Para que Supervisor viera el panel, el rol de `MemoriasController` (Web) pasó de estar en la clase a cada acción. En ASP.NET los `[Authorize]` se combinan en AND, así que un rol fijo a nivel de clase impide que ninguna acción se abra a otro rol. La matriz está fijada por `Asistente.Tests/Web/MemoriasControllerRolesTests.cs`, que además falla si alguien añade una acción sin declararle rol.

**Índice.** `IX_MemoryAccessLogs_Fecha`: el panel pide los N accesos más recientes de TODAS las memorias sin filtrar, y la única columna por la que ordena es `Fecha`.
