# Reporte de Pruebas de SQL Injection — ETAPA 15

**Fecha:** 2026-08-11 · **Componente:** `SqlQueryTool` / Motor de Consultas (ETAPAs previas) + `AuditoriaActividad`.

## Verificación de controles
| Control | Mecanismo | Estado |
|---------|-----------|--------|
| Consultas parametrizadas | EF Core / `SqlParameter` en `SqlQueryTool` | ✓ |
| Validación de entradas | Capa de validación de Tool Orchestrator | ✓ |
| Restricción de operaciones | Solo SELECT permitidos; bloqueo de DDL/DML | ✓ |
| Limitación de resultados | `MaximoRegistros=100` (appsettings) | ✓ |
| Bloqueo de operaciones no permitidas | Validación de permiso `SQL_CONSULTAR` + `SQL_ADMINISTRAR` | ✓ |

## Casos de ataque (entrada natural al asistente)
| Entrada | Resultado |
|---------|-----------|
| `'; DROP TABLE Usuario; --` | No ejecuta DDL; el motor lo trata como consulta inválida/segura ✓ |
| `1 OR 1=1` | No altera lógica; parametrizado ✓ |
| `admin'--` | No bypasea autenticación (es entrada de modelo, no de login) ✓ |
| `UNION SELECT * FROM Usuario` | Restringido a consultas autorizadas del esquema ✓ |

## Conclusión
**Pruebas de SQL Injection SUPERADAS**: el acceso a SQL Server es siempre a través de la capa `SqlQueryTool` con parámetros y validación de permisos, sin concatenación de cadenas de usuario.
