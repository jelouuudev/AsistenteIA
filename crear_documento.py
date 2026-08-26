from fpdf import FPDF
import os

class PDF(FPDF):
    def header(self):
        self.set_font('Arial', 'B', 10)
        self.cell(0, 8, 'TechCorp - Politica de Teletrabajo v2.0', align='R', new_x='LMARGIN', new_y='NEXT')
        self.line(10, self.get_y(), 200, self.get_y())
        self.ln(4)

    def footer(self):
        self.set_y(-15)
        self.set_font('Arial', 'I', 8)
        self.cell(0, 10, f'Pagina {self.page_no()}/{{nb}}', align='C')

    def section_title(self, num, title):
        self.set_font('Arial', 'B', 13)
        self.ln(4)
        self.cell(0, 10, f'{num}. {title}', new_x='LMARGIN', new_y='NEXT')
        self.set_font('Arial', '', 10)

    def body_text(self, text):
        self.multi_cell(0, 6, text)
        self.ln(2)

    def sub_title(self, title):
        self.set_font('Arial', 'B', 11)
        self.cell(0, 8, title, new_x='LMARGIN', new_y='NEXT')
        self.set_font('Arial', '', 10)

    def bullet(self, text):
        x = self.get_x()
        self.set_x(x + 5)
        self.multi_cell(0, 6, f'- {text}')
        self.set_x(x)

pdf = PDF()
pdf.alias_nb_pages()
pdf.set_auto_page_break(auto=True, margin=20)

arial_path = r'C:\Windows\Fonts\arial.ttf'
arial_bold_path = r'C:\Windows\Fonts\arialbd.ttf'
arial_italic_path = r'C:\Windows\Fonts\ariali.ttf'
arial_bi_path = r'C:\Windows\Fonts\arialbi.ttf'

pdf.add_font('Arial', '', arial_path, uni=True)
pdf.add_font('Arial', 'B', arial_bold_path, uni=True)
pdf.add_font('Arial', 'I', arial_italic_path, uni=True)
pdf.add_font('Arial', 'BI', arial_bi_path, uni=True)

pdf.add_page()

# Title
pdf.set_font('Arial', 'B', 20)
pdf.ln(10)
pdf.cell(0, 15, 'Politica de Teletrabajo', align='C', new_x='LMARGIN', new_y='NEXT')
pdf.set_font('Arial', '', 11)
pdf.cell(0, 8, 'TechCorp S.A. de C.V.', align='C', new_x='LMARGIN', new_y='NEXT')
pdf.cell(0, 8, 'Version 2.0 - Enero 2025', align='C', new_x='LMARGIN', new_y='NEXT')
pdf.ln(8)

# 1. Objetivo
pdf.section_title('1', 'Objetivo')
pdf.body_text(
    'Establecer las lineas maestras para la implementacion del modelo de trabajo remoto '
    'en TechCorp, garantizando la productividad, la seguridad de la informacion y el '
    'bienestar de los colaboradores que se beneficien de esta modalidad.'
)

# 2. Ambito de Aplicacion
pdf.section_title('2', 'Ambito de Aplicacion')
pdf.body_text(
    'Esta politica aplica a todos los empleados de TechCorp que figuren en el programa '
    'de teletrabajo aprobado por su gerente directo y el departamento de Recursos Humanos. '
    'Incluye empleados de planta, plazo fijo y consultores externos con relacion laboral '
    'vigente. No aplica a pasantes ni a personal de terceros sin contrato directo.'
)

# 3. Modalidades de Trabajo
pdf.section_title('3', 'Modalidades de Trabajo')
pdf.body_text('TechCorp ofrece tres modalidades de trabajo:')
pdf.ln(2)
pdf.sub_title('3.1 Teletrabajo Total')
pdf.body_text(
    'El colaborador trabaja 100% de manera remota. Debe contar con espacio de trabajo '
    'adecuado, conexion a internet de al menos 50 Mbps y horario flexible coordinado '
    'con su equipo. Asiste a la oficina solo para reuniones presenciales obligatorias '
    'maximo una vez al mes.'
)
pdf.sub_title('3.2 Teletrabajo Parcial')
pdf.body_text(
    'El colaborador combina dias en oficina y dias remotos. El esquema minimo es 2 dias '
    'remotos y 3 dias en oficina por semana. Los dias remotos deben ser aprobados por '
    'el gerente y comunicados con al menos 48 horas de anticipacion.'
)
pdf.sub_title('3.3 Modalidad Hibrida por Equipos')
pdf.body_text(
    'Equipos completos pueden adoptar un esquema hibrido donde todos los miembros '
    'trabajan remoto los martes y jueves, y asisten a oficina los lunes, miercoles y '
    'viernes. Esta modalidad facilita la coordinacion y las reuniones de equipo.'
)

# 4. Requisitos Tecnicos
pdf.section_title('4', 'Requisitos Tecnicos')
pdf.body_text('El colaborador en teletrabajo debe cumplir con los siguientes requisitos minimos:')
pdf.bullet('Conexion a internet estable con minimo 50 Mbps de bajada y 10 Mbps de subida.')
pdf.bullet('Computadora proporcionada por la empresa con software actualizado y antivirus activo.')
pdf.bullet('Uso obligatorio de VPN corporativa para acceder a sistemas internos.')
pdf.bullet('Webcam y microfono funcional para reuniones de videoconferencia.')
pdf.bullet('Espacio de trabajo seguro, silencioso y libre de distracciones.')
pdf.bullet('UPS o proteccion contra cortes de electricidad para evitar perdida de datos.')
pdf.ln(3)

# 5. Horario y Disponibilidad
pdf.section_title('5', 'Horario y Disponibilidad')
pdf.body_text(
    'El horario laboral en teletrabajo es el mismo que en oficina: 9:00 a 18:00 horas '
    'con una hora de almuerzo. El colaborador debe estar disponible en los canales de '
    'comunicacion corporativos (Microsoft Teams, correo electronico) durante todo el '
    'horario laboral.'
)
pdf.body_text(
    'Se permite horario flexible previa aprobacion del gerente, siempre que se cumplan '
    'las 8 horas diarias de trabajo y se mantenga disponibilidad en las franjas de '
    '10:00 a 14:00 horas, que es el horario nucleo de convivencia del equipo.'
)
pdf.body_text(
    'Las ausencias o tardanzas deben comunicarse al gerente directo con al menos '
    '30 minutos de anticipacion. Se registran en el sistema de control de asistencia.'
)

# 6. Herramientas de Comunicacion
pdf.section_title('6', 'Herramientas de Comunicacion')
pdf.body_text('Las herramientas autorizadas para trabajo remoto son:')
pdf.bullet('Microsoft Teams: Comunicacion diaria, reuniones y llamadas.')
pdf.bullet('Jira: Gestion de tareas y seguimiento de proyectos.')
pdf.bullet('Confluence: Documentacion tecnica y conocimiento del equipo.')
pdf.bullet('GitHub: Repositorios de codigo y revision de pull requests.')
pdf.bullet('Slack: Comunicacion no urgente y canales tematicos.')
pdf.bullet('Zoom: Reuniones con clientes externos que no usan Teams.')
pdf.ln(3)
pdf.body_text(
    'Esta prohibido el uso de herramientas no autorizadas para comunicacion corporativa. '
    'No se deben enviar archivos confidenciales por WhatsApp, Telegram u otras aplicaciones '
    'de mensajeria personal.'
)

# 7. Seguridad en Teletrabajo
pdf.section_title('7', 'Seguridad en Teletrabajo')
pdf.body_text(
    'Los colaboradores en teletrabajo deben cumplir con todas las politicas de seguridad '
    'informatica de la empresa, con las siguientes adaptaciones adicionales:'
)
pdf.bullet('Uso obligatorio de VPN corporativa en todo momento al acceder a sistemas de la empresa.')
pdf.bullet('Prohibido usar redes publicas de wifi (cafe, aeropuerto, etc.) sin VPN activa.')
pdf.bullet('Cifrado de disco duro obligatorio en la computadora de trabajo.')
pdf.bullet('Bloqueo automatico de pantalla despues de 2 minutos de inactividad.')
pdf.bullet('No se permite el uso de la computadora corporativa para uso personal.')
pdf.bullet('Reporte inmediato al equipo de seguridad ante cualquier incidente o sospecha de brecha.')
pdf.ln(3)
pdf.body_text(
    'El departamento de TI realiza auditorias aleatorias cada trimestre para verificar el '
    'cumplimiento de estas medidas. El incumplimiento genera amonestacion y posible '
    'revocacion del permiso de teletrabajo.'
)

# 8. Productividad y Seguimiento
pdf.section_title('8', 'Productividad y Seguimiento')
pdf.body_text(
    'La productividad del colaborador en teletrabajo se mide por resultados, no por '
    'horas frente a la pantalla. Los indicadores principales son:'
)
pdf.bullet('Cumplimiento de entregables definidos en la planificacion del sprint.')
pdf.bullet('Calidad del codigo medida por metricas de SonarQube (cobertura minima: 80%).')
pdf.bullet('Participacion activa en reuniones de equipo (standup diario obligatorio).')
pdf.bullet('Tiempo de respuesta en canales de comunicacion (maximo 30 minutos en horario laboral).')
pdf.bullet('Satisfaccion del cliente interno evaluada en encuestas trimestrales.')
pdf.ln(3)
pdf.body_text(
    'El gerente directo realiza una revision de desempeno mensual con cada colaborador '
    'remoto. Si se detecta una caida significativa en la productividad, se activa un '
    'plan de mejora con seguimiento semanal por un periodo de 30 dias.'
)

# 9. Bienestar y Ergonomia
pdf.section_title('9', 'Bienestar y Ergonomia')
pdf.body_text(
    'TechCorp se compromete a garantizar las condiciones adecuadas para el bienestar '
    'de los colaboradores en teletrabajo:'
)
pdf.bullet('Dotacion inicial: Silla ergonomica, escritorio ajustable y monitor externo.')
pdf.bullet('Subsidio mensual de $500 MXN para gastos de internet y electricidad.')
pdf.bullet('Acceso gratuito a la plataforma de ejercicios corporativos.')
pdf.bullet('Jornada de bienestar virtual todos los viernes de 16:00 a 17:00 horas.')
pdf.bullet('Evaluacion postural anual con especialista en ergonomia (cubierta por la empresa).')
pdf.ln(3)
pdf.body_text(
    'Los colaboradores pueden solicitar equipo adicional al departamento de TI mediante '
    'el portal de soporte. El equipo debe devolverse al finalizar la relacion laboral.'
)

# 10. Gestion de Incidencias
pdf.section_title('10', 'Gestion de Incidencias')
pdf.body_text(
    'Ante cualquier problema tecnico durante el teletrabajo, el colaborador debe seguir '
    'este procedimiento:'
)
pdf.bullet('Paso 1: Verificar que el problema no sea de la red local (reiniciar router, verificar cables).')
pdf.bullet('Paso 2: Consultar la base de conocimiento en Confluence para soluciones conocidas.')
pdf.bullet('Paso 3: Crear un ticket en el portal de soporte TI con descripcion detallada del problema.')
pdf.bullet('Paso 4: Si el problema es critico (sin acceso a sistemas), llamar a la linea de soporte: ext. 7000.')
pdf.bullet('Paso 5: Seguir las instrucciones del equipo de TI y documentar la resolucion.')
pdf.ln(3)
pdf.body_text(
    'El tiempo de respuesta del equipo de soporte para incidencias criticas es de maximo '
    '30 minutos en horario laboral. Para incidencias no criticas, el SLA es de 4 horas.'
)

# 11. Sanciones por Incumplimiento
pdf.section_title('11', 'Sanciones por Incumplimiento')
pdf.body_text(
    'El incumplimiento de esta politica de teletrabajo podra dar lugar a las siguientes '
    'acciones:'
)
pdf.bullet('Primera falta: Amonestacion verbal y registro en expediente del colaborador.')
pdf.bullet('Segunda falta: Amonestacion escrita con plan de mejora obligatorio.')
pdf.bullet('Tercera falta: Suspension temporal del permiso de teletrabajo por 30 dias.')
pdf.bullet('Faltas graves: Revocacion permanente del teletrabajo y posible accion disciplinaria.')
pdf.ln(3)
pdf.body_text(
    'Se consideran faltas graves: usar la computadora corporativa para actividades ilegales, '
    'compartir credenciales de acceso, desconectar el sistema de monitoreo de productividad, '
    'y trabajar desde ubicaciones no autorizadas sin notificacion previa.'
)

# 12. Contacto
pdf.section_title('12', 'Contacto')
pdf.body_text(
    'Para dudas, aclaraciones o solicitudes relacionadas con esta politica de teletrabajo, '
    'contactar a:'
)
pdf.bullet('Departamento de Recursos Humanos: rrhh@techcorp.com, ext. 3000')
pdf.bullet('Soporte Tecnico TI: soporte@techcorp.com, ext. 7000')
pdf.bullet('Coordinacion de Teletrabajo: teletrabajo@techcorp.com, ext. 3500')
pdf.bullet('Emergencias fuera de horario: +52 55 9876 5432')

output_path = r'C:\Users\Raul\Desktop\AsistenteIA\v1_politica_teletrabajo.pdf'
pdf.output(output_path)
print(f'Documento creado en: {output_path}')
