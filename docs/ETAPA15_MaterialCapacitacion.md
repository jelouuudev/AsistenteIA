# Material de Capacitación — ETAPA 15

## A. Capacitación para Administradores (2 horas)
1. **Arquitectura (15 min):** capas, flujo chat→orquestador→RAG/Tools/Workflows→Ollama, auditoría.
2. **Seguridad y Gobierno (30 min):** roles, 17 permisos, asignación de asistentes/fuentes por usuario, políticas IA. Demo: bloqueo de operador en dashboard.
3. **Gestión de contenido (30 min):** carga de documentos PDF/TXT, fuentes SQL, indexación automática (ChromaDB).
4. **Auditoría y monitoreo (20 min):** Dashboard de seguridad, `/api/health`, logs Serilog, alertas.
5. **Backups y recuperación (15 min):** `scripts/backup_full.sql`, `backup_documents_config.ps1`, `restore_sql.sql`, rollback.
6. **Despliegue (10 min):** Docker compose perfiles, `appsettings.Production.json`, variables de entorno.

## B. Capacitación para Usuarios Finales (1 hora)
1. **Inicio de sesión y selección de asistente (10 min).**
2. **Uso del chat en lenguaje natural (20 min):** ejemplos de preguntas, RAG, SQL autorizado.
3. **Solicitud de reportes y procesos (15 min).**
4. **Buenas prácticas y seguridad (15 min):** no compartir contraseña, qué hacer si aparece "no autorizado".

## C. Entregables
- Este documento + `ETAPA15_ManualAdministracion.md` + `ETAPA15_ManualUsuario.md` + `ETAPA15_ManualInstalacion.md`.
- Guion de video de instalación/puesta en producción (`ETAPA15_GuionVideo.md`).
