# Manual Técnico — ETAPA 13: Motor de Eventos Empresariales y Automatización Proactiva

## 1. Objetivo

Implementar un **Motor de Eventos Empresariales** que detecta eventos del sistema, evalúa
reglas de automatización configurables y ejecuta automáticamente *workflows*, herramientas o
notificaciones **sin intervención directa del usuario**. El motor garantiza seguridad,
trazabilidad (auditoría completa) y control de las acciones ejecutadas.

Se integra de forma nativa con el **Workflow Engine (ETAPA 12)** reutilizando su orquestación,
autorización y auditoría de herramientas.

## 2. Stack

- **ASP.NET Core 8** (C#), **Clean Architecture**, **SOLID**, **async/await**.
- **Entity Framework Core 8** + **SQL Server** (Windows Auth).
- **Quartz.NET 3.8** para la programación de tareas periódicas (expresión Cron).
- **BackgroundService** para el procesamiento desacoplado de eventos.
- **Ollama** + DeepSeek-R1-Distill-Qwen-7B (respuesta final del asistente, si aplica).
- **FluentValidation**, **Serilog**, **DTO**, **Inyección de dependencias**.

## 3. Modelo de Datos (nuevas entidades)

| Entidad | Responsabilidad |
|---|---|
| `EventoEmpresarial` | Catálogo de eventos: IdEvento, Codigo (único), Nombre, Descripcion, Categoria, Activo, UsuarioCreacion. |
| `ReglaEvento` | Asocia un evento a un workflow: IdRegla, IdEvento, IdWorkflow, Condicion (opcional), Prioridad, Activa. |
| `EventoProcesado` | Auditoría de cada ejecución: IdEventoProcesado, IdEvento, FechaHora, Estado, Resultado, TiempoProcesamiento, IdRegla, IdWorkflow, IdUsuario. |
| `TareaProgramada` | Tarea Cron: IdTarea, Nombre, ExpresionCron, IdWorkflow, Activa, UltimaEjecucion, ProximaEjecucion, UsuarioCreacion. |
| `ConfiguracionEventoMotor` | Políticas globales (1 registro): ReintentosMaximos, IntervaloReintentoMs, TiempoMaximoEventoMs, EventosSimultaneosMax, FrecuenciaProcesadorMs. |
| `EstadoEvento` (enum) | Pendiente=0, EnProceso=1, Completado=2, Error=3, Reintentando=4. |

Migraciones aplicadas: `Etapa13_EventosEmpresariales` y `Etapa13_EventoProcesado_Workflow`.

## 4. Arquitectura (capas)

- **Domain** (`Asistente.Domain`): entidades, enums e interfaces de repositorio
  (`IEventoEmpresarialRepository`, `IReglaEventoRepository`, `IEventoProcesadoRepository`,
  `ITareaProgramadaRepository`, `IConfiguracionEventoMotorRepository`).
- **Application** (`Asistente.Application`): servicios `EventoEmpresarialService`,
  `ReglaEventoService`, `EventoProcesadoService`, `TareaProgramadaService`,
  `ConfiguracionEventoMotorService`, `MonitoreoEventosService` y el núcleo
  `EventoMotorService` (evaluación de reglas + ejecución + reintentos + auditoría).
- **Infrastructure** (`Asistente.Infrastructure`): repositorios EF, EF Configurations,
  `ProcesadorEventosBackgroundService` (desacoplado), `ProgramadorTareasBackgroundService`
  (programa jobs Quartz al arrancar) y `TareaProgramadaJob` (job Quartz que ejecuta el workflow).
- **API** (`Asistente.API`): controladores REST bajo `/api/...`.
- **Web** (`Asistente.Web`): MVC administrativo bajo `/EventosEmpresariales`, `/ReglasEvento`,
  `/TareasProgramadas`, `/MonitoreoEventos`.

## 5. Flujo de procesamiento (desacoplado)

```
Evento Empresarial ──disparar──▶ EventoProcesado (Pendiente) ──cola──▶
ProcesadorEventosBackgroundService ──▶ Evaluador de Reglas ──▶ Workflow Engine
──▶ Tool Orchestrator ──▶ Herramientas ──▶ Resultado ──▶ Auditoría (EventoProcesado)
```

1. Un evento se dispara (manualmente vía API/Web, o programado vía Quartz) llamando a
   `IEventoMotorService.DispararEventoAsync(codigo)`. Esto crea un `EventoProcesado` en
   estado **Pendiente** y lo deja encolado.
2. El `ProcesadorEventosBackgroundService` (cada `FrecuenciaProcesadorMs`, por defecto 2 s)
   consulta los `EventosProcesados` en estado `Pendiente`/`EnProceso`/`Reintentando` (respetando
   `EventosSimultaneosMax`) y llama `EventoMotorService.ProcesarEventoAsync`.
3. `ProcesarEventoAsync` evalúa las reglas activas ordenadas por prioridad, verifica la
   `Condicion` de cada una y, si aplica, ejecuta el workflow asociado vía `IWorkflowEngine`
   con `confirmado: true` (la ejecución automática no requiere confirmación humana).
4. La política de reintentos (`ReintentosMaximos` de `ConfiguracionEventoMotor`) se aplica por
   cada regla; si el workflow falla se reintenta hasta agotar intentos, marcando el estado
   `Reintentando` entre intentos y `Error` al agotarlos.
5. Al finalizar se registra `Estado` (`Completado`/`Error`) y `TiempoProcesamiento` (ms) en
   `EventoProcesado` (auditoría).

## 6. Evaluación de condiciones

`ReglaEvento.Condicion` es una expresión simple y segura que se evalúa contra el contexto del
evento (JSON en `EventoProcesado.Resultado`). Soporta: `==`, `!=`, `>=`, `<=`, `>`, `<` y el
separador `|` (Y lógico). Ejemplo: `Categoria == 'Error' | Prioridad >= 3`. Si la condición
está vacía, la regla siempre aplica.

## 7. Programación de tareas (Quartz.NET)

`ProgramadorTareasBackgroundService` registra al arranque un `IJob` (``TareaProgramadaJob``)
por cada `TareaProgramada` activa, usando su `ExpresionCron`. El job ejecuta el workflow
asociado y actualiza `UltimaEjecucion`/`ProximaEjecucion`. Ejemplos de expresiones:
`0 0 2 * * ?` (diario 2 AM), `0 0/30 * * * ?` (cada 30 min).

## 8. Reintentos y notificaciones

- Número máximo de reintentos, intervalo y estado final configurables en
  `ConfiguracionEventoMotor` (administrable desde el Web).
- Al fallar, el incidente queda registrado en `EventoProcesado` (Estado=`Error`, Resultado con
  el mensaje) y es visible en el panel de monitoreo (alarga de errores).

## 9. Seguridad y auditoría

- Todos los endpoints requieren JWT (`[Authorize]`); las acciones de administración exigen rol
  `Administrador` y el monitoreo rol `Administrador`/`Supervisor`.
- Cada ejecución automática queda registrada en `EventosProcesados` (evento, regla, workflow,
  usuario cuando aplica, resultado y tiempo) — trazabilidad completa para auditoría.
- El núcleo es independiente del transporte: preparado para incorporar colas distribuidas,
  webhooks y mensajería empresarial en etapas futuras sin modificar `EventoMotorService`.

## 10. Pruebas

- `EventoMotorServiceTests` (xUnit + Moq): disparo/enqueue, procesamiento automático,
  evaluación de condiciones, política de reintentos (éxito tras reintentos y agotamiento).
- Configuración vía `dotnet test Asistente.Tests`.
