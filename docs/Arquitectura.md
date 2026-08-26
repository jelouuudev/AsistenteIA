# Documento de Arquitectura - Asistente Inteligente Empresarial

## 1. Visión General

El Asistente Inteligente Empresarial es una aplicación web basada en Clean Architecture que permite la comunicación entre usuarios y modelos de IA ejecutándose localmente mediante Ollama. La arquitectura está diseñada para ser escalable, mantenible y preparada para futuras integraciones.

## 2. Arquitectura por Capas

La solución sigue el patrón de Clean Architecture con separación clara de responsabilidades:

```
AsistenteIA.sln
├── Asistente.Web              (Presentación - MVC)
├── Asistente.API              (Presentación - Web API)
├── Asistente.Application      (Lógica de Aplicación)
├── Asistente.Domain           (Dominio - Entidades e Interfaces)
├── Asistente.Infrastructure   (Infraestructura - Datos y Servicios Externos)
├── Asistente.Shared           (Modelos Compartidos)
└── Asistente.Tests            (Pruebas Unitarias)
```

### 2.1 Asistente.Web (Presentación MVC)
- **Responsabilidad:** Interfaz de usuario web
- **Tecnologías:** ASP.NET Core MVC, Bootstrap 5, JavaScript (Fetch API)
- **Dependencias:** Asistente.Shared
- **Componentes:**
  - Controllers: ChatController, HomeController
  - Views: Chat/Index, Home/Index
  - Services: ApiService (comunicación con API)

### 2.2 Asistente.API (Presentación Web API)
- **Responsabilidad:** Endpoints REST para comunicación con el frontend
- **Tecnologías:** ASP.NET Core Web API, Swagger, Serilog, FluentValidation
- **Dependencias:** Asistente.Application, Asistente.Infrastructure
- **Componentes:**
  - Controllers: ChatController
  - Middleware: ExceptionMiddleware (manejo global de excepciones)
  - Configuration: appsettings.json (Development, Testing, Production)

### 2.3 Asistente.Application (Lógica de Aplicación)
- **Responsabilidad:** Casos de uso y coordinación de servicios
- **Tecnologías:** FluentValidation, DTOs
- **Dependencias:** Asistente.Domain, Asistente.Infrastructure
- **Componentes:**
  - Services: ChatService
  - DTOs: ChatRequestDto, ChatResponseDto, ConversationDto, MessageDto
  - Validators: ChatRequestValidator
  - Interfaces: IChatService

### 2.4 Asistente.Domain (Dominio)
- **Responsabilidad:** Entidades del dominio y contratos
- **Tecnologías:** C#, Entity Framework Core (solo interfaces)
- **Dependencias:** Ninguna (capa central)
- **Componentes:**
  - Entities: Conversacion, Mensaje
  - Enums: EstadoConversacion, RolMensaje
  - Interfaces: IConversacionRepository, IMensajeRepository, IUnitOfWork, IAIProvider

### 2.5 Asistente.Infrastructure (Infraestructura)
- **Responsabilidad:** Implementación de acceso a datos y servicios externos
- **Tecnologías:** Entity Framework Core, HttpClient, Serilog
- **Dependencias:** Asistente.Domain, Asistente.Shared
- **Componentes:**
  - Data: AsistenteDbContext, Migrations
  - Repositories: ConversacionRepository, MensajeRepository
  - Services: OllamaService, OllamaProvider
  - Configuration: OllamaConfig

### 2.6 Asistente.Shared (Modelos Compartidos)
- **Responsabilidad:** Modelos utilizados por múltiples capas
- **Tecnologías:** C#
- **Dependencias:** Ninguna
- **Componentes:**
  - Models: MensajeRequest, MensajeResponse, OllamaConfig

### 2.7 Asistente.Tests (Pruebas Unitarias)
- **Responsabilidad:** Pruebas de la capa Application
- **Tecnologías:** xUnit, Moq, FluentValidation
- **Dependencias:** Asistente.Application, Asistente.Domain, Asistente.Infrastructure
- **Componentes:**
  - Validators Tests: ChatRequestValidatorTests
  - Services Tests: ChatServiceTests, OllamaProviderTests

## 3. Principios de Diseño

### 3.1 SOLID
- **Single Responsibility:** Cada clase tiene una única responsabilidad
- **Open/Closed:** Abierto para extensión, cerrado para modificación
- **Liskov Substitution:** Las interfaces pueden ser sustituidas por implementaciones
- **Interface Segregation:** Interfaces específicas y pequeñas
- **Dependency Inversion:** Dependencia de abstracciones, no de implementaciones

### 3.2 Clean Architecture
- **Regla de Dependencia:** Las dependencias solo apuntan hacia adentro (hacia el dominio)
- **Separación de Capas:** Cada capa tiene una responsabilidad bien definida
- **Desacoplamiento:** Uso de interfaces para desacoplar implementaciones

### 3.3 Patrones Utilizados
- **Repository Pattern:** Abstracción del acceso a datos
- **Unit of Work:** Coordinación de transacciones
- **Dependency Injection:** Inyección de dependencias para desacoplamiento
- **DTO Pattern:** Separación de entidades de dominio de la API
- **Middleware Pattern:** Manejo transversal de excepciones
- **Factory Pattern:** Creación de servicios configurados

## 4. Flujo de Datos

### 4.1 Flujo de Conversación
```
Usuario (Web)
    ↓ HTTP POST /Chat/Enviar
Asistente.Web (ChatController)
    ↓ Fetch API
Asistente.API (ChatController)
    ↓ Validación (FluentValidation)
Asistente.Application (ChatService)
    ↓ IAIProvider.SendMessageAsync
Asistente.Infrastructure (OllamaProvider)
    ↓ HTTP POST
Ollama (localhost:11434)
    ↓ Respuesta
Asistente.Infrastructure (OllamaProvider)
    ↓
Asistente.Application (ChatService)
    ↓ Repositories (SQL Server)
Asistente.Infrastructure (ConversacionRepository, MensajeRepository)
    ↓
SQL Server (AsistenteIA)
    ↓
Asistente.Application (ChatService)
    ↓ DTO
Asistente.API (ChatController)
    ↓ JSON Response
Asistente.Web (ApiService)
    ↓
Usuario (Interfaz Web)
```

## 5. Configuración

### 5.1 Configuración por Ambientes
- **Development:** Logging en Debug, Swagger habilitado
- **Testing:** Logging en Information, Swagger deshabilitado
- **Production:** Logging en Warning, Swagger deshabilitado

### 5.2 Archivos de Configuración
- `appsettings.json`: Configuración base
- `appsettings.Development.json`: Configuración desarrollo
- `appsettings.Testing.json`: Configuración pruebas
- `appsettings.Production.json`: Configuración producción

### 5.3 Serilog Configuration
- **Console:** Salida en tiempo real
- **File:** Archivos rotativos diarios (Logs/log-.txt)
- **Retención:** 30 días de logs
- **Formato:** Timestamp, Level, Message, Exception

## 6. Manejo de Errores

### 6.1 Middleware de Excepciones
- **TimeoutException:** HTTP 408 Request Timeout
- **InvalidOperationException:** HTTP 400 Bad Request
- **UnauthorizedAccessException:** HTTP 401 Unauthorized
- **Exception genérica:** HTTP 500 Internal Server Error

### 6.2 Logging de Errores
- Todos los errores se registran en Serilog
- Incluye stack trace y contexto
- Mensajes de error claros para el usuario final
- Detalles técnicos ocultos en producción

## 7. Validaciones

### 7.1 FluentValidation
- **ChatRequestValidator:**
  - Mensaje: No vacío, mínimo 1 carácter, máximo 4000 caracteres
  - ConversationId: Si presente, debe ser mayor que 0

### 7.2 Ejecución
- Validaciones se ejecutan antes de la lógica de negocio
- Respuestas consistentes en formato JSON
- Mensajes de error claros y descriptivos

## 8. Pruebas Unitarias

### 8.1 Cobertura
- **Objetivo:** 70% cobertura en capa Application
- **Actual:** 100% (18/18 pruebas pasan)

### 8.2 Pruebas Implementadas
- **Validadores:** ChatRequestValidatorTests (7 pruebas)
- **Servicios:** ChatServiceTests (5 pruebas)
- **IA Provider:** OllamaProviderTests (6 pruebas)

### 8.3 Frameworks
- **xUnit:** Framework de pruebas
- **Moq:** Framework de mocking
- **FluentValidation:** Validación en pruebas

## 9. Preparación para Futuras Etapas

### 9.1 Autenticación
- Arquitectura preparada para agregar JWT o Identity
- Middleware de excepciones ya maneja UnauthorizedAccessException
- Interfaces listas para inyectar servicios de autenticación

### 9.2 Procesamiento de Documentos PDF
- Capa Application lista para agregar servicios de procesamiento
- Infrastructure puede extenderse con servicios de PDF
- DTOs pueden extenderse para incluir documentos

### 9.3 Búsqueda Vectorial
- IAIProvider permite agregar nuevos proveedores (ej. OpenAI, Azure OpenAI)
- Architecture soporta integración con bases vectoriales
- Domain puede extenderse con entidades de embeddings

### 9.4 Integración Avanzada con SQL Server
- Repositories ya implementados
- Unit of Work para transacciones
- Entity Framework Core configurado para migraciones

## 10. Tecnologías

### 10.1 Backend
- ASP.NET Core 8 Web API
- C# 12
- Entity Framework Core 8

### 10.2 Frontend
- ASP.NET Core MVC 8
- HTML5
- CSS3
- Bootstrap 5
- JavaScript (Fetch API)

### 10.3 Base de Datos
- Microsoft SQL Server 2022+

### 10.4 Inteligencia Artificial
- Ollama
- Modelo: deepseek-r1:7b

### 10.5 Logging
- Serilog
- Serilog.Sinks.Console
- Serilog.Sinks.File

### 10.6 Validación
- FluentValidation 12

### 10.7 Documentación
- Swagger/OpenAPI

### 10.8 Pruebas
- xUnit
- Moq

### 10.9 Control de Versiones
- Git

## 11. Convenciones de Código

### 11.1 Nomenclatura
- **Clases:** PascalCase (ej. ChatService)
- **Métodos:** PascalCase (ej. ProcesarMensajeAsync)
- **Propiedades:** PascalCase (ej. IdConversacion)
- **Campos privados:** _camelCase (ej. _logger)
- **Interfaces:** I + PascalCase (ej. IChatService)
- **Constantes:** PascalCase (ej. MaxLength)

### 11.2 Comentarios
- XML comments en clases públicas
- Comentarios inline para lógica compleja
- Documentación de XML para Swagger

### 11.3 Organización
- Un archivo por clase
- Carpetas por funcionalidad
- Namespaces consistentes con carpetas

## 12. Seguridad

### 12.1 Actual
- CORS configurado para permitir orígenes específicos
- Validación de entrada en todos los endpoints
- Manejo seguro de excepciones

### 12.2 Futuro
- Autenticación JWT
- Autorización por roles
- HTTPS obligatorio en producción
- Sanitización de entrada

## 13. Performance

### 13.1 Actual
- Programación asíncrona en todas las operaciones I/O
- HttpClient configurado con timeout
- Logging asíncrono con Serilog

### 13.2 Futuro
- Caching de respuestas frecuentes
- Optimización de consultas SQL
- Compresión de respuestas HTTP

## 14. Monitoreo

### 14.1 Actual
- Serilog para logging estructurado
- Logs rotativos diarios
- Retención de 30 días

### 14.2 Futuro
- Application Insights o similar
- Métricas de performance
- Alertas automáticas

## 15. Despliegue

### 15.1 Requisitos
- .NET 8 Runtime
- SQL Server 2022+
- Ollama instalado y ejecutándose
- Puerto 5298 disponible para API
- Puerto 5206 disponible para Web

### 15.2 Proceso
1. Restaurar paquetes: `dotnet restore`
2. Ejecutar migraciones: `dotnet ef database update`
3. Configurar appsettings.json
4. Ejecutar API: `dotnet run --project Asistente.API`
5. Ejecutar Web: `dotnet run --project Asistente.Web`

## 16. Mantenimiento

### 16.1 Actualizaciones
- Migraciones de Entity Framework para cambios en BD
- Versionado de API para cambios breaking
- Documentación actualizada con cada cambio

### 16.2 Troubleshooting
- Verificar logs en carpeta Logs/
- Verificar conexión con Ollama
- Verificar cadena de conexión SQL Server
- Ejecutar pruebas unitarias para validar cambios

## 17. Módulo Gestor Documental (Etapa 6)

El Gestor Documental permite registrar, clasificar, versionar y almacenar archivos PDF de forma externa a la aplicación web, preparándolos para la Etapa 7 de embeddings y RAG.

### 17.1 Arquitectura del Módulo

* **Domain (Entidades y Contratos):** 
  * `CategoriaDocumento`: Clasificación del documento (Manual Técnico, Políticas, etc.).
  * `Documento`: Entidad principal que gestiona el estado (`Borrador`, `Activo`, `Archivado`, `Eliminado`) y la bandera `PendienteProcesamiento`.
  * `DocumentoVersion`: Entidad que rastrea cada archivo cargado, su versión, ruta física externa y hash SHA-256.
  * `AuditoriaDocumental`: Bitácora histórica detallada de operaciones (Cargas, descargas, modificaciones de metadatos y estado).
  * `IFileStorageService`: Abstracción de persistencia de archivos físicos.

* **Infrastructure (Implementaciones):**
  * `FileStorageService`: Implementa `IFileStorageService`. Guarda archivos físicos en directorios externos estructurados (`RutaDocumentos/doc_X/vY_NombreArchivo`). Realiza validación de integridad utilizando Magic Bytes para evitar archivos PDF dañados o extensiones falsas.

* **Application (Lógica de Negocio):**
  * `DocumentoService` y `CategoriaDocumentoService`: Manejan las reglas de creación, control de versiones (incremento de versión actual, bloqueo de sobreescritura), auditorías cruzadas e integración de búsquedas.

* **API y Web (Presentación):**
  * `DocumentosController` (API): Proporciona endpoints RESTful para consulta, descarga en flujo de bytes (stream), subida multi-part y cambios de estado.
  * `ApiService` (Web): Transfiere la subida y descarga de ficheros como flujos de bytes entre Web y API.
  * `DocumentosController` (MVC): Proporciona pantallas de subida combinada, visualización de detalle con historial e interfaz para auditorías completas.

## 18. Conclusiones

La arquitectura implementada sigue los principios de Clean Architecture y está preparada para escalabilidad y mantenimiento. La separación de responsabilidades, el uso de interfaces, y la implementación de patrones de diseño garantizan que el sistema pueda evolucionar sin comprometer la estabilidad. La incorporación del Gestor Documental prepara de manera óptima las bases para los pipelines inteligentes de procesamiento de texto (embeddings, RAG) que se desarrollarán en la siguiente etapa.
