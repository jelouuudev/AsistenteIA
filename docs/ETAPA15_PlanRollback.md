# Plan de Rollback — ETAPA 15

## 1. Cuándo aplicar
Si tras el despliegue se presentan errores críticos o altos (ver clasificación de incidentes), se regresa a la versión anterior.

## 2. Prerrequisitos
- Backup previo a despliegue (`backup_full.sql` + diff + log).
- Imagen/tag de la versión anterior disponible (ej. `asistenteapi:v1.0.0`).
- Configuración anterior respaldada.

## 3. Procedimiento
1. **Backup inmediato** del estado actual antes de revertir.
2. **Detener** servicios nuevos:
   - `docker compose --profile prod down` (o detener `dotnet` de API/Web).
3. **Restaurar base de datos** con `scripts/restore_sql.sql` (usa el backup previo al despliegue).
4. **Restaurar configuración** anterior (appsettings, .env, docker-compose).
5. **Redesplegar versión anterior**:
   - Docker: `docker compose --profile prod up -d` con tag previo.
   - O `dotnet publish` de la rama `tag vX.Y.Z` anterior.
6. **Verificación** (pruebas de humo):
   - `/health` en API y Web devuelve 200.
   - Login admin OK.
   - 1 chat de prueba OK.
   - Dashboard de seguridad carga.
7. **Registro** en bitácora: fecha, versión revertida, causa, responsable.

## 4. Criterio de éxito
Sistema operativo en la versión anterior, usuarios pueden iniciar sesión y consultar.
