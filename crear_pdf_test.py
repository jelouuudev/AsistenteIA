from fpdf import FPDF

class PDF(FPDF):
    def header(self):
        self.set_font('Helvetica', 'B', 14)
        self.cell(0, 10, 'Manual de Usuario - Asistente Inteligente Empresarial', 0, 1, 'C')
        self.ln(5)

    def chapter(self, title, content):
        self.set_font('Helvetica', 'B', 12)
        self.cell(0, 10, title, 0, 1)
        self.set_font('Helvetica', '', 11)
        self.multi_cell(0, 7, content)
        self.ln(5)

pdf = PDF()
pdf.set_auto_page_break(auto=True, margin=15)

# Pagina 1
pdf.add_page()
pdf.chapter('1. Introduccion',
    'El Asistente Inteligente Empresarial es una plataforma basada en inteligencia artificial '
    'local que permite a las organizaciones gestionar conocimiento interno de manera eficiente. '
    'El sistema utiliza modelos de lenguaje grandes (LLM) ejecutados localmente a traves de '
    'Ollama, garantizando que los datos corporativos nunca salgan de la infraestructura de la empresa.')

pdf.chapter('2. Funcionalidades Principales',
    'El asistente ofrece las siguientes funcionalidades principales:\n\n'
    '- Chat conversacional con modelos de IA locales (DeepSeek R1)\n'
    '- Gestion documental con control de versiones\n'
    '- Procesamiento automatico de documentos PDF\n'
    '- Extraccion y normalizacion de texto\n'
    '- Division inteligente de contenido en fragmentos (chunking)\n'
    '- Administracion de usuarios y roles\n'
    '- Auditoria completa de actividades\n'
    '- Memoria de conversacion con contexto')

pdf.chapter('3. Requisitos del Sistema',
    'Para el correcto funcionamiento del sistema se requiere:\n\n'
    '- Sistema operativo: Windows 10/11 o Linux\n'
    '- .NET 8.0 Runtime o superior\n'
    '- SQL Server 2019 o superior\n'
    '- Ollama instalado con modelo DeepSeek R1 (7B)\n'
    '- Minimo 8 GB de RAM\n'
    '- 20 GB de espacio en disco\n'
    '- Conexion a internet para descarga inicial del modelo')

# Pagina 2
pdf.add_page()
pdf.chapter('4. Instalacion y Configuracion',
    'El proceso de instalacion consta de los siguientes pasos:\n\n'
    'Paso 1: Clonar el repositorio del proyecto desde GitHub.\n'
    'Paso 2: Restaurar las dependencias con el comando dotnet restore.\n'
    'Paso 3: Configurar la cadena de conexion a SQL Server en appsettings.json.\n'
    'Paso 4: Ejecutar las migraciones con dotnet ef database update.\n'
    'Paso 5: Instalar Ollama y descargar el modelo deepseek-r1:7b.\n'
    'Paso 6: Iniciar la aplicacion con dotnet run.\n'
    'Paso 7: Acceder a la interfaz web en http://localhost:5298.')

pdf.chapter('5. Gestion de Usuarios',
    'El sistema incluye tres roles predefinidos:\n\n'
    'Administrador: Tiene acceso total a todas las funcionalidades. Puede crear, modificar '
    'y eliminar usuarios, documentos, asistentes y configuraciones del sistema.\n\n'
    'Operador: Puede gestionar documentos, cargar versiones, consultar el historial de '
    'procesamiento y utilizar el chat con los asistentes configurados.\n\n'
    'Supervisor: Puede monitorear el procesamiento de documentos, revisar auditorias '
    'y generar reportes del sistema.')

pdf.chapter('6. Flujo de Procesamiento Documental',
    'El motor de procesamiento documental opera de la siguiente manera:\n\n'
    '1. El usuario sube un archivo PDF a traves de la interfaz web.\n'
    '2. El sistema valida el archivo (extension, tamano, magic bytes).\n'
    '3. Se crea un registro con estado Pendiente.\n'
    '4. El servicio en segundo plano detecta documentos pendientes.\n'
    '5. Se extrae el texto pagina por pagina usando UglyToad.PdfPig.\n'
    '6. Se normaliza el contenido (eliminacion de caracteres de control, '
    'unificacion de saltos de linea, limpieza de espacios).\n'
    '7. El texto se divide en fragmentos configurables (chunks).\n'
    '8. Cada fragmento se almacena con sus metadatos asociados.\n'
    '9. El estado se actualiza a Procesado.')

# Pagina 3
pdf.add_page()
pdf.chapter('7. Configuracion del Chunking',
    'El sistema de division de texto en fragmentos es altamente configurable:\n\n'
    'Tamano maximo del chunk: Define la cantidad maxima de caracteres por fragmento. '
    'Valor por defecto: 1000 caracteres.\n\n'
    'Solapamiento (overlap): Cantidad de caracteres que se compartan entre fragmentos '
    'consecutivos para mantener el contexto. Valor por defecto: 200 caracteres.\n\n'
    'Longitud minima: Tamano minimo aceptable para un fragmento. Si un fragmento es '
    'mas pequeno, se fusiona con el anterior. Valor por defecto: 100 caracteres.\n\n'
    'El algoritmo de chunking respeta la estructura del documento, intentando dividir '
    'por parrafos primero y luego por oraciones cuando es necesario. Nunca corta palabras.')

pdf.chapter('8. Monitoreo y Auditoria',
    'El panel de monitoreo proporciona informacion en tiempo real sobre:\n\n'
    '- Total de documentos en el sistema\n'
    '- Documentos pendientes de procesamiento\n'
    '- Documentos en proceso\n'
    '- Documentos procesados exitosamente\n'
    '- Documentos con errores\n'
    '- Total de chunks generados\n'
    '- Total de caracteres extraidos\n'
    '- Tasa de exito del procesamiento\n\n'
    'Todas las acciones realizadas por los usuarios quedan registradas en el '
    'sistema de auditoria, incluyendo accesos, modificaciones, cargas de documentos '
    'y consultas. Los logs se almacenan usando Serilog con rotacion diaria.')

# Pagina 4
pdf.add_page()
pdf.chapter('9. Manejo de Errores',
    'El sistema maneja los siguientes tipos de errores:\n\n'
    'Archivo corrupto: El sistema valida la estructura del PDF antes de procesarlo. '
    'Si detecta que el archivo esta danado, registra el error y cambia el estado a Error.\n\n'
    'Documento protegido: Si el PDF requiere contrasena para abrirse, el sistema '
    'detecta la excepcion y registra un error especifico.\n\n'
    'Error de lectura: Problemas con la codificacion o estructura interna del PDF.\n\n'
    'Error inesperado: Cualquier otra excepcion se captura y registra con detalle '
    'completo en los logs de Serilog.\n\n'
    'Todos los errores quedan registrados en la tabla de AuditoriaDocumental y '
    'se pueden consultar desde la interfaz de administracion.')

pdf.chapter('10. Arquitectura Tecnica',
    'El proyecto sigue los principios de Clean Architecture con las siguientes capas:\n\n'
    'Domain: Entidades, enums e interfaces del dominio. Sin dependencias externas.\n\n'
    'Application: Servicios de negocio, DTOs, validaciones con FluentValidation.\n\n'
    'Infrastructure: Implementaciones de repositorios, servicios tecnicos, '
    'Entity Framework Core, Background Services.\n\n'
    'API: Controladores REST con autenticacion por sesiones.\n\n'
    'Web: Interfaz de usuario basada en MVC con vistas Razor.\n\n'
    'Shared: DTOs compartidos, configuraciones y modelos de peticion/respuesta.')

# Pagina 5
pdf.add_page()
pdf.chapter('11. Preguntas Frecuentes',
    'P: Es seguro usar IA local?\n'
    'R: Si, los datos nunca salen de su infraestructura. El modelo se ejecuta '
    'completamente local via Ollama.\n\n'
    'P: Que formatos de documentos soporta?\n'
    'R: Actualmente soporta PDF. La arquitectura permite agregar soporte para '
    'Word, Excel, HTML, Markdown y texto plano en futuras versiones.\n\n'
    'P: Cuantos documentos puede procesar simultaneamente?\n'
    'R: Por defecto procesa hasta 5 documentos por ciclo del servicio en segundo '
    'plano. Este valor es configurable.\n\n'
    'P: Que pasa si el servidor se apaga mientras procesa?\n'
    'R: Los documentos quedan en estado Pendiente o EnProceso. Al reiniciar '
    'el servicio, continuaran procesandose automaticamente.\n\n'
    'P: Puedo buscar dentro de los documentos procesados?\n'
    'R: En esta etapa los documentos se procesan y almacenan. La busqueda '
    'semantica se implementara en la siguiente fase del proyecto.')

pdf.chapter('12. Soporte y Contacto',
    'Para soporte tecnico o consultas sobre el sistema, comunicarse con el '
    'equipo de desarrollo a traves del correo electronico soporte@asistenteia.local '
    'o crear un ticket en el repositorio de GitHub del proyecto.')

pdf.output('C:\\Users\\Raul\\Desktop\\AsistenteIA\\documento_prueba.pdf')
print("PDF creado exitosamente con 5 paginas y texto real.")
