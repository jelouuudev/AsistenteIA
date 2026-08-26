# 🗑️ Limpieza Total de Documentos Test

Este directorio contiene scripts para eliminar todos los documentos de prueba del sistema.

## ⚠️ ADVERTENCIA

**Estos scripts eliminarán TODOS los documentos de forma IRREVERSIBLE**

Se creará un backup del vectorstore antes de eliminarlo.

---

## 🚀 Opción 1: Limpieza Completa (Recomendada)

Elimina **TODO**: documentos de la base de datos + vectorstore

### Ejecutar:
```powershell
.\scripts\limpiar_todo.ps1
```

### ¿Qué hace?
1. ✅ Limpia el vectorstore (embeddings)
2. ✅ Elimina todos los documentos de SQL Server
3. ✅ Elimina todas las versiones, chunks, procesamientos
4. ✅ Elimina las relaciones con fuentes de conocimiento
5. ✅ Elimina las auditorías documentales

---

## 🧹 Opción 2: Solo Vectorstore

Elimina **SOLO** los embeddings, mantiene los documentos en la base de datos

### Ejecutar:
```powershell
.\scripts\limpiar_vectorstore.ps1
```

### ¿Qué hace?
1. ✅ Limpia el archivo vectors.json
2. ✅ Crea backup automático
3. ✅ Mantiene documentos en SQL Server

### Útil para:
- Reindexar todo desde cero
- Corregir problemas de embeddings
- Cambiar el modelo de embeddings

---

## 📝 Opción 3: Manual (SQL Server Management Studio)

Si prefieres ejecutar el SQL manualmente:

1. Abre **SQL Server Management Studio** o **Azure Data Studio**
2. Conéctate a tu base de datos `AsistenteDB`
3. Ejecuta el script: `scripts\limpiar_todos_documentos.sql`

---

## 🔄 Después de la Limpieza

### Paso 1: Reiniciar la aplicación
```bash
dotnet run --project Asistente.API
```

### Paso 2: Verificar que todo está limpio
- Abre la interfaz web: http://localhost:5206
- Ve a **Documentos** → Debería estar vacío
- Ve a **Indexación** → Dashboard debería mostrar 0 documentos

### Paso 3: Subir nuevos documentos
- Ve a **Documentos** → **Subir Documento**
- Sube tus PDFs
- El sistema los procesará automáticamente

### Paso 4: Verificar indexación
- Ve a **Indexación** → Verifica que los documentos estén indexados
- O ejecuta: `POST /api/indexacion/reindexar-todos`

---

## 📊 Documentos que se Eliminarán

Según tu lista, se eliminarán estos 26 documentos:

| Código | Nombre | Categoría |
|--------|--------|-----------|
| 101 | astronomia ejemplo | Normativas |
| 435 | cocina1 ... | Políticas |
| 1221 | sostenibilidad ... | Manual Técnico |
| 89 | teletrabajo1 ... | Procedimientos |
| 56 | vacaciones ,, | Manual Usuario |
| 90 | rrhh ... | Manual Usuario |
| 13 | seguridad ... | Procedimientos |
| 0 | jornadaa ... | Manual Usuario |
| 9 | viaticos ... | Políticas |
| 7 | beneficios ... | Procedimientos |
| 6 | capacitaciones ... | Políticas |
| 3 | verificacion ... | Capacitaciones |
| 2 | demo 2 ... | Manual Técnico |
| 1 | demo 1 ... | Políticas |
| 1234 | IT .... | Políticas |
| 12 | vacaciones y permisos ... | Políticas |
| 123 | ultima prueba ... | Manual Técnico |
| documento config | ... ... | Procedimientos |
| 11 | onboarding . | Procedimientos |
| DOC-CLOUD-001 | Manual CloudSync Pro ejemplo | FAQ |
| DOC-TELETRABAJO-001 | Politica de Teletrabajo | Políticas |
| DOC-TEST | Guia Desarrollo Software . | Manual Usuario |
| 15001 | Asistente2 ... | FAQ |
| 15000 | Documento Prueba v2 prueba 2 | Procedimientos |
| DOC-TEST-001 | Manual de Usuario | Manual Usuario |

---

## 🆘 Solución de Problemas

### Error: "No se pudo conectar a SQL Server"
- Verifica que SQL Server esté ejecutándose
- Verifica la cadena de conexión en `appsettings.json`
- Usa la Opción 3 (manual) con SSMS

### Error: "Acceso denegado al archivo vectors.json"
- Cierra la aplicación antes de ejecutar el script
- Ejecuta PowerShell como administrador

### Los documentos siguen apareciendo después de limpiar
- Reinicia la aplicación
- Limpia la caché del navegador (Ctrl+F5)
- Verifica que ejecutaste el script correcto

---

## 📞 Soporte

Si tienes problemas:
1. Revisa los logs en la consola de la aplicación
2. Verifica que SQL Server esté ejecutándose
3. Asegúrate de que la aplicación esté detenida antes de limpiar

---

**Última actualización:** 2026-08-01
