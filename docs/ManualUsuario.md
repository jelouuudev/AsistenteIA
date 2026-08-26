# Manual de Usuario - Asistente Inteligente Empresarial

## Funcionalidades Principales

El Asistente Inteligente Empresarial ofrece las siguientes funcionalidades:

### 1. Chat Conversacional con IA
- Interacción con modelos de IA locales (DeepSeek R1) mediante Ollama
- Conversaciones con contexto y memoria
- Selección de asistentes especializados con diferentes personalidades
- Respuestas basadas en documentos de la organización (RAG)

### 2. Gestión Documental Inteligente
- Registro y administración de documentos PDF
- Control de versiones automático (v1, v2, v3...)
- Procesamiento automático: extracción de texto, normalización y división en fragmentos (chunking)
- Indexación vectorial para búsqueda semántica
- Categorización y organización de documentos

### 3. Administración de Usuarios y Roles
- Creación, edición y desactivación de usuarios
- Asignación de roles y permisos
- Tres roles predefinidos: Administrador, Operador, Supervisor
- Validación de contraseñas seguras

### 4. Auditoría y Seguimiento
- Registro completo de sesiones de usuario
- Historial de actividades del sistema
- Trazabilidad de acciones por usuario, módulo y fecha
- Auditoría específica por documento

### 5. Administración de Asistentes y Prompts
- Creación de asistentes especializados con configuración personalizada
- Gestión de prompts con versionado automático
- Panel de pruebas para validar prompts antes de activarlos
- Configuración de idioma, formalidad, formato de respuesta y restricciones

### 6. Fuentes de Conocimiento
- Creación de fuentes que agrupan documentos
- Asignación de fuentes a asistentes específicos
- Control de prioridades entre fuentes
- Filtrado de búsqueda por fuente

## Requisitos previos

- Ollama instalado y ejecutándose
- SQL Server ejecutándose
- Aplicación Web accesible en `http://localhost:5206`

## Inicio de sesión

### Acceder al sistema

1. Abra su navegador en `http://localhost:5206`
2. Será redirigido automáticamente a la página de login
3. Ingrese sus credenciales:
   - **Usuario**: Su nombre de usuario
   - **Contraseña**: Su contraseña

### Usuarios iniciales

El sistema se inicializa con los siguientes usuarios:

| Usuario | Contraseña | Rol | Permisos |
|---------|------------|-----|----------|
| admin | Admin123* | Administrador | Acceso completo (Chat, Usuarios, Roles, Auditoría) |
| operador | Operador123* | Operador | Solo Chat (Asistente IA) |
| supervisor | Supervisor123* | Supervisor | Chat y Auditoría |

### Cerrar sesión

1. Haga clic en el botón **"Salir"** en la esquina superior derecha
2. Será redirigido a la página de login

## Interfaz principal

### Menú de navegación

El menú superior muestra las opciones disponibles según su rol:

- **Asistente**: Disponible para Administrador y Operador
- **Usuarios**: Disponible solo para Administrador
- **Roles**: Disponible solo para Administrador
- **Auditoría**: Disponible para Administrador y Supervisor

### Información de usuario

En la esquina superior derecha verá:
- Su nombre completo
- Sus roles asignados (badges)
- Botón para cerrar sesión

## Módulo de Chat (Asistente IA)

### Interfaz de chat

La interfaz consta de:

- **Área de conversación**: Muestra el historial de mensajes con la IA
- **Campo de texto**: Para escribir sus preguntas
- **Botón Enviar**: Envía el mensaje a la IA
- **Botón Limpiar**: Limpia la conversación actual
- **Indicador de carga**: Animación mientras la IA genera respuesta

### Cómo usar el asistente

1. Escriba su pregunta en el campo de texto
2. Presione Enter o haga clic en "Enviar"
3. Espere la respuesta de la IA
4. Continúe la conversación o limpie el historial

### Ejemplos de uso

- "¿Cuál es la capital de Francia?"
- "Explícame qué es Clean Architecture"
- "Ayúdame a redactar un correo formal"
- "Resumen de los beneficios de la programación asíncrona"

## Módulo de Usuarios (Solo Administrador)

### Lista de usuarios

1. En el menú, haga clic en **"Usuarios"**
2. Verá la lista de todos los usuarios del sistema
3. Cada fila muestra: Usuario, Nombres, Apellidos, Correo, Estado, Roles

### Crear un nuevo usuario

1. Haga clic en el botón **"Crear Usuario"**
2. Complete el formulario:
   - **Usuario**: Nombre de usuario (único)
   - **Nombres**: Nombres del usuario
   - **Apellidos**: Apellidos del usuario
   - **Correo**: Correo electrónico
   - **Contraseña**: Contraseña (mínimo 8 caracteres, mayúscula, minúscula, número, carácter especial)
   - **Roles**: Seleccione los roles a asignar
3. Haga clic en **"Guardar"**

### Editar un usuario

1. En la lista de usuarios, haga clic en **"Editar"** en el usuario deseado
2. Modifique los campos necesarios
3. Puede agregar o quitar roles
4. Haga clic en **"Guardar"**

### Desactivar un usuario

1. En la lista de usuarios, haga clic en **"Desactivar"**
2. Confirme la acción
3. El usuario ya no podrá iniciar sesión

### Cambiar contraseña

1. En la lista de usuarios, haga clic en **"Cambiar Contraseña"**
2. Ingrese la nueva contraseña
3. Haga clic en **"Guardar"**

## Módulo de Roles (Solo Administrador)

### Lista de roles

1. En el menú, haga clic en **"Roles"**
2. Verá la lista de todos los roles del sistema
3. Cada fila muestra: Nombre, Descripción, Estado

### Crear un nuevo rol

1. Haga clic en el botón **"Crear Rol"**
2. Complete el formulario:
   - **Nombre**: Nombre del rol (único)
   - **Descripción**: Descripción del rol
   - **Activo**: Marque para activar el rol
3. Haga clic en **"Guardar"**

### Editar un rol

1. En la lista de roles, haga clic en **"Editar"** en el rol deseado
2. Modifique los campos necesarios
3. Haga clic en **"Guardar"**

### Activar/Desactivar un rol

1. En la lista de roles, cambie el estado del rol
2. Los roles desactivados no pueden asignarse a usuarios

## Módulo de Auditoría (Administrador y Supervisor)

### Sesiones de usuario

1. En el menú, haga clic en **"Auditoría"**
2. Verá dos pestañas: **"Sesiones"** y **"Actividades"**
3. En la pestaña **"Sesiones"** verá:
   - Usuario
   - Fecha de inicio
   - Fecha de fin (si aplica)
   - Dirección IP
   - Navegador
   - Estado (Exitoso, Fallido, Inactivo, Cerrado)

### Actividades del sistema

1. En la pestaña **"Actividades"** verá:
   - Usuario
   - Fecha y hora
   - Módulo (Usuarios, Roles, etc.)
   - Acción (Creación, Modificación, etc.)
   - Descripción detallada
   - Dirección IP

### Filtros de búsqueda

- Puede filtrar por usuario
- Puede filtrar por fecha
- Puede filtrar por módulo
- Puede filtrar por acción

## Seguridad de contraseñas

### Requisitos de contraseña

- Mínimo 8 caracteres
- Al menos una letra mayúscula
- Al menos una letra minúscula
- Al menos un número
- Al menos un carácter especial (!@#$%^&*)

### Ejemplos de contraseñas válidas

- `Password123*`
- `Admin@2024`
- `SecurePass!1`

### Ejemplos de contraseñas inválidas

- `password` (sin mayúscula, número, carácter especial)
- `Password` (sin número, carácter especial)
- `PASSWORD123` (sin minúscula, carácter especial)

## 5. Administración de Asistentes

### 5.1 Crear un asistente
1. Vaya a **Asistentes** en el menú de navegación.
2. Haga clic en **Crear Asistente**.
3. Complete los campos:
   - **Nombre**: nombre del asistente (ej. "Asistente Jurídico").
   - **Descripción**: propósito del asistente.
   - **Modelo IA**: modelo de Ollama a utilizar (por defecto `deepseek-r1:7b`).
   - **Idioma**: `es` (español), `en` (inglés), `pt` (portugués), `fr` (francés).
   - **Formalidad**: formal, profesional, casual o amigable.
   - **Formato de respuesta**: texto, markdown, html o json.
   - **Restricciones**: reglas de comportamiento (ej. "No responder temas políticos").
   - **Mensaje de bienvenida**: mensaje inicial que el asistente mostrará al saludar.
   - **Temperatura**: creatividad del modelo (0.0 = determinista, 2.0 = muy creativo).
   - **Máx. tokens**: límite de tokens por respuesta.
   - **Timeout**: tiempo máximo de espera a Ollama en segundos.
4. Haga clic en **Guardar**.

### 5.2 Editar un asistente
1. Desde el listado de **Asistentes**, haga clic en **Editar** junto al asistente deseado.
2. Modifique los campos necesarios.
3. Haga clic en **Guardar**.

### 5.3 Activar/Desactivar un asistente
- Use los botones **Activar** / **Desactivar** en el listado.
- Solo los asistentes activos pueden usarse desde el chat.

## 6. Administración de Prompts

### 6.1 Crear un prompt
1. Vaya a **Prompts** en el menú de navegación.
2. Haga clic en **Crear Prompt**.
3. Seleccione el **asistente** al que pertenece el prompt.
4. Escriba el **nombre** y el **contenido** del prompt (instrucciones del sistema).
5. Haga clic en **Guardar** (se crea como versión 1, inactivo por defecto).

### 6.2 Editar un prompt (versionado automático)
1. Desde el listado de **Prompts**, haga clic en **Editar**.
2. Modifique el contenido o el nombre.
3. Haga clic en **Guardar** — el sistema crea automáticamente una nueva versión (v2, v3, etc.) y registra el cambio en el historial.
4. Las versiones anteriores nunca se pierden y pueden consultarse en **Historial**.

### 6.3 Activar/Desactivar un prompt
- Use los botones **Activar** / **Desactivar**.
- Solo un prompt puede estar activo por asistente.
- Al activar un prompt, los demás del mismo asistente se desactivan automáticamente.

### 6.4 Duplicar un prompt
- Haga clic en **Duplicar** para copiar un prompt existente.
- La copia se crea con el nombre " (copia)", versión 1 y estado inactivo.
- Útil para crear variantes a partir de un prompt existente.

### 6.5 Historial de versiones
1. En el listado de **Prompts**, haga clic en **Historial**.
2. Podrá ver todas las versiones anteriores del prompt.
3. La vista muestra: versión, contenido, fecha de modificación, usuario y motivo del cambio.

## 7. Panel de Pruebas de Prompts

### 7.1 Probar un asistente con un prompt
1. Vaya a **Prompts** y haga clic en **Probar** junto al prompt deseado.
2. Seleccione el **asistente** (se preselecciona el del prompt).
3. Escriba un **mensaje** de prueba.
4. Haga clic en **Enviar**.
5. El sistema muestra:
   - **Prompt generado**: el System Prompt completo (configuración del asistente + contenido del prompt). Puede ocultarse/mostrarse con el botón **Mostrar/Ocultar Prompt**.
   - **Respuesta**: la respuesta del modelo IA.

### 7.2 Interpretar los resultados
- Si la respuesta no es la esperada, ajuste el **contenido del prompt**, las **restricciones** o la **temperatura** del asistente.
- Vuelva a realizar la prueba cuantas veces sea necesario sin salir de la página.

## 8. Chat con selección de asistente

1. Vaya a **Chat** en el menú de navegación.
2. Seleccione un **asistente** del menú desplegable (solo muestra asistentes activos).
3. Escriba su mensaje en el campo de texto.
4. El sistema usará el prompt activo del asistente seleccionado para guiar la respuesta.
5. Puede cambiar de asistente en cualquier momento para recibir respuestas con distinta personalidad.

## Solución de problemas

### No puedo iniciar sesión

- Verifique que su usuario esté activo (contacte al administrador)
- Verifique que esté escribiendo correctamente su usuario y contraseña
- Si olvidó su contraseña, contacte al administrador

### El asistente no responde

- Verifique que Ollama esté ejecutándose
- Verifique que el modelo de Ollama esté instalado
- Intente recargar la página

### No veo opciones en el menú

- Verifique su rol asignado
- Contacte al administrador si cree que debería tener más permisos

### Error al crear usuario

- Verifique que el nombre de usuario no exista
- Verifique que la contraseña cumpla los requisitos
- Verifique que el correo sea válido

## Soporte

Para problemas técnicos o solicitudes de soporte, contacte al administrador del sistema o consulte el Manual Técnico para más detalles.

## 9. Módulo Gestor Documental

El Gestor Documental permite registrar y subir los documentos de la organización que servirán de entrenamiento/conocimiento para el Asistente IA (en formato PDF).

### 9.1 Consultar y Filtrar Documentos
1. En el menú superior, haga clic en el enlace principal **"Documentos"** (disponible para Administradores y Operadores).
2. Verá la pantalla del repositorio de documentos donde se muestra la lista actual.
3. Puede usar el panel superior para realizar búsquedas rápidas mediante filtros:
   * **Nombre o Código:** Escriba palabras clave del título del documento o su código único.
   * **Categoría:** Seleccione una categoría en el desplegable (ej. Manual Usuario, Políticas).
   * **Estado:** Filtre por Borrador, Activo o Archivado.
   * **Fechas (Desde/Hasta):** Acote los resultados según su fecha de registro en el sistema.
4. Haga clic en el botón de la lupa para aplicar los filtros.

### 9.2 Registrar y Subir un Nuevo Documento
1. Desde la pantalla principal de documentos, haga clic en **"Subir Documento"**.
2. Complete el formulario con la información requerida:
   * **Código Único:** Un código identificativo alfanumérico (ej: `POL-003`). El sistema no permitirá códigos duplicados.
   * **Nombre del Documento:** Título claro y descriptivo (ej: `Política de Trabajo Híbrido 2026`).
   * **Categoría:** Seleccione a qué categoría pertenece de la lista.
   * **Archivo PDF:** Seleccione el archivo PDF a subir de su computadora (es obligatorio para poder guardar).
   * **Descripción:** Redacte un breve resumen o información complementaria sobre el documento.
3. Haga clic en **"Crear y Cargar PDF"**. El sistema registrará el documento en estado *Borrador* y subirá automáticamente la versión *v1*, activándolo de inmediato si la subida fue exitosa.

### 9.3 Gestión de Versiones y Detalle
1. En la lista de documentos, haga clic en **"Detalle"** en el renglón correspondiente al documento que desea inspeccionar.
2. La vista de detalles le muestra:
   * **Ficha Técnica:** Metadatos generales, fecha, estado actual e indicador de procesamiento inteligente.
   * **Historial de Versiones:** Una lista con todos los archivos subidos para este documento (v1, v2, v3...), su peso, fecha de subida y hash de seguridad SHA-256.
   * **Auditoría del Documento:** Historial con nombre, IP y fecha de cada acción ejecutada en este documento.
3. **Cargar una Nueva Versión:**
   * Haga clic en **"Nueva Versión"** en la parte superior.
   * Seleccione el nuevo archivo PDF correspondiente al documento.
   * Presione **"Subir Archivo"**. Esto incrementará el número de versión automáticamente (ej: v1 -> v2) y lo marcará de nuevo como *Pendiente de Procesamiento* para que el motor de IA integre los nuevos datos.
4. **Descargar una Versión Anterior o Actual:**
   * En el historial de versiones del documento, localice la versión deseada y haga clic en el botón verde con el icono de descarga (flecha hacia abajo).
   * El navegador iniciará la descarga del archivo PDF exacto de esa versión.

### 9.4 Cambiar Estado o Eliminar un Documento
1. En la vista de **Detalle** del documento, localice el botón **"Cambiar Estado"** en la esquina superior derecha:
   * **Activar:** El documento se marca como disponible para el sistema.
   * **Archivar (Desactivar):** El documento se almacena históricamente pero ya no estará disponible para consultas del asistente de IA.
   * **Eliminar:** Elimina lógicamente el documento (cambia su estado interno a Eliminado). Ya no se mostrará en el listado ni estará accesible, aunque conserva su registro de auditoría en la base de datos de manera oculta.

### 9.5 Administrar Categorías (Solo Administrador)
1. Despliegue el menú **"Configuración"** en la barra superior y elija la opción **"Categorías Doc."**.
2. Verá el panel de control de categorías donde se muestran todas las clasificaciones vigentes (Normativas, Procedimientos, FAQ, etc.).
3. **Crear Categoría:** Haga clic en **"Nueva Categoría"**, asigne un nombre y descripción y guarde.
4. **Editar Categoría:** Haga clic en **"Editar"** en la tarjeta de la categoría. Podrá modificar su nombre, descripción o desactivarla (si la desactiva, ya no se podrá asociar a nuevos documentos).

### 9.6 Solución de Problemas del Gestor Documental
* **Error: Extensión no permitida:** El sistema solo admite archivos con la extensión `.pdf`. Asegúrese de que su archivo termina en `.pdf`.
* **Error: El archivo excede el tamaño máximo:** Su archivo pesa más del límite permitido por el sistema (ej. 50 MB). Intente comprimir el PDF o consulte con el administrador del sistema para aumentar el límite.
* **Error: El archivo no es un documento PDF válido o está corrupto:** El sistema inspecciona los bytes internos del archivo para corroborar su integridad. Si el archivo está dañado, no se guardó bien o se renombró otro tipo de archivo (ej. un `.docx` a `.pdf`), el sistema lo rechazará por seguridad. Vuelva a guardar o exportar el PDF desde el programa de origen y suba la nueva versión.

