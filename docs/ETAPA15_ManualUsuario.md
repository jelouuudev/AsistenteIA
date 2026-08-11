# Manual de Usuario — Asistente Inteligente Empresarial (ETAPA 15)

## 1. Inicio de sesión
- Abre la Web (`http://localhost:5206`).
- Ingresa tu usuario y contraseña. Si no tienes, pide al administrador que te cree usuario y te asigne asistentes/fuentes.

## 2. Selección del asistente
- En el chat, elige el asistente autorizado para tu usuario (dropdown). Si aparece "asistente no autorizado", solicita al admin la asignación.

## 3. Uso del chat
- Escribe en lenguaje natural (ej. "¿Cuál es el procedimiento de alta de empleado?").
- El asistente usa RAG (manuales), Tools (SQL autorizado) y Workflows según corresponda.

## 4. Consulta de información
- Pregunta sobre manuales: el RAG recupera fragmentos relevantes y DeepSeek genera la respuesta.
- Pregunta de datos: si tienes fuente SQL autorizada, el SqlQueryTool consulta y muestra resultados (limitado a 100 filas).

## 5. Uso de documentos
- Los documentos se cargan desde la administración; tú solo consultas sobre ellos vía chat.

## 6. Solicitud de reportes / ejecución de procesos
- Pide un reporte o proceso en lenguaje natural; si hay un Workflow autorizado, el orquestador lo ejecuta y entrega el resultado.

## 7. Visualización de resultados
- La respuesta aparece en el chat con la fuente/contenido recuperado. Operaciones relevantes quedan auditadas.

## 8. Seguridad
- No compartas tu contraseña. El sistema bloquea intentos de inyección de prompts y accesos no autorizados.
