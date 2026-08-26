from fpdf import FPDF

class PDF(FPDF):
    def header(self):
        self.set_font('Helvetica', 'B', 10)
        self.cell(0, 10, 'TechCorp Solutions - Politica de Ciberseguridad', 0, 0, 'C')
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
pdf.cell(0, 15, 'Politica de Ciberseguridad', 0, 1, 'C')
pdf.cell(0, 15, 'y Uso de Dispositivos Tecnologicos', 0, 1, 'C')
pdf.set_font('Helvetica', '', 14)
pdf.cell(0, 10, 'TechCorp Solutions S.A. de C.V.', 0, 1, 'C')
pdf.set_font('Helvetica', '', 11)
pdf.cell(0, 8, 'Version 3.0 - Agosto 2026', 0, 1, 'C')
pdf.ln(15)

# Seccion 1
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '1. OBJETO Y ALCANCE', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'La presente Politica de Ciberseguridad establece los requisitos minimos para '
    'proteger los activos de informacion de TechCorp Solutions S.A. de C.V. (en '
    'adelante "TechCorp") frente a amenazas ciberneticas, asi como las reglas de '
    'uso de los dispositivos, sistemas y redes corporativas.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Esta politica aplica a todos los empleados, directivos, contratistas y '
    'proveedores que utilicen los activos de TechCorp o que procesen informacion '
    'de la empresa, incluyendo equipos personales en esquemas de trabajo remoto '
    '(BYOD).'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Fecha de ultima actualizacion: 1 de agosto de 2026. El responsable de esta '
    'politica es el equipo de Seguridad de la Informacion.'
))
pdf.ln(5)

# Seccion 2
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '2. CONTRASENAS Y AUTENTICACION', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Todos los sistemas y aplicaciones de TechCorp deben protegerse con contrasenas '
    'y, cuando exista la opcion, con autenticacion multifactor (MFA).'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    'Requisitos minimos de contrasenas:\n\n'
    '- Longitud minima de 14 caracteres\n'
    '- Deben combinar mayusculas, minusculas, numeros y simbolos\n'
    '- Queda prohibido reutilizar contrasenas entre sistemas de TechCorp y cuentas '
    'personales\n'
    '- Las contrasenas deben cambiarse cada 90 dias\n'
    '- Queda prohibido compartir contrasenas con otros colaboradores\n'
    '- Se recomienda el uso de administradores de contrasenas aprobados por '
    'TechCorp'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'La autenticacion multifactor (MFA) es obligatoria para acceder al correo '
    'electronico corporativo, a la VPN y a cualquier sistema que contenga datos '
    'clasificados como Confidencial o Secreto.'
))
pdf.ln(5)

# Seccion 3
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '3. USO ACEPTABLE DE DISPOSITIVOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Los dispositivos proporcionados por TechCorp (laptop, tablet, telefono) son '
    'para uso corporativo y deben utilizarse de forma responsable:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    '- Queda prohibida la instalacion de software no autorizado o de origen '
    'desconocido\n'
    '- Queda prohibido el uso de software pirata o sin licencia\n'
    '- Los equipos deben mantenerse con el sistema operativo y las actualizaciones '
    'de seguridad al dia\n'
    '- Los dispositivos deben bloquearse automaticamente despues de 5 minutos de '
    'inactividad\n'
    '- Queda prohibido conectar dispositivos USB no autorizados a los equipos\n'
    '- Los equipos que se pierdan o extravien deben reportarse de inmediato al '
    'equipo de seguridad'
))
pdf.ln(5)

# Seccion 4
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '4. CORREO ELECTRONICO Y PHISHING', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El correo electronico es una de las principales superficies de ataque. '
    'Todo colaborador debe:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    '- No abrir archivos adjuntos ni enlaces de remitentes desconocidos\n'
    '- Verificar la direccion del remitente antes de responder solicitudes de '
    'informacion o transferencias\n'
    '- No responder a solicitudes de credenciales o datos personales por correo\n'
    '- Reportar cualquier correo sospechoso al equipo de seguridad mediante el '
    'boton "Reportar phishing"\n'
    '- Confirmar por telefono cualquier solicitud urgente de transferencia de '
    'fondos, incluso si proviene de un directivo\n\n'
    'Los pagos y transferencias superiores a 500,000 pesos requieren doble '
    'autorizacion: del solicitante y del area de finanzas.'
))
pdf.ln(5)

# Seccion 5
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '5. REDES Y CONECTIVIDAD', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El acceso a los recursos de TechCorp debe realizarse a traves de redes '
    'seguras:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    '- El uso de redes Wi-Fi publicas o abiertas para trabajar con informacion '
    'Confidencial esta prohibido\n'
    '- El acceso remoto a la red corporativa debe realizarse exclusivamente a '
    'traves de la VPN corporativa con MFA\n'
    '- Los hotspots personales solo pueden utilizarse si estan protegidos con '
    'contrasena WPA2 o superior\n'
    '- Queda prohibido conectar routers o access points no autorizados a la red '
    'corporativa'
))
pdf.ln(5)

# Seccion 6
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '6. DISPOSITIVOS PERSONALES (BYOD)', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Los colaboradores pueden utilizar dispositivos personales para actividades '
    'laborales siempre que cumplan los siguientes requisitos:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    '- El dispositivo debe contar con cifrado de disco activado\n'
    '- Debe tener un PIN o biometria configurada para desbloqueo\n'
    '- El sistema operativo debe estar actualizado a la ultima version estable\n'
    '- Debe instalarse la solucion antivirus y de administracion de dispositivos '
    'moviles (MDM) aprobada por TechCorp\n'
    '- Queda prohibido almacenar datos clasificados como Secreto en dispositivos '
    'personales\n\n'
    'La empresa podra eliminar de forma remota la informacion corporativa '
    'almacenada en un dispositivo personal en caso de perdida, robo o egreso del '
    'colaborador.'
))
pdf.ln(5)

# Seccion 7
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '7. REPORTE DE INCIDENTES', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Todo incidente de seguridad, real o sospechoso, debe reportarse de inmediato '
    'al equipo de seguridad de la informacion.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Plazos y medios de reporte:\n\n'
    '- Incidentes de alta severidad (fuga de datos, ransomware, acceso no '
    'autorizado): reporte dentro de las 24 horas a ciberseguridad@techcorp.com\n'
    '- Perdida o robo de equipo: reporte en un maximo de 24 horas\n'
    '- Correos de phishing detectados: reporte inmediato con el boton "Reportar '
    'phishing"\n'
    '- Incidentes de baja severidad: reporte dentro de 5 dias habiles\n\n'
    'El colaborador que reporte un incidente de buena fe no sera sancionado por '
    'ello; esconder o no reportar un incidente si constituye una falta.'
))
pdf.ln(5)

# Seccion 8
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '8. TRABAJO REMOTO', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'En esquemas de trabajo remoto o hibrido, el colaborador debe garantizar un '
    'entorno de trabajo seguro:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    '- Trabajar en lugares donde la pantalla no sea visible por terceros\n'
    '- Utilizar protector de pantalla con bloqueo automatico\n'
    '- No dejar equipos o documentos desatendidos en espacios publicos\n'
    '- La informacion Confidencial no debe imprimirse en impresoras domésticas\n'
    '- Las reuniones virtuales con informacion sensible deben protegerse con '
    'contrasena y sala de espera\n'
    '- Al terminar la jornada, los equipos deben apagarse o bloquearse'
))
pdf.ln(5)

# Seccion 9
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '9. SANCIONES', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El incumplimiento de esta politica se sanciona conforme a su gravedad:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    '- Uso de software pirata o no autorizado: falta grave, suspension de 3 a 15 '
    'dias sin goce de sueldo\n'
    '- Divulgacion de credenciales de acceso: falta grave\n'
    '- Acceso no autorizado a informacion clasificada: falta muy grave, '
    'separacion del puesto\n'
    '- Fuga de datos provocada por negligencia: separacion del puesto y posible '
    'denuncia penal\n\n'
    'Las sanciones se aplican sin perjuicio de las acciones legales que TechCorp '
    'pudiera ejercer.'
))
pdf.ln(5)

# Seccion 10
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '10. CONTACTO', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Para dudas, reportes o solicitudes relacionadas con esta politica:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    'Equipo de Seguridad de la Informacion:\n'
    'Correo: ciberseguridad@techcorp.com\n'
    'Telefono: +52 55 1234 5678 ext. 1700\n\n'
    'El numero de linea directa para incidentes de seguridad es 800 832 4700 '
    'disponible las 24 horas los 365 dias del ano.'
))

pdf.output('C:\\Users\\Raul\\Desktop\\AsistenteIA\\politica_ciberseguridad.pdf')
print("PDF Politica de Ciberseguridad creado exitosamente")
