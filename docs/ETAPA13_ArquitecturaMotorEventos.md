# Documento de Arquitectura — Motor de Eventos Empresariales (ETAPA 13)

## 1. Propósito

Este documento describe la arquitectura del **Motor de Eventos Empresariales**, su ubicación
dentro de la *Clean Architecture* del proyecto y los puntos de extensión previstos para etapas
futuras (colas distribuidas, webhooks, integración empresarial).

## 2. Principios

- **Clean Architecture / SOLID**: las dependencias apuntan hacia el Dominio; la lógica de
  negocio vive en `Application` y no depende de frameworks.
- **Desacoplamiento**: la *detección* de un evento está separada de su *procesamiento*. Disparar
  un evento solo encola un `EventoProcesado`; un `BackgroundService` lo procesa de forma
  asíncrona.
- **Reutilización**: el motor delega la ejecución de procesos en el **Workflow Engine (ETAPA 12)**,
  aprovechando su orquestación, autorización y auditoría de herramientas.
- **Trazabilidad**: toda ejecución automática queda registrada en `EventosProcesados`.
- **Asincronía e Inyección de Dependencias** en todas las capas.

## 3. Componentes

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Capa de Presentación                            │
│  Asistente.API (Controllers REST)   Asistente.Web (MVC Admin/Monitor)   │
└───────────────┬───────────────────────────────────┬────────────────────┘
                │ disparar / consultar                │ administrar
                ▼                                     ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       Capa de Aplicación                                │
│  EventoMotorService (núcleo)  •  Servicios de catálogo (Evento/Regla/   │
│  Tarea/Config/Monitoreo)  •  Interfaces de repositorio                 │
│        │ evalúa reglas, ejecuta workflow, reintenta, audita            │
│        ▼                                                                │
│  IWorkflowEngine ─────────────▶ Workflow Engine (ETAPA 12)            │
└───────────────┬───────────────────────────────────┬────────────────────┘
                │                                     │
                ▼                                     ▼
┌──────────────────────────────┐      ┌────────────────────────────────────┐
│ Infrastructure (Background)    │      │ Infrastructure (Repos / EF / Quartz) │
│ ProcesadorEventosBackgroundSvc │      │ Repositorios EF Core + DbContext      │
│ ProgramadorTareasBackgroundSvc │      │ TareaProgramadaJob (Quartz)           │
│                                │      │                                      │
└──────────────────────────────┘      └──────────────────────────────────────┘
```

## 4. Núcleo: `EventoMotorService`

Responsabilidades (sin acoplarse a transporte ni a UI):

- `DispararEventoAsync(codigo, contextoJson?, idUsuario?)`: crea `EventoProcesado` (Pendiente)
  y lo encola. **No** procesa inline (evita doble ejecución y conflictos de tracking).
- `ProcesarEventoAsync(id)`: evaluación de reglas (por prioridad), verificación de `Condicion`,
  ejecución del workflow (`IWorkflowEngine.EjecutarAsync(..., confirmado: true)`) y política de
  reintentos. Registra auditoría al final.

El servicio es **agnóstico al origen** del evento: puede venir de un controlador REST, de un
`BackgroundService` de Quartz, o (en el futuro) de una cola de mensajes.

## 5. Procesamiento desacoplado

`ProcesadorEventosBackgroundService` (hosted service) consulta periódicamente los eventos
pendientes y los procesa respetando la concurrencia máxima. Esto aporta:
- **Alta disponibilidad**: el disparo no bloquea al llamador.
- **Resiliencia**: un fallo en un evento no detiene a los demás.
- **Reintentos**: gestionados por el núcleo con backoff configurable.

## 6. Programación (Quartz.NET)

`ProgramadorTareasBackgroundService` registra un `TareaProgramadaJob` por cada tarea activa al
arranque, usando `ExpresionCron`. El job invoca `IEventoMotorService.DispararEventoAsync` (o
ejecuta directamente el workflow) y actualiza `UltimaEjecucion`/`ProximaEjecucion`. Esto cumple
el requisito de tareas periódicas (reindexación, limpieza, reportes) sin dependencias externas.

## 7. Modelo de datos y auditoría

Las entidades (`EventoEmpresarial`, `ReglaEvento`, `EventoProcesado`, `TareaProgramada`,
`ConfiguracionEventoMotor`) se mapean con EF Core. `EventosProcesados` es la tabla de auditoría
obligatoria: conserva evento, regla, workflow, usuario, resultado y tiempo de procesamiento.

## 8. No incluido (y puntos de extensión)

La etapa **no** incluye colas distribuidas (Kafka/RabbitMQ/Service Bus), webhooks ni ML para
reglas. El diseño ya está preparado para incorporarlos sin tocar el núcleo:

- **Colas distribuidas**: sustituir el "encolar en BD" de `DispararEventoAsync` por publicar en
  un *message broker*; el consumidor llamaría a `ProcesarEventoAsync`. La interfaz
  `IEventoMotorService` no cambia.
- **Webhooks**: añadir un paso de notificación al final de `ProcesarEventoAsync` (o una regla de
  tipo "Notificación") que POSTee el resultado a una URL configurada.
- **Mensajería empresarial**: el `EventoProcesado` ya es el contrato de salida; un adaptador
  externo puede suscribirse a la tabla (o a un evento de dominio) sin modificar el motor.

## 9. Calidad

- **FluentValidation** en DTOs de entrada.
- **Serilog** para trazas de disparo/procesamiento.
- **Pruebas unitarias** (`EventoMotorServiceTests`) cubren disparo, procesamiento automático,
  condiciones y reintentos.
