# =============================================================================
# MANUAL DE ADMINISTRACIÓN
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 26
# =============================================================================

## 1. Usuarios

### 1.1 Crear usuario
1. Ir a Configuración → Usuarios
2. Clic en "Nuevo Usuario"
3. Llenar campos:
   - Usuario (único)
   - Nombres
   - Apellidos
   - Correo electrónico
   - Contraseña temporal
4. Asignar rol (Administrador, Operador, Supervisor, Usuario)
5. Guardar

### 1.2 Editar usuario
1. Ir a Configuración → Usuarios
2. Clic en el usuario a editar
3. Modificar datos
4. Guardar cambios

### 1.3 Activar/Desactivar usuario
- Una vez creado no se puede desactivar desde la interfaz, pero se puede eliminar si es necesario.

### 1.4 Usuarios por defecto
| Usuario | Contraseña | Rol |
|---|---|---|
| admin | Admin123* | Administrador |
| operador | Operador123* | Operador |
| supervisor | Supervisor123* | Supervisor |

## 2. Roles

### 2.1 Roles del sistema
| Rol | Descripción |
|---|---|
| Administrador | Acceso completo a todo el sistema |
| Operador | Puede utilizar el asistente y consultar |
| Supervisor | Puede consultar auditorías |
| Usuario | Acceso básico |

### 2.2 Crear rol
1. Ir a Configuración → Roles
2. Clic en "Nuevo Rol"
3. Asignar nombre y descripción
4. Asignar permisos
5. Guardar

### 2.3 Permisos disponibles
| Código | Descripción |
|---|---|
| CHAT_CONSULTAR | Consultar chat |
| CHAT_ADMINISTRAR | Administrar chat |
| DOCUMENTOS_CONSULTAR | Consultar documentos |
| DOCUMENTOS_ADMINISTRAR | Administrar documentos |
| FUENTES_CONSULTAR | Consultar fuentes |
| FUENTES_ADMINISTRAR | Administrar fuentes |
| ASISTENTES_CONSULTAR | Consultar asistentes |
| ASISTENTES_ADMINISTRAR | Administrar asistentes |
| HERRAMIENTAS_CONSULTAR | Consultar herramientas |
| HERRAMIENTAS_ADMINISTRAR | Administrar herramientas |
| WORKFLOWS_CONSULTAR | Consultar workflows |
| WORKFLOWS_EJECUTAR | Ejecutar workflows |
| WORKFLOWS_ADMINISTRAR | Administrar workflows |
| SQL_CONSULTAR | Consultar SQL |
| SQL_ADMINISTRAR | Administrar SQL |
| AUDITORIA_CONSULTAR | Consultar auditoría |
| CONFIGURACION_ADMINISTRAR | Administrar configuración |

## 3. Asistentes

### 3.1 Crear asistente
1. Ir a Configuración → Asistentes
2. Clic en "Nuevo Asistente"
3. Llenar datos:
   - Nombre
   - Código (único)
   - Modelo IA (deepseek-r1:7b)
   - Descripción
   - Prompt del sistema
4. Asignar herramientas
5. Asignar fuentes de conocimiento
6. Asignar roles autorizados
7. Guardar

### 3.2 Configuración del modelo
| Parámetro | Valor recomendado | Descripción |
|---|---|---|
| Temperatura | 0.3 | Controla la creatividad |
| Max Tokens | 8192 | Límite de respuesta |
| Timeout | 600s | Tiempo máximo de espera |

## 4. Documentos

### 4.1 Subir documento
1. Ir a Documentos → Subir
2. Seleccionar archivo PDF
3. Asignar categoría
4. Subir

### 4.2 Versionamiento
- Cada documento puede tener múltiples versiones
- Solo la versión vigente se usa en RAG
- Las versiones históricas se conservan

### 4.3 Procesamiento
- Los documentos se procesan automáticamente
- Se generan chunks (fragmentos)
- Se crean embeddings
- Se almacenan en ChromaDB

## 5. Fuentes de Conocimiento

### 5.1 Tipos de fuentes
- Manual
- Procedimiento
- FAQ
- Política
- Normativa
- Documentación Técnica

### 5.2 Crear fuente
1. Ir a Configuración → Fuentes de Conocimiento
2. Clic en "Nueva Fuente"
3. Llenar datos:
   - Nombre
   - Código
   - Tipo
   - Descripción
   - Prioridad
4. Asociar documentos
5. Asociar asistentes
6. Guardar

## 6. Herramientas

### 6.1 Herramientas disponibles
| Herramienta | Descripción |
|---|---|
| DocumentSearchTool | Búsqueda en RAG |
| SqlQueryTool | Consultas SQL controladas |
| CalculatorTool | Operaciones matemáticas |
| DateTimeTool | Fecha y hora del servidor |
| ReportTool | Generación de reportes PDF |

### 6.2 Configurar herramienta
1. Ir a Configuración → Herramientas
2. Seleccionar herramienta
3. Activar/Desactivar
4. Asignar a asistentes

## 7. Workflows

### 7.1 Crear workflow
1. Ir a Configuración → Workflows
2. Clic en "Nuevo Workflow"
3. Llenar datos:
   - Nombre
   - Código
   - Descripción
   - Frases disparadoras
4. Agregar pasos:
   - Nombre del paso
   - Herramienta
   - Parámetros (JSON)
   - Reintentos
   - Tiempo máximo
   - Estrategia de error
5. Guardar

### 7.2 Estrategias de error
- **Cancelar**: Detener el flujo si falla el paso
- **Omitir**: Continuar al siguiente paso
  (RegistrarIncidencia se eliminó por ser idéntica a Omitir sin registrar nada.)

## 8. Eventos

### 8.1 Crear evento
1. Ir a Configuración → Eventos
2. Clic en "Nuevo Evento"
3. Llenar datos:
   - Nombre
   - Código
   - Categoría
   - Descripción
4. Crear reglas asociadas
5. Guardar

### 8.2 Crear regla
1. Seleccionar evento
2. Clic en "Nueva Regla"
3. Asociar workflow
4. Definir condición (opcional)
5. Asignar prioridad
6. Guardar

### 8.3 Tareas programadas
1. Ir a Configuración → Tareas Programadas
2. Clic en "Nueva Tarea"
3. Llenar datos:
   - Nombre
   - Expresión Cron
   - Workflow asociado
4. Guardar

### 8.4 Formato de expresión Cron
| Campo | Valores |
|---|---|
| Minutos | 0-59 |
| Horas | 0-23 |
| Día del mes | 1-31 |
| Mes | 1-12 |
| Día de la semana | 0-6 |

Ejemplos:
- `0 0 9 * * ?` - Todos los días a las 9:00 AM
- `0 0 */6 * * ?` - Cada 6 horas
- `30 2 * * 0` - Domingos a las 2:30 AM

## 9. Auditoría

### 9.1 Consultar sesiones
1. Ir a Auditoría → Sesiones
2. Filtrar por usuario, fecha, etc.
3. Exportar si es necesario

### 9.2 Consultar actividades
1. Ir a Auditoría → Actividades
2. Filtrar por módulo, acción, fecha
3. Ver detalle de cada actividad

### 9.3 Auditoría de IA
1. Ir a Auditoría → IA
2. Ver interacciones completas:
   - Usuario
   - Pregunta
   - Modelo utilizado
   - Herramientas usadas
   - Tiempo de respuesta

## 10. Configuración IA

### 10.1 Parámetros del modelo
| Parámetro | Valor | Descripción |
|---|---|---|
| Modelo | deepseek-r1:7b | Modelo de IA |
| Temperatura | 0.3 | Creatividad (0-1) |
| Max Tokens | 8192 | Longitud máxima |
| Timeout | 600s | Tiempo de espera |

### 10.2 Parámetros RAG
| Parámetro | Valor | Descripción |
|---|---|---|
| Max Chunks | 5 | Documentos recuperados |
| Similarity Threshold | 0.7 | Mínimo de similitud |
| Chunk Size | 500 | Tamaño de fragmento |
| Overlap | 50 | Solapamiento |

## 11. Backup y Recuperación

### 11.1 Backup manual
```bash
# Backup completo de BD
./Scripts/Backup/backup_sql.sh completo

# Backup diferencial
./Scripts/Backup/backup_sql.sh diferencial

# Backup de documentos
./Scripts/Backup/backup_documentos.sh
```

### 11.2 Restauración
Ver Plan de Rollback en Documentacion/Plan_Rollback.md

## 12. Monitoreo

### 12.1 Dashboard de seguridad
1. Ir a Configuración → Dashboard de Seguridad
2. Ver métricas:
   - Usuarios activos
   - Conversaciones
   - Consultas realizadas
   - Tiempo promedio de respuesta
   - Errores

### 12.2 Alertas
El sistema genera alertas automáticas para:
- Contenedores caídos
- Ollama no disponible
- SQL Server no disponible
- Disco lleno
- RAM alta

### 12.3 Logs
- Ubicación: `/app/logs/` en contenedor
- Formato: Serilog JSON
- Rotación: Diaria
- Retención: 30 días

## 13. Solución de Problemas

### 13.1 La API no responde
```bash
docker logs asistenteapi
docker restart asistenteapi
```

### 13.2 SQL Server no conecta
```bash
docker ps | grep sql
docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "AsistenteSQL2026" -C -Q "SELECT 1"
```

### 13.3 Ollama no responde
```bash
docker logs asistenteollama
curl http://localhost:11434/api/tags
```

### 13.4 ChromaDB no responde
```bash
docker logs asistentechroma
curl http://localhost:8000/api/v1/heartbeat
```

### 13.5 Los modelos no están disponibles
```bash
docker exec asistenteollama ollama list
docker exec asistenteollama ollama pull deepseek-r1:7b
docker exec asistenteollama ollama pull nomic-embed-text
```
