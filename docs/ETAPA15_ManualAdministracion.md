# Manual de Administración — Asistente Inteligente Empresarial (ETAPA 15)

Accede como **Administrador** (`admin`). Menú **Configuración → Seguridad**.

## 1. Usuarios
- `Seguridad → Usuarios`: lista de usuarios con roles. Botón **Asignar Asistentes** / **Asignar Fuentes** por usuario.
- Crear usuario: definir nombre, correo, contraseña y rol.

## 2. Roles
- Roles disponibles: **Administrador** (todo), **Supervisor** (subconjunto), **Usuario** (consultas), **Operador** (operacional).
- Asignar roles en la creación/edición de usuario.

## 3. Permisos
- 17 códigos por módulo: CHAT_*, DOCUMENTOS_*, FUENTES_*, ASISTENTES_*, HERRAMIENTAS_*, WORKFLOWS_*, SQL_*, AUDITORIA_CONSULTAR, CONFIGURACION_ADMINISTRAR.
- Se asocian a roles vía `RolPermisos`. Ver en `Seguridad → Permisos`.

## 4. Asistentes
- `Seguridad → Asignar Asistentes/{idUsuario}`: marca los asistentes que el usuario podrá usar.
- Sin asignación, el chat responde "asistente no autorizado".

## 5. Documentos
- Carpeta `C:\AsistenteIA_Documentos`. Subir PDF/TXT; el BackgroundService los indexa a ChromaDB automáticamente.

## 6. Fuentes (RAG / SQL)
- `Seguridad → Asignar Fuentes/{idUsuario}`: fuentes de conocimiento autorizadas por usuario.
- Fuentes SQL deben tener conexión configurada y permiso `SQL_CONSULTAR`.

## 7. Herramientas y Workflows
- Configurados en `ConfiguracionWorkflow` / `Herramientas`. El Orchestrator las ejecuta según autorización.

## 8. Eventos y Tareas programadas
- Quartz inicia los BackgroundServices (procesamiento documental 30s, eventos, indexación). Ver logs al arrancar la API.

## 9. Auditoría
- `Seguridad → Dashboard` muestra métricas; `AuditoriaActividad` y `AuditoriaIA` registran operaciones y uso de IA.

## 10. Configuración IA
- `appsettings.json` (sección `Ollama`, `Embedding`, `MotorConsultas`, `JwtSettings`). En producción usar `appsettings.Production.json` + variables de entorno.
