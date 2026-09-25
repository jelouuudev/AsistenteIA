# =============================================================================
# PLAN DE ROLLBACK
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 33
# =============================================================================

## Procedimiento para regresar a la versión anterior en caso de problemas durante el despliegue.

### 1. Backup previo al rollback
```bash
cd C:\Users\Raul\Desktop\AsistenteIA
docker compose --profile prod down
mkdir -p Backups/rollback_$(date +%Y%m%d_%H%M%S)
```

### 2. Restauración de la aplicación
```bash
# Detener contenedores actuales
docker compose --profile prod down

# Restaurar imágenes anteriores (si están etiquetadas)
docker rmi -f asistenteia-api asistenteia-web
docker tag asistenteia-api:backup asistenteia-api:latest
docker tag asistenteia-web:backup asistenteia-web:latest

# O reconstruir desde una versión anterior del código
git checkout tags/v1.0.0
docker compose --profile prod up -d --build
```

### 3. Restauración de base de datos
```bash
# Restaurar backup completo
docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "AsistenteSQL2026" -C \
    -Q "RESTORE DATABASE [AsistenteIA] FROM DISK = '/var/opt/mssql/data/backup_completo_YYYYMMDD.bak' WITH REPLACE;"
```

### 4. Restauración de configuración
```bash
# Restaurar archivos de configuración
cp Backups/configuracion/app_config_*.tar.gz ./
tar -xzf app_config_*.tar.gz
cp Backups/configuracion/docker_config_*.tar.gz ./
tar -xzf docker_config_*.tar.gz
```

### 5. Verificación
```bash
# Verificar contenedores
docker ps --format "table {{.Names}}\t{{.Status}}"

# Verificar API
curl -s -o /dev/null -w "%{http_code}" http://localhost:5298/api/usuarios

# Verificar Web
curl -s -o /dev/null -w "%{http_code}" http://localhost:5206

# Verificar SQL Server
docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "AsistenteSQL2026" -C \
    -Q "SELECT COUNT(*) FROM [Usuario];"

# Verificar Ollama
curl -s http://localhost:11434/api/tags | grep deepseek-r1:7b

# Verificar ChromaDB
curl -s http://localhost:8000/api/v1/heartbeat
```

### 6. Checklist de verificación post-rollback
- [ ] Contenedores corriendo
- [ ] API responde (HTTP 200)
- [ ] Web responde (HTTP 200)
- [ ] SQL Server responde
- [ ] Ollama responde con modelo deepseek-r1:7b
- [ ] ChromaDB responde
- [ ] Login funciona
- [ ] Chat funciona
- [ ] SQL funciona
- [ ] Workflows funcionan
- [ ] Eventos funcionan
- [ ] Auditoría registra actividad
