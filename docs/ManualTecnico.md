# Manual Técnico - Asistente Inteligente Empresarial

## Arquitectura

### Capas del proyecto (Clean Architecture)

1. **Asistente.Domain**: Entidades del dominio (Conversacion, Mensaje, Usuario, Rol, UsuarioRol, AuditoriaSesion, AuditoriaActividad), enumeraciones e interfaces (IAIProvider, IConversacionRepository, IMensajeRepository, IUsuarioRepository, IRolRepository, IAuditoriaRepository, IUnitOfWork, IPasswordHasher).
2. **Asistente.Shared**: DTOs compartidos entre capas (MensajeRequest, MensajeResponse, OllamaConfig, LoginRequest, LoginResponse, UsuarioDto, RolDto, AuditoriaSesionDto, AuditoriaActividadDto, CrearUsuarioRequest, ActualizarUsuarioRequest, CambiarPasswordRequest, CrearRolRequest, ActualizarRolRequest).
3. **Asistente.Application**: Casos de uso (ChatService, UsuarioService, RolService, AuditoriaService), DTOs de aplicación (ChatRequestDto, ChatResponseDto, ConversationDto, MessageDto), validadores (FluentValidation).
4. **Asistente.Infrastructure**: Implementaciones concretas (DbContext, Repositorios, OllamaService, OllamaProvider, PasswordHasher), configuración de Serilog, DbInitializer.
5. **Asistente.API**: Web API REST con endpoints para el chat, autenticación, usuarios, roles y auditoría, Swagger/OpenAPI, middleware de excepciones, FluentValidation.
6. **Asistente.Web**: Interfaz de usuario MVC con Bootstrap 5, JavaScript (Fetch API), autenticación por cookies, administración de usuarios/roles/auditoría.
7. **Asistente.Tests**: Pruebas unitarias con xUnit y Moq.

### Principios aplicados

- **SOLID**: Cada clase tiene una responsabilidad única.
- **Clean Architecture**: Dependencias solo apuntan hacia adentro (hacia el dominio).
- **Inyección de dependencias**: Todos los servicios se registran en el contenedor DI.
- **Programación asíncrona**: Todas las operaciones I/O usan async/await.
- **Repository Pattern**: Abstracción de acceso a datos.
- **Unit of Work**: Coordinación de transacciones.
- **DTO Pattern**: Separación de entidades de dominio de la API.
- **Middleware Pattern**: Manejo transversal de excepciones.

### Flujo de una conversación

```
Usuario -> Interfaz Web (JS Fetch) -> ChatController (MVC)
  -> ApiService (HTTP) -> ChatController (API)
    -> FluentValidation (Validación)
    -> ChatService (Application)
      -> IAIProvider.SendMessageAsync (OllamaProvider)
        -> Ollama (HTTP)
      -> Repositorios (SQL Server)
    -> ExceptionMiddleware (Manejo de errores)
    -> Serilog (Logging)
```

## Base de datos

### Tabla Conversacion

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdConversacion | INT (PK, Identity) | Identificador único |
| FechaInicio | DATETIME2 | Inicio de la conversación |
| FechaFin | DATETIME2 (nullable) | Fin de la conversación |
| Estado | NVARCHAR(20) | Activa / Finalizada |

### Tabla Mensaje

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdMensaje | INT (PK, Identity) | Identificador único |
| IdConversacion | INT (FK) | Referencia a conversación |
| Rol | NVARCHAR(20) | User / Assistant |
| Contenido | NVARCHAR(MAX) | Texto del mensaje |
| FechaHora | DATETIME2 | Momento del mensaje |
| TiempoRespuestaMs | BIGINT (nullable) | Tiempo de respuesta de la IA |

### Tabla Usuario

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdUsuario | INT (PK, Identity) | Identificador único |
| Usuario | NVARCHAR(50) (Unique) | Nombre de usuario |
| Nombres | NVARCHAR(100) | Nombres del usuario |
| Apellidos | NVARCHAR(100) | Apellidos del usuario |
| Correo | NVARCHAR(100) | Correo electrónico |
| PasswordHash | NVARCHAR(256) | Hash de contraseña (PBKDF2 + Salt) |
| Activo | BIT | Usuario activo/inactivo |
| FechaCreacion | DATETIME2 | Fecha de creación |
| FechaUltimoAcceso | DATETIME2 (nullable) | Último acceso |

### Tabla Rol

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdRol | INT (PK, Identity) | Identificador único |
| Nombre | NVARCHAR(50) (Unique) | Nombre del rol (Administrador, Operador, Supervisor) |
| Descripcion | NVARCHAR(250) | Descripción del rol |
| Activo | BIT | Rol activo/inactivo |

### Tabla UsuarioRol

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdUsuario | INT (FK, PK) | Referencia a usuario |
| IdRol | INT (FK, PK) | Referencia a rol |

### Tabla AuditoriaSesion

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdSesion | INT (PK, Identity) | Identificador único |
| IdUsuario | INT (FK) | Referencia a usuario |
| FechaInicio | DATETIME2 | Inicio de sesión |
| FechaFin | DATETIME2 (nullable) | Fin de sesión |
| DireccionIP | NVARCHAR(50) | Dirección IP del cliente |
| Navegador | NVARCHAR(250) | User-Agent del navegador |
| Estado | NVARCHAR(20) | Exitoso, Fallido, Inactivo, Cerrado |

### Tabla AuditoriaActividad

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdActividad | INT (PK, Identity) | Identificador único |
| IdUsuario | INT (FK) | Referencia a usuario |
| FechaHora | DATETIME2 | Fecha y hora de la actividad |
| Modulo | NVARCHAR(50) | Módulo afectado (Usuarios, Roles, etc.) |
| Accion | NVARCHAR(100) | Acción realizada (Creación, Modificación, etc.) |
| Descripcion | NVARCHAR(500) | Descripción detallada |
| DireccionIP | NVARCHAR(50) | Dirección IP del cliente |

## API REST

### Autenticación

#### POST /api/auth/login

**Request:**
```json
{
  "usuario": "admin",
  "contrasena": "Admin123*",
  "direccionIP": "127.0.0.1",
  "navegador": "Mozilla/5.0..."
}
```

**Response (éxito):**
```json
{
  "exitoso": true,
  "usuario": {
    "idUsuario": 1,
    "usuarioNombre": "admin",
    "nombres": "Administrador",
    "apellidos": "Principal",
    "correo": "admin@asistenteia.local",
    "activo": true,
    "fechaCreacion": "2026-07-06T00:00:00Z",
    "fechaUltimoAcceso": "2026-07-06T12:00:00Z",
    "roles": ["Administrador"]
  },
  "idSesion": 1,
  "error": null
}
```

**Response (error):**
```json
{
  "exitoso": false,
  "usuario": null,
  "idSesion": null,
  "error": "Usuario o contraseña incorrectos."
}
```

#### POST /api/auth/logout/{sessionId}

Cierra la sesión del usuario y registra la fecha de fin.

### Usuarios

#### GET /api/usuarios
Obtiene todos los usuarios (requiere rol Administrador).

#### GET /api/usuarios/{id}
Obtiene un usuario por ID.

#### POST /api/usuarios
Crea un nuevo usuario.

**Request:**
```json
{
  "usuarioNombre": "nuevo_usuario",
  "nombres": "Juan",
  "apellidos": "Pérez",
  "correo": "juan@empresa.com",
  "contrasena": "Password123*",
  "roles": ["Operador"]
}
```

#### PUT /api/usuarios/{id}
Actualiza un usuario existente.

#### DELETE /api/usuarios/{id}
Desactiva un usuario (no elimina físicamente).

#### POST /api/usuarios/{id}/cambiar-password
Cambia la contraseña de un usuario.

### Roles

#### GET /api/roles
Obtiene todos los roles.

#### GET /api/roles/{id}
Obtiene un rol por ID.

#### POST /api/roles
Crea un nuevo rol.

**Request:**
```json
{
  "nombre": "NuevoRol",
  "descripcion": "Descripción del rol",
  "activo": true
}
```

#### PUT /api/roles/{id}
Actualiza un rol existente.

### Auditoría

#### GET /api/auditoria/sesiones
Obtiene todas las sesiones registradas.

#### GET /api/auditoria/actividades
Obtiene todas las actividades registradas.

### POST /api/chat/enviar

**Request:**
```json
{
  "idConversacion": null,
  "mensaje": "Hola, ¿cómo estás?"
}
```

**Response (éxito):**
```json
{
  "idConversacion": 1,
  "respuesta": "¡Hola! Estoy bien, ¿en qué puedo ayudarte?",
  "tiempoRespuestaMs": 3500,
  "exitoso": true,
  "error": null
}
```

**Response (error):**
```json
{
  "idConversacion": 0,
  "respuesta": "",
  "tiempoRespuestaMs": 0,
  "exitoso": false,
  "error": "El servicio Ollama no está disponible."
}
```

### Swagger/OpenAPI

La API está documentada con Swagger en: `http://localhost:5298/swagger`

Incluye:
- Descripción de cada endpoint
- Parámetros de entrada
- Tipos de respuesta
- Códigos HTTP esperados

## Configuración

### appsettings.json (API)

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AsistenteIA;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Ollama": {
    "Url": "http://localhost:11434",
    "Modelo": "deepseek-r1:7b",
    "TimeoutSegundos": 600
  }
}
```

### Configuración por ambientes

- **appsettings.Development.json**: Logging en Debug, Swagger habilitado
- **appsettings.Testing.json**: Logging en Information, Swagger deshabilitado
- **appsettings.Production.json**: Logging en Warning, Swagger deshabilitado

### appsettings.json (Web)

```json
{
  "ApiBaseUrl": "http://localhost:5298",
  "ApiTimeoutSegundos": 130
}
```

## Validaciones (FluentValidation)

### ChatRequestValidator

- **Mensaje**: No vacío, mínimo 1 carácter, máximo 4000 caracteres
- **ConversationId**: Si presente, debe ser mayor que 0

Las validaciones se ejecutan automáticamente antes de la lógica de negocio.

## Logging (Serilog)

### Configuración

- **Console**: Salida en tiempo real para desarrollo
- **File**: Archivos rotativos diarios en carpeta `Logs/`
- **Retención**: 30 días de logs
- **Formato**: `[Timestamp] [Level] Message Exception`

### Eventos registrados

- Inicio de aplicación
- Solicitudes HTTP
- Tiempo de respuesta
- Excepciones
- Errores de comunicación con Ollama
- Errores de acceso a SQL Server

## Manejo de Errores

### Middleware de Excepciones

- **TimeoutException**: HTTP 408 Request Timeout
- **InvalidOperationException**: HTTP 400 Bad Request
- **UnauthorizedAccessException**: HTTP 401 Unauthorized
- **Exception genérica**: HTTP 500 Internal Server Error

### Casos específicos

- Ollama detenido: mensaje claro al usuario
- Modelo inexistente: sugerencia de comando `ollama pull`
- Timeout: mensaje con el tiempo configurado
- Error de red: mensaje de conexión
- Mensaje vacío: validación con FluentValidation
- Excepciones: logging centralizado con Serilog

## Pruebas Unitarias

### Frameworks

- **xUnit**: Framework de pruebas
- **Moq**: Framework de mocking
- **FluentValidation**: Validación en pruebas

### Pruebas implementadas

- **ChatRequestValidatorTests** (7 pruebas): Validaciones de DTOs
- **ChatServiceTests** (5 pruebas): Lógica de negocio
- **OllamaProviderTests** (6 pruebas): Proveedor de IA
- **UsuarioServiceTests** (pruebas): Lógica de autenticación y usuarios
- **PasswordHasherTests** (pruebas): Seguridad de contraseñas

### Ejecución

```bash
dotnet test Asistente.Tests
```

**Cobertura actual:** 100% (18/18 pruebas pasan)

## DTOs (Data Transfer Objects)

### Application Layer DTOs

- **ChatRequestDto**: Solicitud de chat (Message, ConversationId)
- **ChatResponseDto**: Respuesta de chat (ConversationId, Response, ResponseTimeMs, Success, Error)
- **ConversationDto**: Conversación (Id, StartDate, EndDate, Status, Messages)
- **MessageDto**: Mensaje (Id, ConversationId, Role, Content, DateTime, ResponseTimeMs)

### Shared DTOs

- **MensajeRequest**: Request compartido (IdConversacion, Mensaje)
- **MensajeResponse**: Response compartido (IdConversacion, Respuesta, TiempoRespuestaMs, Exitoso, Error)
- **OllamaConfig**: Configuración de Ollama (Url, Modelo, TimeoutSegundos)
- **LoginRequest**: Request de login (Usuario, Contrasena, DireccionIP, Navegador)
- **LoginResponse**: Response de login (Exitoso, Usuario, IdSesion, Error)
- **UsuarioDto**: DTO de usuario (IdUsuario, UsuarioNombre, Nombres, Apellidos, Correo, Activo, FechaCreacion, FechaUltimoAcceso, Roles)
- **RolDto**: DTO de rol (IdRol, Nombre, Descripcion, Activo)
- **AuditoriaSesionDto**: DTO de sesión de auditoría (IdSesion, IdUsuario, FechaInicio, FechaFin, DireccionIP, Navegador, Estado)
- **AuditoriaActividadDto**: DTO de actividad de auditoría (IdActividad, IdUsuario, FechaHora, Modulo, Accion, Descripcion, DireccionIP)
- **CrearUsuarioRequest**: Request para crear usuario (UsuarioNombre, Nombres, Apellidos, Correo, Contrasena, Roles)
- **ActualizarUsuarioRequest**: Request para actualizar usuario (Nombres, Apellidos, Correo, Activo, Roles)
- **CambiarPasswordRequest**: Request para cambiar contraseña (NuevaContrasena)
- **CrearRolRequest**: Request para crear rol (Nombre, Descripcion, Activo)
- **ActualizarRolRequest**: Request para actualizar rol (Nombre, Descripcion, Activo)

## Interfaces de Proveedores de IA

### IAIProvider

Interfaz para desacoplar la implementación del proveedor de IA:

```csharp
public interface IAIProvider
{
    Task<string> SendMessageAsync(IEnumerable<string> messages, CancellationToken cancellationToken = default);
}
```

### Implementaciones

- **OllamaProvider**: Implementación actual usando Ollama
- **Futuros**: OpenAIProvider, AzureOpenAIProvider, etc.

## Dependencias

### Paquetes NuGet principales

- **Asistente.API**: Serilog.AspNetCore, FluentValidation.AspNetCore, Swashbuckle.AspNetCore
- **Asistente.Application**: FluentValidation, FluentValidation.DependencyInjectionExtensions
- **Asistente.Infrastructure**: Microsoft.EntityFrameworkCore.SqlServer, Microsoft.Extensions.Http
- **Asistente.Tests**: xUnit, Moq, FluentValidation

## Migraciones de Entity Framework

### Ejecutar migraciones

```bash
dotnet ef migrations add NombreMigracion --project Asistente.Infrastructure
dotnet ef database update --project Asistente.Infrastructure
```

### Scripts SQL

Los scripts de migración se generan en `scripts/04_MigracionEF.sql`

## Despliegue

### Requisitos

- .NET 8 Runtime
- SQL Server 2022+
- Ollama instalado y ejecutándose
- Puerto 5298 disponible para API
- Puerto 5206 disponible para Web

### Pasos

1. Restaurar paquetes: `dotnet restore`
2. Ejecutar migraciones: `dotnet ef database update`
3. Configurar appsettings.json según ambiente
4. Ejecutar API: `dotnet run --project Asistente.API`
5. Ejecutar Web: `dotnet run --project Asistente.Web`

## Troubleshooting

### Problemas comunes

1. **Ollama no responde**
   - Verificar que Ollama esté ejecutándose: `ollama serve`
   - Verificar URL en appsettings.json
   - Verificar modelo disponible: `ollama list`

2. **Error de conexión SQL Server**
   - Verificar cadena de conexión
   - Verificar que SQL Server esté ejecutándose
   - Verificar que la base de datos exista

3. **Timeout en respuestas**
   - Aumentar TimeoutSegundos en appsettings.json
   - Verificar carga del sistema
   - Considerar modelo más ligero

4. **Errores en pruebas**
   - Ejecutar `dotnet restore`
   - Verificar que todas las dependencias estén instaladas
   - Ejecutar pruebas individualmente para identificar el problema

## Documentación adicional

- **Arquitectura.md**: Documento detallado de arquitectura
- **ManualInstalacion.md**: Guía de instalación paso a paso
- **ManualUsuario.md**: Guía para usuarios finales
- **Seguridad.md**: Documento de seguridad implementada

## ETAPA 4 — Motor de Configuración del Asistente (Prompts, Roles y Personalidad)

### Tablas nuevas

#### Tabla Asistente

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdAsistente | INT (PK, Identity) | Identificador único |
| Nombre | NVARCHAR(100) | Nombre del asistente |
| Descripcion | NVARCHAR(500) | Propósito del asistente |
| ModeloIA | NVARCHAR(100) | Modelo de IA (ej. deepseek-r1:7b) |
| Activo | BIT | Asistente activo/inactivo |
| FechaCreacion | DATETIME2 | Fecha de creación |
| Idioma | NVARCHAR(10) | Idioma de respuesta (es, en, pt, fr) |
| LongitudMaximaRespuesta | INT | Máx. caracteres en respuesta |
| NivelFormalidad | NVARCHAR(20) | formal, profesional, casual, amigable |
| FormatoRespuesta | NVARCHAR(20) | texto, markdown, html, json |
| Restricciones | NVARCHAR(MAX) | Reglas de comportamiento |
| MensajeBienvenida | NVARCHAR(500) | Mensaje inicial del asistente |
| Temperatura | FLOAT | Creatividad del modelo (0.0–2.0) |
| MaxTokens | INT | Máx. tokens por respuesta |
| TimeoutSegundos | INT | Timeout de conexión con Ollama |

#### Tabla PromptSistema

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdPrompt | INT (PK, Identity) | Identificador único |
| IdAsistente | INT (FK) | Referencia al asistente |
| Nombre | NVARCHAR(200) | Nombre del prompt |
| Contenido | NVARCHAR(MAX) | Instrucciones del sistema |
| Version | INT | Versión automática (1, 2, 3...) |
| Activo | BIT | Prompt activo/inactivo |
| FechaCreacion | DATETIME2 | Fecha de creación |
| UsuarioCreacion | NVARCHAR(100) | Usuario que lo creó |

#### Tabla HistorialPrompt

| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdHistorial | INT (PK, Identity) | Identificador único |
| IdPrompt | INT (FK) | Referencia al prompt |
| Version | INT | Versión del prompt en ese momento |
| Contenido | NVARCHAR(MAX) | Contenido en esa versión |
| FechaModificacion | DATETIME2 | Fecha del cambio |
| UsuarioModificacion | NVARCHAR(100) | Usuario que modificó |
| MotivoCambio | NVARCHAR(500) | Razón del cambio |

### Nuevos endpoints API

#### Asistentes

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | /api/asistentes | Obtiene todos los asistentes |
| GET | /api/asistentes/{id} | Obtiene un asistente por ID |
| POST | /api/asistentes | Crea un asistente |
| PUT | /api/asistentes/{id} | Actualiza un asistente |
| PUT | /api/asistentes/{id}/activar | Activa un asistente |
| PUT | /api/asistentes/{id}/desactivar | Desactiva un asistente |

#### Prompts

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | /api/prompts | Obtiene todos los prompts |
| GET | /api/prompts/{id} | Obtiene un prompt por ID |
| GET | /api/prompts/asistente/{id} | Prompts de un asistente |
| GET | /api/prompts/asistente/{id}/activo | Prompt activo de un asistente |
| POST | /api/prompts | Crea un prompt (versión 1) |
| PUT | /api/prompts/{id} | Actualiza prompt (nueva versión automática) |
| PUT | /api/prompts/{id}/activar | Activa un prompt (desactiva otros) |
| PUT | /api/prompts/{id}/desactivar | Desactiva un prompt |
| DELETE | /api/prompts/{id} | Elimina un prompt |
| POST | /api/prompts/{id}/duplicar | Duplica un prompt |
| GET | /api/prompts/{id}/historial | Historial de versiones |
| POST | /api/prompts/probar | Prueba asistente con prompt |

### DTOs nuevos

```csharp
// Asistente
AsistenteDto, CrearAsistenteRequest, ActualizarAsistenteRequest

// Prompt
PromptSistemaDto, CrearPromptRequest, ActualizarPromptRequest

// Historial
HistorialPromptDto

// Prueba
PruebaAsistenteRequest, PruebaAsistenteResponse
```

### Validadores nuevos (FluentValidation)

- **CrearAsistenteRequestValidator**: Nombre obligatorio (max 100), ModeloIA obligatorio (max 100)
- **ActualizarAsistenteRequestValidator**: Nombre obligatorio (max 100), ModeloIA obligatorio (max 100)
- **CrearPromptRequestValidator**: IdAsistente > 0, Nombre obligatorio (max 200), Contenido obligatorio
- **ActualizarPromptRequestValidator**: Nombre obligatorio (max 200), Contenido obligatorio
- **PruebaAsistenteRequestValidator**: IdAsistente > 0, Mensaje obligatorio

### Motor de construcción del Prompt

El `ChatService` construye automáticamente el System Prompt antes de enviar al modelo:

1. Obtiene el asistente seleccionado (por `IdAsistente` en `MensajeRequest`)
2. Obtiene el `PromptSistema` activo de ese asistente
3. Combina: nombre, idioma, nivel de formalidad, restricciones, mensaje de bienvenida + contenido del prompt
4. Envía el system prompt como mensaje con role `system` a Ollama
5. El usuario final solo ve la respuesta, nunca el prompt generado

### Pruebas unitarias actualizadas

| Archivo | Pruebas | Descripción |
|---------|---------|-------------|
| ChatServiceTests.cs | 5 | Actualizadas para nuevo constructor |
| AsistenteServiceTests.cs | 9 | CRUD, activar/desactivar asistentes |
| PromptSistemaServiceTests.cs | 7 | CRUD, versionado, activación, historial |
| CrearAsistenteRequestValidatorTests.cs | 3 | Validación de creación |
| CrearPromptRequestValidatorTests.cs | 4 | Validación de prompts |
| **Total** | **28** (nuevas) | +21 nuevas, 53 totales |

### Arquitectura — Flujo actualizado con motor de prompts

```
Usuario (Web)
    ↓ POST /Chat/Enviar { mensaje, idAsistente }
ChatController (Web)
    ↓ Fetch API
ChatController (API)
    ↓ Validación (FluentValidation)
ChatService (Application)
    ↓ Obtiene Asistente activo + PromptSistema activo
    ↓ Construye System Prompt (configuración + restricciones + prompt)
    ↓ IOllamaService.SendMessageAsync(historial, modelOverride, systemPrompt)
OllamaService (Infrastructure)
    ↓ POST /api/chat (Ollama) — incluye mensaje "system"
Ollama (localhost:11434)
    ↓ Respuesta
→ Se guarda en Conversacion + Mensaje (SQL Server)
```

## ETAPA 3 - Autenticación y Autorización

### Sistema de Autenticación

- **Mecanismo**: Autenticación basada en cookies (ASP.NET Core Authentication)
- **Hash de contraseñas**: PBKDF2 con SHA256, 100,000 iteraciones, salt de 128 bits
- **Sesiones**: Registro de inicio/fin, IP, navegador, estado
- **Roles**: Administrador, Operador, Supervisor

### Sistema de Autorización

- **Protección de rutas**: RequireAuthenticatedUser global en Asistente.Web
- **Autorización por roles**: [Authorize(Roles = "Administrador")] en controladores
- **Menú condicional**: _Layout.cshtml muestra opciones según rol del usuario

### Auditoría

- **Sesiones**: Registro de todos los intentos de login (exitosos, fallidos, inactivos)
- **Actividades**: Registro de cambios en usuarios, roles, contraseñas
- **Almacenamiento**: Tablas AuditoriaSesion y AuditoriaActividad en SQL Server

### Usuarios Iniciales

El sistema se inicializa con 3 usuarios (DbInitializer.cs):

| Usuario | Contraseña | Rol |
|---------|------------|-----|
| admin | Admin123* | Administrador |
| operador | Operador123* | Operador |
| supervisor | Supervisor123* | Supervisor |

### Permisos por Rol

- **Administrador**: Acceso completo (Chat, Usuarios, Roles, Auditoría, Gestor Documental, Categorías)
- **Operador**: Chat y Gestor Documental (subida y visualización de documentos de conocimiento)
- **Supervisor**: Chat y Auditoría (general y documental)

## ETAPA 6 — Gestor Documental Empresarial

El Gestor Documental permite registrar y versionar archivos PDF de manera aislada (almacenamiento fuera del proyecto). Estos documentos servirán de base de conocimientos para la IA (Etapa 7).

### Base de datos (Tablas Nuevas)

#### CategoriaDocumento
| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdCategoria | INT (PK, Identity) | Identificador único de la categoría |
| Nombre | NVARCHAR(100) (Unique) | Nombre único (Manual Técnico, Procedimientos, etc.) |
| Descripcion | NVARCHAR(500) | Breve detalle |
| Activo | BIT | Estado lógico de disponibilidad |

#### Documento
| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdDocumento | INT (PK, Identity) | Identificador único del documento |
| Codigo | NVARCHAR(50) (Unique) | Código de referencia del documento |
| Nombre | NVARCHAR(200) | Nombre descriptivo del documento |
| Descripcion | NVARCHAR(1000) | Resumen del contenido |
| IdCategoria | INT (FK) | Relación con la tabla CategoriaDocumento |
| VersionActual | INT | Número correlativo de la última versión activa |
| Estado | NVARCHAR(50) | Borrador, Activo, Archivado, Eliminado |
| PendienteProcesamiento | BIT | Indica si está pendiente para procesamiento RAG (Etapa 7) |
| FechaRegistro | DATETIME2 | Fecha en UTC de creación |
| UsuarioRegistro | INT | ID del usuario que creó el registro |

#### DocumentoVersion
| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdVersion | INT (PK, Identity) | Identificador de la versión física |
| IdDocumento | INT (FK) | Relación con la tabla Documento |
| NumeroVersion | INT | Versión del archivo (1, 2, 3...) |
| NombreArchivo | NVARCHAR(255) | Nombre del archivo original subido |
| RutaArchivo | NVARCHAR(1000) | Ruta absoluta del archivo en almacenamiento externo |
| TamanoArchivo | BIGINT | Tamaño en bytes |
| HashArchivo | NVARCHAR(64) | Firma SHA-256 única del archivo |
| FechaCarga | DATETIME2 | Fecha en UTC de subida |
| UsuarioCarga | INT | ID del usuario que subió la versión |
| Activo | BIT | Estado de la versión |

#### AuditoriaDocumental
| Columna | Tipo | Descripción |
|---------|------|-------------|
| IdAuditoria | INT (PK, Identity) | Identificador único de auditoría |
| IdDocumento | INT (FK) | Relación con el documento auditado |
| IdVersion | INT (nullable) | Relación con la versión involucrada |
| Accion | NVARCHAR(50) | Carga, Modificación, Descarga, CambioEstado, etc. |
| Descripcion | NVARCHAR(1000) | Detalle del cambio realizado |
| UsuarioId | INT | ID del usuario que ejecutó la acción |
| FechaAccion | DATETIME2 | Fecha en UTC del evento |
| DireccionIP | NVARCHAR(50) | Dirección IP del cliente |

### API REST (Nuevos Endpoints)

#### Categorías Documento
* `GET /api/categoriasdocumento` - Obtiene todas las categorías.
* `GET /api/categoriasdocumento/activas` - Obtiene las categorías disponibles para clasificación.
* `GET /api/categoriasdocumento/{id}` - Obtiene categoría por ID.
* `POST /api/categoriasdocumento` - Crea una nueva categoría.
* `PUT /api/categoriasdocumento/{id}` - Modifica una categoría.

#### Documentos
* `GET /api/documentos` - Obtiene documentos filtrados por nombre, categoría, estado y fechas.
* `GET /api/documentos/{id}` - Obtiene metadatos de un documento.
* `POST /api/documentos` - Registra un nuevo documento.
* `PUT /api/documentos/{id}` - Actualiza metadatos (nombre, descripción, categoría).
* `PUT /api/documentos/{id}/activar` - Pone el estado en Activo.
* `PUT /api/documentos/{id}/archivar` - Pone el estado en Archivado.
* `DELETE /api/documentos/{id}` - Pone el estado en Eliminado (Eliminación lógica).
* `GET /api/documentos/{id}/versiones` - Obtiene todas las versiones de un documento.
* `POST /api/documentos/{id}/versiones` - Sube un nuevo archivo PDF e incrementa la versión.
* `GET /api/documentos/{id}/versiones/{versionId}/descargar` - Descarga el archivo de la versión como Stream.
* `GET /api/documentos/{id}/auditoria` - Obtiene la auditoría específica del documento.
* `GET /api/documentos/auditoria/todas` - Obtiene la auditoría documental global.

### Configuración (appsettings.json)
Se introduce la sección `GestorDocumental`:
```json
  "GestorDocumental": {
    "RutaDocumentos": "C:\\AsistenteIA_Documentos",
    "TamanoMaximoMB": 50,
    "ExtensionesPermitidas": [ ".pdf" ]
  }
```

### Validaciones e Integridad
1. **Validación de Archivos:**
   * **Extensión:** Caso insensible, solo `.pdf` permitido.
   * **Tamaño:** No vacío y menor a `TamanoMaximoMB`.
   * **Integridad (Anticorrupción):** Validación de firma de bytes (Magic Bytes) comprobando que los primeros 4 bytes del stream coincidan exactamente con `%PDF` (`0x25 0x50 0x44 0x46`). Evita cargas corruptas o archivos camuflados.
2. **Cálculo de Hash:**
   * Cada archivo genera una firma SHA-256 única almacenada en base de datos.
3. **No Sobreescritura:**
   * Cada subida de archivo genera un nombre físico enriquecido con el ID y versión (`RutaDocumentos/doc_X/vY_NombreOriginal.pdf`), asegurando que ninguna versión reemplace a otra.

