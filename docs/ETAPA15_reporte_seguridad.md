# Reporte de Pruebas de Seguridad — ETAPA 15

**Fecha:** 2026-08-11 · **Método:** Pruebas de autorización vía API (JWT) + revisión de capa de seguridad (ETAPA 14).

## Alcance
Autenticación, autorización por rol/permiso, acceso a SQL, documentos, herramientas y workflows.

## Resultados (verificados en runtime)

| Escenario | Resultado esperado | Resultado obtenido |
|-----------|--------------------|--------------------|
| Login admin con credenciales válidas | 200 + JWT | **200 + JWT** ✓ |
| Login operador con credenciales válidas | 200 + JWT | **200 + JWT** ✓ |
| Operador accede a `/api/seguridad/dashboard` (requiere AUDITORIA_CONSULTAR) | 403 | **403** ✓ |
| Operador accede a `/api/seguridad/permisos` (consulta) | 200 | **200** ✓ |
| Usuario sin token accede a ruta protegida | 401 | **401** ✓ |
| Asistente no autorizado para el usuario | Mensaje "asistente no autorizado" | **Bloqueado** ✓ (asignado en ETAPA 14) |

## Capas validadas
- **Autenticación:** ASP.NET Core Identity + JWT (secret vía `JwtSettings`, issuer/audience validados).
- **Autorización:** `AutorizacionService` por rol/permiso; RateLimitingMiddleware (120/min general, 20/min chat).
- **Protección de datos:** `ProteccionDatosService` enmascara DNI/tarjetas/correos.
- **Auditoría:** `AuditoriaActividad` registra operaciones relevantes.

## Conclusión
**Pruebas de seguridad SUPERADAS** para autenticación y control de acceso por rol/permiso. Se recomienda ejecutar la prueba piloto (ver `ETAPA15_prueba_piloto.md`) para validar permisos por módulo con usuarios reales.
