# =============================================================================
# MANUAL DE USUARIO
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 27
# =============================================================================

## 1. Inicio de Sesión

### 1.1 Acceder al sistema
1. Abrir navegador web
2. Ir a: http://localhost:5206
3. Ingresar credenciales:
   - Usuario
   - Contraseña
4. Clic en "Iniciar Sesión"

### 1.2 Usuarios de prueba
| Usuario | Contraseña | Rol |
|---|---|---|
| admin | Admin123* | Administrador |
| operador | Operador123* | Operador |
| supervisor | Supervisor123* | Supervisor |

### 1.3 Cerrar sesión
1. Clic en el menú de usuario (esquina superior derecha)
2. Seleccionar "Cerrar Sesión"

## 2. Selección del Asistente

### 2.1 Asistentes disponibles
| Asistente | Descripción |
|---|---|
| Asistente General | Consultas generales de la empresa |
| Asistente de Soporte Técnico | Consultas técnicas |
| Asistente de RRHH | Consultas de recursos humanos |

### 2.2 Seleccionar asistente
1. En la pantalla principal, ir al selector de asistentes
2. Seleccionar el asistente deseado
3. El chat se actualizará con el contexto del asistente seleccionado

## 3. Uso del Chat

### 3.1 Enviar mensaje
1. Escribir la pregunta en el campo de texto
2. Clic en "Enviar" o presionar Enter
3. Esperar respuesta del asistente

### 3.2 Ejemplos de preguntas
- "¿Cuántos activos tenemos registrados?"
- "¿Qué procedimientos hay para solicitar vacaciones?"
- "Muéstrame los clientes activos"
- "¿Cuál es el inventario actual?"

### 3.3 Historial de conversación
- Las conversaciones se guardan automáticamente
- Se puede ver el historial en el panel izquierdo
- Clic en cualquier conversación para ver su contenido

### 3.4 Crear nueva conversación
1. Clic en "Nueva Conversación"
2. Seleccionar asistente
3. Comenzar a preguntar

## 4. Consulta de Información

### 4.1 Consultas SQL
El asistente puede consultar información de la base de datos:
- "¿Cuántos usuarios hay?"
- "Lista de productos activos"
- "Total de ventas este mes"

### 4.2 Consultas RAG (Documentos)
El asistente puede consultar documentos:
- "¿Qué dice el manual de procedimientos?"
- "Políticas de vacaciones"
- "Requisitos para solicitar un préstamo"

### 4.3 Consultas combinadas
El asistente puede combinar información:
- "Según el manual, ¿cuál es el proceso para...?"
- "Compara los datos de la base con el documento..."

## 5. Uso de Documentos

### 5.1 Ver documentos disponibles
1. Ir a Documentos
2. Ver lista de documentos procesados
3. Clic en un documento para ver detalles

### 5.2 Buscar en documentos
1. Usar el chat para preguntar sobre documentos específicos
2. El asistente buscará en ChromaDB
3. Mostrará fragmentos relevantes

## 6. Solicitud de Reportes

### 6.1 Generar reporte
1. Pedir al asistente: "Genera un reporte de..."
2. El asistente usará ReportTool
3. El reporte se generará en PDF

### 6.2 Tipos de reportes
- Reporte de activos
- Reporte de usuarios
- Reporte de inventario
- Reporte personalizado

## 7. Ejecución de Procesos (Workflows)

### 7.1 ¿Qué es un workflow?
Un workflow es un proceso compuesto por múltiples pasos que se ejecutan secuencialmente.

### 7.2 Solicitar un workflow
1. Usar las frases disparadoras configuradas:
   - "Consulta de activos"
   - "Reporte de clientes"
2. El asistente identificará el proceso
3. Ejecutará los pasos automáticamente
4. Mostrará el resultado final

### 7.3 Ejemplo de uso
- Usuario: "Consulta activos"
- Sistema:
  1. Consulta SQL a la base de datos
  2. Genera reporte con los datos
  3. Presenta resultado al usuario

## 8. Visualización de Resultados

### 8.1 Formato de respuesta
Las respuestas pueden incluir:
- Texto explicativo
- Tablas de datos
- Referencias documentales
- Archivos generados (PDF)

### 8.2 Referencias documentales
- El asistente muestra qué documentos usó
- Se puede hacer clic para ver el documento original
- Se muestra la página y fragmento relevante

### 8.3 Fuentes de datos
- SQL Server: datos estructurados
- RAG: documentos y manuales
- Herramientas: cálculos, fechas, etc.

## 9. Funciones Administrativas (Solo Administradores)

### 9.1 Gestión de usuarios
- Crear nuevos usuarios
- Asignar roles
- Activar/desactivar cuentas

### 9.2 Gestión de asistentes
- Crear asistentes
- Asignar herramientas
- Configurar prompts

### 9.3 Gestión de documentos
- Subir documentos
- Categorizar
- Procesar

### 9.4 Ver auditoría
- Sesiones de usuarios
- Actividades del sistema
- Consultas realizadas

## 10. Atajos de Teclado

| Atajo | Acción |
|---|---|
| Enter | Enviar mensaje |
| Shift + Enter | Nueva línea |
| Ctrl + N | Nueva conversación |

## 11. Preguntas Frecuentes

### 11.1 ¿Por qué el asistente no responde?
- Verificar conexión a internet
- Verificar que el servicio Ollama esté corriendo
- Contactar al administrador

### 11.2 ¿Cómo cambio mi contraseña?
1. Ir a Configuración → Mi cuenta
2. Seleccionar "Cambiar contraseña"
3. Ingresar contraseña actual y nueva
4. Guardar

### 11.3 ¿Puedo usar el asistente en móvil?
Sí, la interfaz web es responsive y funciona en tablets y teléfonos.

### 11.4 ¿Qué hacer si obtengo un error?
1. Anotar el mensaje de error
2. Capturar pantalla
3. Reportar al administrador

## 12. Consejos de Uso

### 12.1 Para mejores resultados
- Sé específico en tus preguntas
- Usa lenguaje natural
- Si no entiendes la respuesta, pide aclaración
- Usa las frases disparadoras para procesos

### 12.2 Ejemplos útiles
- "¿Cuántos X hay?"
- "Lista de X activos"
- "¿Qué dice el manual sobre X?"
- "Genera un reporte de X"
- "Compara X con Y"

## 13. Soporte

Si necesitas ayuda:
1. Consulta este manual
2. Contacta al administrador del sistema
3. Revisa los logs de error
4. Verifica el estado de los servicios

---

**Nota:** Este manual es una guía general. La funcionalidad específica puede variar según la configuración de tu sistema y los permisos asignados.
