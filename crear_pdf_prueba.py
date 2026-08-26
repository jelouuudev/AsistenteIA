from fpdf import FPDF

class PDF(FPDF):
    def header(self):
        self.set_font('Helvetica', 'B', 10)
        self.cell(0, 10, 'Politica de Seguridad Informatica v2.0', 0, 0, 'C')
        self.ln(15)

pdf = PDF()
pdf.set_auto_page_break(auto=True, margin=15)
pdf.add_page()

pdf.set_font('Helvetica', 'B', 18)
pdf.cell(0, 15, 'Politica de Seguridad Informatica', 0, 1, 'C')
pdf.set_font('Helvetica', '', 10)
pdf.cell(0, 8, 'Empresa: TechCorp Solutions S.A.', 0, 1, 'C')
pdf.cell(0, 8, 'Version: 2.0  |  Fecha: Enero 2025', 0, 1, 'C')
pdf.ln(10)

# Seccion 1
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '1. Objetivo', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Esta politica tiene como objetivo establecer las directrices y procedimientos de seguridad '
    'informatica que deben seguir todos los empleados, contratistas y usuarios del sistema de '
    'informacion de TechCorp Solutions S.A. La politica aplica a todos los activos de información '
    'y sistemas de información de la empresa, incluyendo hardware, software, redes y datos.'
))
pdf.ln(5)

# Seccion 2
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '2. Alcance', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Esta politica aplica a todo el personal de TechCorp Solutions, incluyendo empleados '
    'de planta, personal temporal, contratistas, consultores externos y cualquier persona '
    'que tenga acceso a los sistemas de informacion de la empresa. El alcance cubre '
    'todos los departamentos: Desarrollo, Recursos Humanos, Finanzas, Operaciones '
    'y Administracion.'
))
pdf.ln(5)

# Seccion 3
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '3. Clasificacion de la Informacion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Toda la informacion de la empresa debe ser clasificada en una de las siguientes categorias '
    'segun su nivel de sensibilidad y criticidad para el negocio:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.1 Confidencial', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Informacion de acceso restringido que, si fuera divulgada, podria causar danos '
    'significativos a la empresa. Incluye: secretos comerciales, información financiera '
    'no publica, datos personales de clientes, estrategias de negocio y propiedad intelectual. '
    'Solo pueden acceder las personas autorizadas explicitamente.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.2 Interna', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Informacion de uso interno que no debe ser compartida con personas externas a la empresa. '
    'Incluye: correos electronicos internos, documentos de procedimientos, organigramas, '
    'calendarios de proyectos y reuniones internas.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.3 Publica', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Informacion disponible para cualquier persona. Incluye: pagina web corporativa, '
    'materiales de marketing publicados, comunicados de prensa y informacion de contacto '
    'de la empresa.'
))
pdf.ln(5)

# Seccion 4
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '4. Control de Acceso', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El control de acceso a los sistemas de informacion se rige por los siguientes principios:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '4.1 Principio de menor privilegio', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Cada usuario debe tener acceso unicamente a la informacion y los recursos necesarios '
    'para realizar sus funciones laborales. No se concede acceso adicional sin una justificacion '
    'documentada y aprobacion del supervisor directo y del area de TI.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '4.2 Autenticacion multifactor', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Todos los usuarios deben utilizar autenticacion de dos factores (2FA) para acceder '
    'a los sistemas criticos. Esto incluye una combinacion de contrasena y un codigo '
    'generado por una aplicacion de autenticacion o un token fisico.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '4.3 Revison periodica de accesos', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El area de TI debe realizar una revision de todos los accesos concedidos cada 90 dias. '
    'Los accesos que ya no sean necesarios deben ser revocados inmediatamente. Los '
    'resultados de la revision deben documentarse y archivarse.'
))
pdf.ln(5)

# Seccion 5
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '5. Politica de Contrasenas', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Todos los usuarios deben cumplir con los siguientes requisitos para sus contrasenas:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    '- Minimo 12 caracteres de longitud.\n'
    '- Debe contener al menos una mayuscula, una minuscula, un numero y un caracter especial.\n'
    '- No puede contener el nombre de usuario, fecha de nacimiento ni palabras del diccionario.\n'
    '- Debe cambiarse cada 60 dias como maximo.\n'
    '- No se permite reutilizar las ultimas 5 contrasenas anteriores.\n'
    '- Esta prohibido compartir contrasenas con otros usuarios.\n'
    '- Se debe utilizar un gestor de contrasenas aprobado por la empresa.'
))
pdf.ln(5)

# Seccion 6
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '6. Procedimiento ante Incidentes de Seguridad', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'En caso de detectar o sospechar un incidente de seguridad informatica, se debe '
    'seguir el siguiente procedimiento:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, 'Paso 1: Identificacion del incidente', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El usuario que detecte el incidente debe documentar todos los detalles disponibles: '
    'fecha, hora, tipo de incidente, sistemas afectados y cualquier evidencia recolectada.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, 'Paso 2: Contencion inmediata', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Si es posible y seguro hacerlo, el usuario debe aislar el sistema afectado '
    'desconectandolo de la red. No se debe apagar el equipo ya que se perderia evidencia '
    'valiosa para el analisis forense.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, 'Paso 3: Notificacion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Notificar inmediatamente al equipo de Seguridad Informatica a traves del correo '
    'seguridad@techcorp.com o al telefono interno ext. 5555. La notificacion debe '
    'realizarse dentro de los primeros 30 minutos despues de la deteccion.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, 'Paso 4: Analisis y erradicacion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El equipo de Seguridad realizara un analisis completo del incidente, identificara '
    'la causa raiz, erradicara la amenaza y restaurara los sistemas a su estado normal. '
    'Este proceso tiene un objetivo de resolucion de 4 horas para incidentes criticos.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, 'Paso 5: Documentacion y lecciones aprendidas', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Despues de resolver el incidente, se debe elaborar un informe detallado con la '
    'cronologia de eventos, acciones tomadas y recomendaciones para prevenir incidentes '
    'similares en el futuro. Este informe se presenta a la direccion general.'
))
pdf.ln(5)

# Seccion 7
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '7. Uso Aceptable de los Recursos', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Los recursos de TI de la empresa son proporcionados exclusivamente para fines '
    'laborales. El uso personal esta permitido de manera moderada siempre que no '
    'interfiera con las responsabilidades laborales ni comprometa la seguridad. '
    'Esta estrictamente prohibido: instalar software no autorizado, conectar dispositivos '
    'personales no homologados, usar redes no cifradas para transmitir información '
    'confidencial, y compartir credenciales de acceso.'
))
pdf.ln(5)

# Seccion 8
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '8. Copias de Seguridad', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El area de TI debe realizar copias de seguridad de toda la informacion critica '
    'con la siguiente frecuencia: copias diarias de bases de datos, copias semanales '
    'de sistemas completos, y copias mensuales de archivadores. Las copias deben '
    'almacenarse en una ubicacion geograficamente separada y cifradas con AES-256. '
    'Se deben realizar pruebas de restauracion trimestrales para garantizar la '
    'integridad de las copias de seguridad.'
))
pdf.ln(5)

# Seccion 9
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '9. Sanciones por Incumplimiento', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El incumplimiento de esta politica de seguridad informatica podra dar lugar a '
    'sanciones disciplinarias que incluyen: amonestacion escrita, suspension temporal, '
    'terminacion del contrato laboral, y acciones legales en caso de danos materiales '
    'o vulneracion de datos personales. La gravedad de la sancion dependera de la '
    'naturaleza del incumplimiento, el dano causado y si hubo intencionalidad.'
))
pdf.ln(5)

# Seccion 10
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '10. Revision y Actualizacion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Esta politica debe ser revisada y actualizada al menos una vez al año o cuando '
    'se produzcan cambios significativos en la infraestructura tecnologica, la normativa '
    'aplicable o la estructura organizativa de la empresa. Las actualizaciones deben '
    'ser aprobadas por el Director de Tecnologia y comunicadas a todos los empleados '
    'dentro de los 5 dias habiles siguientes a su aprobacion.'
))
pdf.ln(8)

# Contacto
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '11. Informacion de Contacto', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Para consultas, dudas o reportes relacionados con esta politica de seguridad, '
    'contactar al equipo de Seguridad Informatica:\n\n'
    'Correo electronico: seguridad@techcorp.com\n'
    'Telefono interno: ext. 5555\n'
    'Horario de atencion: Lunes a Viernes de 8:00 a 18:00\n'
    'Para emergencias de seguridad fuera de horario: +52 55 1234 5678'
))

pdf.output('C:\\Users\\Raul\\Desktop\\AsistenteIA\\documento_prueba2.pdf')
print("PDF creado exitosamente")
