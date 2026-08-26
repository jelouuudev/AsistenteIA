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

## 9. Integración con Proyecto1 (Control de Activos Fijos)
El AsistenteIA puede consultar en vivo el sistema de Control de Activos Fijos (Proyecto1) a través de una conexión SQL Server autorizada:
- **Conexión:** `Activos Fijos (Proyecto1)` — servidor `asistentesql,1433`, base de datos `ControlActivos`, usuario `app_activos`, autenticación SQL (solo lectura).
- **Tabla autorizada:** `Activos` (esquema `dbo`).
- **Cómo usarlo:** en el chat del asistente "Asistente General", pregunta en lenguaje natural, por ejemplo: *"muéstrame los activos fijos registrados"*. El `SqlQueryTool` consulta la base real y devuelve los registros con sus estados (ACTIVO / INACTIVO).
- El asistente distingue dos fuentes: **datos estructurados** (BD en vivo vía SQL) y **conocimiento** (RAG documental, p. ej. preguntas de RRHH como "permisos remunerados").

## 10. Video instructivo
Existe un video instructivo (formato híbrido: instalación rápida con Docker + demo de uso) que muestra:
1. Levantar el stack con `docker compose --profile prod up -d` y verificar los contenedores con `docker ps`.
2. Acceder a la web (`http://localhost:5206`) e iniciar sesión.
3. Consultar activos fijos en vivo desde el Proyecto1.
4. Usar RAG para temas de conocimiento (ej. permisos remunerados).
5. Iniciar sesión con el usuario del Ingeniero.
El guion completo está en `docs/ETAPA15_GuionVideo_Hibrido.md`. Recuerda: el video no debe mostrar contraseñas en pantalla.
