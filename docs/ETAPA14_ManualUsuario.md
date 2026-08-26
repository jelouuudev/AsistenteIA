# Manual de Usuario — ETAPA 14: Seguridad, Gobierno, Auditoría y Observabilidad

## 1. Acceso
1. Abrir `http://localhost:5206` e iniciar sesión (administrador: **admin / Admin123***).
2. El sistema registra inicio de sesión en la auditoría. Las sesiones expiradas o usuarios inactivos son bloqueados.

## 2. Usuarios y Roles
- **Administrador**: acceso total (usuarios, roles, permisos, políticas, auditoría, dashboard).
- **Supervisor**: consulta de auditoría y dashboard, además de lo básico.
- **Usuario**: chat, documentos, fuentes, herramientas de consulta y ejecución de workflows/SQL.
- **Operador**: solo consultas básicas (chat, documentos, fuentes, herramientas de consulta).
- Roles adicionales se pueden crear en **Configuración → Roles**.

## 3. Permisos
- Los permisos se agrupan por módulo (Chat, Documentos, Fuentes, Asistentes, Herramientas, Workflows, SQL, Auditoría, Configuración).
- Ejemplos: `CHAT_CONSULTAR`, `SQL_ADMINISTRAR`, `WORKFLOWS_EJECUTAR`, `AUDITORIA_CONSULTAR`.
- Cada rol recibe solo los permisos necesarios (mínimo privilegio).

## 4. Control de acceso
- **Asistentes**: en **Configuración → Seguridad → Asignar Asistentes** se elige, por usuario, qué asistentes puede usar.
  Un usuario no puede invocar un asistente no autorizado (pantalla de “no autorizado”).
- **Fuentes de conocimiento**: en **Asignar Fuentes** se limita, por usuario, qué fuentes puede consultar la IA.
  El control se aplica con independencia de la pregunta formulada al modelo.
- **Herramientas**: cada herramienta exige un permiso. Por ejemplo, `SqlQueryTool` requiere `SQL_CONSULTAR`/`SQL_ADMINISTRAR`;
  `ReportTool` requiere `HERRAMIENTAS_ADMINISTRAR`. Un usuario sin el permiso ve la operación bloqueada.

## 5. Auditoría
- Toda operación crítica queda registrada: inicio/cierre de sesión, preguntas, asistente usado, fuentes consultadas,
  herramientas ejecutadas, consultas SQL, workflows, eventos automáticos, cambios de configuración y errores.
- **Auditoría → Auditoría de Actividad** permite reconstruir cualquier operación (Caso 7).

## 6. Dashboard de Seguridad
- **Configuración → Seguridad → Dashboard Seguridad** muestra: usuarios activos, conversaciones, consultas,
  tiempo promedio de respuesta, uso de herramientas, consultas SQL, workflows, errores, eventos procesados y
  las últimas auditorías (con badge de resultado: Éxito / Bloqueado / Error).

## 7. Políticas de IA
- **Configuración → Seguridad → Políticas de IA** lista las políticas activas:
  modelo permitido, tamaño máximo de contexto, máximo de resultados RAG, herramientas permitidas,
  tiempo máximo de respuesta, máximo de ejecuciones y fuentes autorizadas.

## 8. Protección de datos y Prompt Injection
- Los datos sensibles (DNI, contraseña, token, tarjeta, credenciales) se enmascaran antes de enviarse al modelo.
- Si un documento o mensaje contiene instrucciones como “ignora las instrucciones anteriores” u “otorga permisos de administrador”,
  el sistema las trata únicamente como contenido documental y no permite que modifiquen las políticas del sistema.

## 9. Rate Limiting
- El sistema limita solicitudes por minuto (General 60, Herramientas 20, SQL 10, Workflows 10). Al superar el límite
  se responde 429 (Caso 5).

## 10. Manejo de errores
- Ante una excepción, el usuario recibe un mensaje amigable; el detalle técnico queda en los logs (Serilog).
