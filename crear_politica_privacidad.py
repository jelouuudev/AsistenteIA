from fpdf import FPDF

class PDF(FPDF):
    def header(self):
        self.set_font('Helvetica', 'B', 10)
        self.cell(0, 10, 'TechCorp Solutions - Politica de Privacidad', 0, 0, 'C')
        self.ln(15)

    def footer(self):
        self.set_y(-15)
        self.set_font('Helvetica', 'I', 8)
        self.cell(0, 10, f'Pagina {self.page_no()}/{{nb}}', 0, 0, 'C')

pdf = PDF()
pdf.set_auto_page_break(auto=True, margin=15)
pdf.add_page()

# Portada
pdf.set_font('Helvetica', 'B', 22)
pdf.ln(25)
pdf.cell(0, 15, 'Politica de Privacidad', 0, 1, 'C')
pdf.set_font('Helvetica', '', 14)
pdf.cell(0, 10, 'TechCorp Solutions S.A. de C.V.', 0, 1, 'C')
pdf.set_font('Helvetica', '', 11)
pdf.cell(0, 8, 'Version 4.0 - Agosto 2026', 0, 1, 'C')
pdf.ln(15)

# Seccion 1
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '1. INFORMACION GENERAL', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'TechCorp Solutions S.A. de C.V. (en adelante "TechCorp", "nosotros" o "la empresa") '
    'con domicilio en Av. Reforma 505, Piso 20, Colonia Cuauhtemoc, C.P. 06500, Ciudad de '
    'Mexico, Mexico, respeta y protege la privacidad de los datos personales de sus usuarios, '
    'clientes y visitantes (en adelante "el Usuario").'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'La presente Politica de Privacidad (en adelante la "Politica") tiene por objeto informar '
    'al Usuario sobre el tratamiento de sus datos personales, los fines para los cuales se '
    'recaban, los derechos que le asisten y las medidas de seguridad implementadas.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Al utilizar nuestros servicios, el Usuario acepta los terminos de esta Politica. '
    'Fecha de ultima actualizacion: 1 de agosto de 2026.'
))
pdf.ln(5)

# Seccion 2
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '2. DATOS PERSONALES QUE RECABAMOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Para las finalidades senaladas en esta Politica, podemos recabar sus datos personales '
    'de diferentes formas:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '2.1 Datos Recabados Directamente del Usuario', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Cuando el Usuario se registra, contrata servicios o nos contacta, podemos recabar:\n\n'
    '- Datos de identificacion: Nombre completo, razon social, RFC, CURP\n'
    '- Datos de contacto: Correo electronico, telefono, direccion fisica\n'
    '- Datos de facturación: Domicilio fiscal, datos bancarios, informacion fiscal\n'
    '- Datos de acceso: Usuario, contrasena (almacenada de forma cifrada)\n'
    '- Datos profesionales: Cargo, empresa, industria, tamano de organizacion\n'
    '- Datos de comunicacion: Historial de correos, llamadas, reuniones'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '2.2 Datos Recabados Automaticamente', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Cuando el Usuario utiliza nuestros servicios o visita nuestro sitio web, recabamos:\n\n'
    '- Datos de navegacion: Direccion IP, tipo de navegador, sistema operativo\n'
    '- Datos de uso: Paginas visitadas, tiempo de permanencia, clics realizados\n'
    '- Datos de dispositivo: Modelo, marca, resolucion de pantalla, idioma\n'
    '- Cookies y tecnologias similares: Ver seccion 8 de esta Politica\n'
    '- Datos de localizacion: Geolocalizacion aproximada (ciudad/pais)\n'
    '- Datos de rendimiento: Velocidad de carga, errores, metricas tecnicas'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '2.3 Datos de Terceros', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'En algunos casos, podemos recibir datos personales de terceros:\n\n'
    '- Referencias comerciales proporcionadas por el Usuario\n'
    '- Datos de contactos de emergencia o personas autorizadas\n'
    '- Informacion de partners tecnologicos y proveedores\n'
    '- Datos de redes sociales cuando el Usuario inicia sesion con ellas'
))
pdf.ln(5)

# Seccion 3
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '3. FINALIDADES DEL TRATAMIENTO DE DATOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Sus datos personales seran utilizados para las siguientes finalidades primarias:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.1 Finalidades Primarias (No requieren consentimiento)', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Prestacion de los servicios contratados por el Usuario\n'
    '- Facturacion y emision de comprobantes fiscales\n'
    '- Atencion a solicitudes y requerimientos del Usuario\n'
    '- Envio de notificaciones relacionadas con el servicio\n'
    '- Verificacion de identidad y prevencion de fraude\n'
    '- Cumplimiento de obligaciones legales y regulatorias\n'
    '- Administracion de la relacion contractual'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.2 Finalidades Secundarias (Requieren consentimiento)', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Envio de comunicaciones comerciales y promocionales\n'
    '- Invitaciones a eventos, webinars y capacitaciones\n'
    '- Realizacion de encuestas de satisfaccion\n'
    '- Analisis de comportamiento para mejorar servicios\n'
    '- Personalizacion de contenido y recomendaciones\n'
    '- Estudios de mercado e investigacion de clientes\n'
    '- Perfilamiento para ofertas personalizadas'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'El Usuario puede oponerse en cualquier momento al tratamiento de sus datos para '
    'finalidades secundarias enviando un correo a privacidad@techcorp.com'
))
pdf.ln(5)

# Seccion 4
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '4. TRANSFERENCIA DE DATOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'TechCorp no comparte sus datos personales con terceros sin su consentimiento, '
    'salvo en los siguientes casos:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '4.1 Transferencias Autorizadas por Ley', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Autoridades fiscales (SAT) para cumplimiento de obligaciones fiscales\n'
    '- Autoridades judiciales en cumplimiento de ordenes judiciales\n'
    '- Autoridades regulatorias (INAI, CONDUSEF, etc.)\n'
    '- Procuraduria General de la Republica en casos de investigacion'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '4.2 Transferencias a Proveedores de Servicios', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Compartimos datos con proveedores que nos ayudan a operar nuestros servicios:\n\n'
    '- Proveedores de hosting y cloud: Amazon Web Services (AWS), Microsoft Azure\n'
    '- Pasarelas de pago: Stripe, PayPal, OpenPay\n'
    '- Servicios de email: SendGrid, Amazon SES\n'
    '- Herramientas de analisis: Google Analytics, Mixpanel\n'
    '- Soporte al cliente: Zendesk, Intercom\n\n'
    'Todos los proveedores estan obligados contractualmente a proteger sus datos '
    'y utilizarlos unicamente para los fines especificados.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '4.3 Transferencias Internacionales', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Algunos de nuestros proveedores almacenan datos en servidores ubicados fuera de Mexico. '
    'En estos casos:\n\n'
    '- Se implementan clausulas contractuales estandar aprobadas por el INAI\n'
    '- Se garantiza un nivel de proteccion adecuado de los datos\n'
    '- Se realizan auditorias periodicas de cumplimiento\n'
    '- El Usuario puede solicitar informacion sobre las transferencias en privacidad@techcorp.com'
))
pdf.ln(5)

# Seccion 5
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '5. DERECHOS ARCO', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El Usuario tiene los siguientes derechos sobre sus datos personales (Derechos ARCO):'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '5.1 Derecho de Acceso', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Conocer que datos personales se tienen sobre el Usuario, para que fines se utilizan '
    'y las condiciones del uso que se les da. Para ejercer este derecho:\n\n'
    '- Enviar solicitud a privacidad@techcorp.com\n'
    '- Incluir nombre completo, correo electronico y documento de identificacion\n'
    '- Especificar que informacion desea conocer\n'
    '- Respuesta en un plazo maximo de 20 dias habiles'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '5.2 Derecho de Rectificacion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Solicitar la correccion de datos personales cuando sean inexactos o incompletos:\n\n'
    '- Indicar que datos desea corregir y la correccion solicitada\n'
    '- Proporcionar documentacion que respalde la correccion\n'
    '- La correccion se realizara en un plazo de 15 dias habiles\n'
    '- Se notificara al Usuario una vez realizada la rectificacion'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '5.3 Derecho de Cancelacion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Solicitar la supresion de sus datos personales cuando considere que no estan siendo '
    'utilizados conforme a esta Politica:\n\n'
    '- Enviar solicitud indicando que datos desea eliminar\n'
    '- La cancelacion procedera cuando:\n'
    '  * Los datos ya no sean necesarios para los fines para los que fueron recabados\n'
    '  * El Usuario haya revocado su consentimiento\n'
    '  * Se haya vencido el plazo de conservacion establecido\n'
    '- Excepciones: Obligaciones legales, prescripcion de acciones, responsabilidad contractual'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '5.4 Derecho de Oposicion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Oponerse al uso de sus datos personales para fines especificos:\n\n'
    '- Oposicion a recibir comunicaciones comerciales\n'
    '- Oposicion a la realizacion de perfilamiento\n'
    '- Oposicion a la cesion de datos a terceros\n'
    '- La oposicion sera atendida en un plazo de 10 dias habiles'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '5.5 Derecho a la Portabilidad', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Solicitar una copia de sus datos personales en formato estructurado para transferirlos '
    'a otro proveedor de servicios:\n\n'
    '- Formato: JSON, CSV o XML segun preferencia del Usuario\n'
    '- Incluye datos proporcionados directamente por el Usuario\n'
    '- No incluye datos generados por TechCorp (analisis, reportes)\n'
    '- Entrega en plazo de 30 dias habiles'
))
pdf.ln(5)

# Seccion 6
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '6. MEDIDAS DE SEGURIDAD', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'TechCorp ha implementado medidas de seguridad administrativas, tecnicas y fisicas '
    'para proteger sus datos personales:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '6.1 Medidas Tecnicas', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Cifrado de datos en transito: TLS 1.3\n'
    '- Cifrado de datos en reposo: AES-256\n'
    '- Autenticacion multifactor (MFA) obligatoria\n'
    '- Firewall de nueva generacion con deteccion de intrusiones\n'
    '- Monitoreo 24/7 de sistemas y redes\n'
    '- Copias de seguridad diarias con cifrado\n'
    '- Pruebas de penetracion trimestrales\n'
    '- Escaneo de vulnerabilidades semanal'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '6.2 Medidas Administrativas', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Politicas de privacidad y confidencialidad firmadas por todo el personal\n'
    '- Capacitacion anual en proteccion de datos\n'
    '- Control de accesos basado en principio de minimo privilegio\n'
    '- Auditorias internas trimestrales\n'
    '- Procedimientos de respuesta a incidentes documentados\n'
    '- Evaluacion de riesgos anual de privacidad'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '6.3 Medidas Fisicas', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Acceso restringido a areas de servidores con biometria\n'
    '- Videovigilancia 24/7 en instalaciones criticas\n'
    '- Control de visitantes con registro y escolta\n'
    '- Destruccion segura de documentos fisicos\n'
    '- Generadores electricos de respaldo\n'
    '- Sistemas de supresion de incendios con gas inerte'
))
pdf.ln(5)

# Seccion 7
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '7. CONSERVACION DE DATOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Los datos personales se conservan durante el tiempo necesario para cumplir con '
    'las finalidades para las que fueron recabados:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '7.1 Plazos de Conservacion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Datos de clientes activos: Durante la vigencia de la relacion contractual + 5 anos\n'
    '- Datos de prospectos: 3 anos desde el ultimo contacto\n'
    '- Datos de facturacion: 5 anos (obligacion fiscal)\n'
    '- Datos de navegacion: 12 meses\n'
    '- Cookies: Segun tipo (ver seccion 8)\n'
    '- Registros de auditoria: 3 anos\n'
    '- Datos de soporte: 2 anos desde el cierre del ticket'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '7.2 Eliminacion de Datos', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Una vez vencido el plazo de conservacion:\n\n'
    '- Los datos se eliminan de forma segura usando metodos de borrado certificado\n'
    '- Se mantienen unicamente los datos necesarios para cumplir obligaciones legales\n'
    '- El Usuario puede solicitar la eliminacion anticipada (ver seccion 5.3)\n'
    '- Se notifica al Usuario antes de la eliminacion si hay saldo pendiente'
))
pdf.ln(5)

# Seccion 8
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '8. COOKIES Y TECNOLOGIAS SIMILARES', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Utilizamos cookies y tecnologias similares para mejorar la experiencia del Usuario:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '8.1 Tipos de Cookies', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Cookies Esenciales:\n'
    '- Necesarias para el funcionamiento del sitio\n'
    '- No requieren consentimiento del Usuario\n'
    '- Duracion: Sesion o maximo 1 ano\n\n'
    'Cookies de Rendimiento:\n'
    '- Recopilan informacion sobre el uso del sitio\n'
    '- Requieren consentimiento del Usuario\n'
    '- Duracion: Maximo 2 anos\n\n'
    'Cookies de Funcionalidad:\n'
    '- Permiten recordar preferencias del Usuario\n'
    '- Requieren consentimiento del Usuario\n'
    '- Duracion: Maximo 1 ano\n\n'
    'Cookies de Publicidad:\n'
    '- Permiten mostrar anuncios personalizados\n'
    '- Requieren consentimiento del Usuario\n'
    '- Duracion: Maximo 2 anos'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '8.2 Gestion de Cookies', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El Usuario puede gestionar sus preferencias de cookies:\n\n'
    '- Desde el panel de configuracion de cookies en nuestro sitio web\n'
    '- Desde la configuracion de su navegador\n'
    '- Desactivando cookies de terceros especificos\n\n'
    'Nota: La desactivacion de cookies esenciales puede afectar el funcionamiento del sitio.'
))
pdf.ln(5)

# Seccion 9
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '9. MENORES DE EDAD', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Nuestros servicios estan dirigidos a mayores de 18 anos. No recabamos intencionalmente '
    'datos personales de menores de edad.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Si un padre o tutor descubre que su hijo ha proporcionado datos personales a TechCorp '
    'sin su consentimiento, puede solicitar su eliminacion enviando un correo a '
    'privacidad@techcorp.com. Eliminaremos los datos lo antes posible.'
))
pdf.ln(5)

# Seccion 10
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '10. CAMBIOS A ESTA POLITICA', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'TechCorp se reserva el derecho de modificar esta Politica en cualquier momento.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Los cambios seran notificados:\n\n'
    '- Mediante correo electronico a los usuarios registrados\n'
    '- A traves de un aviso visible en nuestro sitio web\n'
    '- Con al menos 30 dias de anticipacion a su entrada en vigor\n\n'
    'El uso continuado de nuestros servicios despues de la entrada en vigor de los cambios '
    'constituye aceptacion de los mismos.'
))
pdf.ln(5)

# Seccion 11
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '11. CONTACTO', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Para cualquier duda, comentario o solicitud relacionada con esta Politica de Privacidad:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, 'Oficial de Privacidad', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Nombre: Lic. Maria Elena Rodriguez Gutierrez\n'
    'Correo: privacidad@techcorp.com\n'
    'Telefono: +52 55 1234 5678 ext. 1500\n'
    'Horario: Lunes a Viernes, 9:00 a 18:00 horas\n\n'
    'Domicilio: Av. Reforma 505, Piso 20, Colonia Cuauhtemoc, C.P. 06500, Ciudad de Mexico'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, 'Autoridad de Proteccion de Datos', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Si el Usuario considera que sus derechos de proteccion de datos han sido vulnerados, '
    'puede presentar una queja ante el Instituto Nacional de Transparencia, Acceso a la '
    'Informacion y Proteccion de Datos Personales (INAI):\n\n'
    'Sitio web: www.gob.mx/inai\n'
    'Telefono: 01 800 832 4723\n'
    'Domicilio: Avenida Insurgentes Sur 3211, Santa Teresa, C.P. 04500, Ciudad de Mexico'
))

pdf.output('C:\\Users\\Raul\\Desktop\\AsistenteIA\\politica_privacidad.pdf')
print("PDF Politica de Privacidad creado exitosamente")
