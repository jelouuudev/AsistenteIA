# Manual Técnico

## Arquitectura

### Capas del proyecto

1. **Asistente.Domain**: Entidades del dominio (Conversacion, Mensaje) e interfaces de repositorios.
2. **Asistente.Shared**: DTOs compartidos entre capas (MensajeRequest, MensajeResponse, OllamaRequest/Response).
3. **Asistente.Application**: Casos de uso (ChatService) que orquestan la lógica de negocio.
4. **Asistente.Infrastructure**: Implementaciones concretas (DbContext, Repositorios, OllamaService).
5. **Asistente.API**: Web API REST con endpoints para el chat.
6. **Asistente.Web**: Interfaz de usuario MVC con Bootstrap 5.

### Principios aplicados

- **SOLID**: Cada clase tiene una responsabilidad única.
- **Inyección de dependencias**: Todos los servicios se registran en el contenedor DI.
- **Programación asíncrona**: Todas las operaciones I/O usan async/await.
- **Repository Pattern**: Abstracción de acceso a datos.

### Flujo de una conversación

```
Usuario -> Interfaz Web (JS Fetch) -> ChatController (MVC)
  -> ApiService (HTTP) -> ChatController (API)
    -> ChatService (Application) -> OllamaService (Infrastructure)
      -> Ollama (HTTP)
    -> Repositorios -> SQL Server
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

## API REST

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

## Configuración

### appsettings.json (API)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AsistenteIA;..."
  },
  "Ollama": {
    "Url": "http://localhost:11434",
    "Modelo": "deepseek-r1-distill-qwen-7b",
    "TimeoutSegundos": 120
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### appsettings.json (Web)

```json
{
  "ApiBaseUrl": "http://localhost:5000"
}
```

## Manejo de errores

- Ollama detenido: mensaje claro al usuario
- Modelo inexistente: sugerencia de comando `ollama pull`
- Timeout: mensaje con el tiempo configurado
- Error de red: mensaje de conexión
- Mensaje vacío: validación en cliente y servidor
- Excepciones: logging centralizado
