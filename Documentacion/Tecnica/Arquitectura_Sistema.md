# =============================================================================
# DOCUMENTACIÓN TÉCNICA - ARQUITECTURA DEL SISTEMA
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 24
# =============================================================================

## 1. ARQUITECTURA GENERAL

### 1.1 Diagrama de Arquitectura

```
                          USUARIO
                             │
                             ▼
                     ┌───────────────┐
                     │    WEB APP    │
                     │ ASP.NET Core  │
                     │   Bootstrap 5 │
                     └───────┬───────┘
                             │
                             ▼
                     ┌───────────────┐
                     │ CHAT / API    │
                     │  ASP.NET Core │
                     │      8        │
                     └───────┬───────┘
                             │
                             ▼
                     ┌───────────────┐
                     │AI ORCHESTRATOR│
                     │  ChatService  │
                     └───────┬───────┘
                             │
         ┌───────────────────┼───────────────────┐
         ▼                   ▼                   ▼
    ┌──────────┐      ┌──────────┐       ┌──────────┐
    │   RAG    │      │  TOOLS   │       │ WORKFLOW │
    │          │      │          │       │          │
    └────┬─────┘      └────┬─────┘       └────┬─────┘
         │                 │                  │
         ▼                 ▼                  ▼
    ┌──────────┐      ┌──────────┐       ┌──────────┐
    │ ChromaDB │      │SQL Server│       │  EVENT   │
    │          │      │          │       │  ENGINE  │
    └──────────┘      └──────────┘       └──────────┘
         │                 │                  │
         └─────────────────┼──────────────────┘
                           │
                           ▼
                    ┌──────────────┐
                    │   OLLAMA     │
                    │  DeepSeek    │
                    └──────────────┘
                           │
                           ▼
                    ┌──────────────┐
                    │  AUDITORÍA   │
                    │    LOGS      │
                    │  MÉTRICAS    │
                    └──────────────┘
```

### 1.2 Componentes

| Componente | Tecnología | Puerto | Descripción |
|---|---|---|---|
| Web App | ASP.NET Core 8, Bootstrap 5 | 5206 | Interfaz de usuario |
| API | ASP.NET Core 8, JWT | 5298 | Lógica de negocio |
| SQL Server | Microsoft SQL Server 2022 | 1433 | Base de datos |
| Ollama | Ollama + deepseek-r1:7b | 11434 | Modelo de IA local |
| ChromaDB | ChromaDB | 8000 | Base vectorial |

### 1.3 Capas de la Arquitectura

#### Capa de Presentación
- **Asistente.Web**: Aplicación MVC con Bootstrap 5
- Vistas: Chat, Documentos, Workflows, Eventos, Configuración
- Controladores: ChatController, DocumentosController, etc.

#### Capa de Aplicación
- **Asistente.API**: API REST con autenticación JWT
- Servicios: ChatService, QueryEmpresarialService, WorkflowEngine
- Orquestadores: ToolOrchestrator, EventoMotorService

#### Capa de Dominio
- **Asistente.Domain**: Entidades y reglas de negocio
- Entidades: Usuario, Rol, Documento, Workflow, Evento
- Interfaces: IUsuarioRepository, IChatService, etc.

#### Capa de Infraestructura
- **Asistente.Infrastructure**: Acceso a datos y servicios externos
- Repositories: UsuarioRepository, DocumentoRepository
- Servicios: OllamaService, ChromaService, SqlQueryExecutor

### 1.4 Flujo de Información

1. Usuario → Web → API → ChatService
2. ChatService → RAG (ChromaDB) + Tools (SQL) + Workflow
3. ChatService → Ollama (DeepSeek) → Respuesta
4. ChatService → Auditoría → SQL Server

### 1.5 Seguridad en Capas

```
Usuario → Autenticación (JWT) → Autorización (Roles/Permisos)
       → Asistente autorizado → Fuentes autorizadas
       → Herramientas autorizadas → Workflow autorizado
       → Ejecución → Auditoría
```

## 2. TECNOLOGÍAS UTILIZADAS

| Categoría | Tecnología | Versión |
|---|---|---|
| Backend | ASP.NET Core | 8.0 |
| Frontend | ASP.NET MVC + Bootstrap | 5.3 |
| Base de Datos | SQL Server | 2022 |
| IA | Ollama + DeepSeek | r1:7b |
| RAG | ChromaDB | 0.4.x |
| Logging | Serilog | 3.x |
| Automatización | Quartz.NET | 3.8.x |
| Contenedores | Docker | 24.x |
| ORM | Entity Framework Core | 8.0 |
| PDF | PdfPig + QuestPDF | 0.1.x |
| Validaciones | FluentValidation | 11.x |

## 3. DEPENDENCIAS

### 3.1 NuGet Packages
- Microsoft.EntityFrameworkCore.SqlServer (8.0.x)
- Microsoft.AspNetCore.Authentication.JwtBearer (8.0.x)
- Quartz.Extensions.Hosting (3.8.x)
- Serilog.AspNetCore (8.0.x)
- FluentValidation (11.x)
- Dapper (2.x)
- QuestPDF (2023.x)
- PdfPig (0.1.x)

### 3.2 Servicios Externos
- Ollama (http://localhost:11434)
- ChromaDB (http://localhost:8000)
- SQL Server (localhost:1433)

## 4. CONFIGURACIÓN

### 4.1 Connection Strings
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AsistenteIA;User Id=sa;Password=AsistenteSQL2026;TrustServerCertificate=true;"
  }
}
```

### 4.2 JWT Settings
```json
{
  "JwtSettings": {
    "SecretKey": "AsistenteIA_SuperSecretKey_2024_MustBeAtLeast32Chars!",
    "Issuer": "AsistenteIA",
    "Audience": "AsistenteIA_Users",
    "ExpiryMinutes": 480
  }
}
```

### 4.3 Ollama Settings
```json
{
  "Ollama": {
    "BaseUrl": "http://ollama:11434",
    "Modelo": "deepseek-r1:7b",
    "Temperature": 0.3,
    "MaxTokens": 8192
  }
}
```

## 5. BASE DE DATOS

### 5.1 Diagrama ER (Resumen)

```
Usuario 1──* UsuarioRol *──1 Rol
Rol 1──* RolPermisos *──1 Permisos
Usuario 1──* UsuarioAsistentes *──1 Asistente
Asistente 1──* AsistentesHerramientas *──1 Herramientas
Asistente 1──* AsistenteFuente *──1 FuenteConocimiento
Documento 1──* DocumentoVersion
Documento 1──* DocumentoChunk
Documento 1──* DocumentoFuente
Workflow 1──* WorkflowPasos
Workflow 1──* WorkflowEjecuciones
EventoEmpresarial 1──* ReglasEvento
EventoEmpresarial 1──* EventosProcesados
```

### 5.2 Tablas Principales

| Tabla | Descripción |
|---|---|
| Usuario | Usuarios del sistema |
| Rol | Roles (Administrador, Operador, Supervisor, Usuario) |
| Permisos | Permisos por módulo |
| Asistente | Agentes de IA |
| Herramientas | Herramientas disponibles |
| Documento | Documentos del gestor documental |
| FuenteConocimiento | Fuentes de conocimiento RAG |
| Workflow | Flujos de trabajo |
| EventosEmpresariales | Eventos del sistema |
| AuditoriaActividad | Registro de actividades |
| MetricasIA | Métricas de IA |

### 5.3 Índices Recomendados
- IX_Usuario_Usuario (Único)
- IX_Rol_Nombre (Único)
- IX_Asistente_Codigo (Único)
- IX_Documento_Codigo (Único)
- IX_Workflow_Codigo (Único)
- IX_Evento_Codigo (Único)
- IX_Auditoria_Fecha (Para consultas por rango)

## 6. RAG (Retrieval-Augmented Generation)

### 6.1 Configuración
- Modelo de embeddings: nomic-embed-text
- Tamaño de chunk: 500 tokens
- Overlap: 50 tokens
- Top-K: 5 documentos
- Similarity threshold: 0.7

### 6.2 Flujo
1. Usuario pregunta → ChatService
2. ChatService → RagService (ChromaDB)
3. ChromaDB → Embeddings + Búsqueda semántica
4. RagService → Contexto relevante
5. ChatService → Ollama (con contexto)
6. Ollama → Respuesta final

## 7. TOOLS (Herramientas)

### 7.1 Herramientas Disponibles
| Herramienta | Descripción | Permiso |
|---|---|---|
| DocumentSearchTool | Búsqueda en RAG | HERRAMIENTAS_CONSULTAR |
| SqlQueryTool | Consultas SQL | SQL_CONSULTAR |
| CalculatorTool | Operaciones matemáticas | HERRAMIENTAS_CONSULTAR |
| DateTimeTool | Fecha y hora | HERRAMIENTAS_CONSULTAR |
| ReportTool | Generación de reportes | HERRAMIENTAS_CONSULTAR |

### 7.2 Flujo de Ejecución
1. ChatService → ToolOrchestrator
2. ToolOrchestrator → ValidarPermisos
3. ToolOrchestrator → Ejecutar herramienta
4. ToolOrchestrator → Registrar auditoría
5. ToolOrchestrator → Retornar resultado

## 8. WORKFLOWS

### 8.1 Estructura
- Workflow: Define el flujo
- WorkflowPasos: Pasos secuenciales
- WorkflowEjecuciones: Historial de ejecuciones
- WorkflowPasosEjecucion: Resultado de cada paso

### 8.2 Estrategias de Error
- Cancelar: Detener el flujo
- Omitir: Continuar al siguiente paso
  (RegistrarIncidencia se eliminó por ser idéntica a Omitir sin registrar nada.)

## 9. EVENTOS

### 9.1 Tipos de Eventos
- Internos: DocumentoCargado, UsuarioCreado, etc.
- Programados: Tareas periódicas con Quartz.NET

### 9.2 Flujo
1. Evento ocurre → EventoMotorService
2. EventoMotorService → Evaluar reglas
3. EventoMotorService → Ejecutar workflow asociado
4. EventoMotorService → Registrar en EventosProcesados

## 10. SEGURIDAD

### 10.1 Autenticación
- JWT (JSON Web Tokens)
- Expiración: 480 minutos (8 horas)
- Algoritmo: HMAC-SHA256

### 10.2 Autorización
- Roles: Administrador, Operador, Supervisor, Usuario
- Permisos por módulo (17 permisos)
- Validación en cada capa

### 10.3 Protección de Datos
- Contraseñas: PBKDF2 HMAC-SHA256 (100k iteraciones)
- Connection strings: Cifradas
- JWT Secret: Variable de entorno en producción

## 11. MONITOREO

### 11.1 Logs
- Serilog con salida a consola y archivo
- Nivel: Information
- Archivo: /app/logs/log-.txt

### 11.2 Métricas
- Tiempo de respuesta
- Cantidad de consultas
- Uso de herramientas
- Errores

## 12. CONTENEDORES DOCKER

| Contenedor | Imagen | Puerto |
|---|---|---|
| asistenteweb | asistenteia-web | 5206 |
| asistenteapi | asistenteia-api | 5298 |
| asistentesql | mcr.microsoft.com/mssql/server:2022-latest | 1433 |
| asistenteollama | ollama/ollama:latest | 11434 |
| asistentechroma | chromadb/chroma:latest | 8000 |
