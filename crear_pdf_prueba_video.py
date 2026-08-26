from fpdf import FPDF

class PDF(FPDF):
    def header(self):
        self.set_font('Helvetica', 'B', 10)
        self.cell(0, 10, self.doc_title, 0, 0, 'C')
        self.ln(15)

    def footer(self):
        self.set_y(-15)
        self.set_font('Helvetica', 'I', 8)
        self.cell(0, 10, f'Pagina {self.page_no()}/{{nb}}', 0, 0, 'C')

def section(pdf, title):
    pdf.set_font('Helvetica', 'B', 13)
    pdf.cell(0, 10, title, 0, 1)
    pdf.ln(1)

def para(pdf, text):
    pdf.set_font('Helvetica', '', 11)
    pdf.multi_cell(0, 7, text)
    pdf.ln(2)

def bullets(pdf, items):
    pdf.set_font('Helvetica', '', 11)
    for item in items:
        pdf.multi_cell(0, 7, '- ' + item)
        pdf.ln(2)

def make_pdf(filename, title, cover, sections_data):
    pdf = PDF()
    pdf.doc_title = title
    pdf.set_auto_page_break(auto=True, margin=15)
    pdf.add_page()
    pdf.set_font('Helvetica', 'B', 22)
    pdf.ln(25)
    for line in cover['title']:
        pdf.cell(0, 15, line, 0, 1, 'C')
    pdf.set_font('Helvetica', '', 14)
    pdf.cell(0, 10, 'TechCorp Solutions S.A. de C.V.', 0, 1, 'C')
    pdf.set_font('Helvetica', '', 11)
    pdf.cell(0, 8, cover['version'], 0, 1, 'C')
    pdf.ln(12)
    for s in sections_data:
        section(pdf, s['title'])
        if 'text' in s:
            para(pdf, s['text'])
        if 'bullets' in s:
            bullets(pdf, s['bullets'])
    pdf.output(filename)
    print(filename + ' creado exitosamente')

# 1) Manual de Producto CloudSync Pro
make_pdf(
    'C:\\Users\\Raul\\Desktop\\AsistenteIA\\pdf_prueba_manual_producto.pdf',
    'TechCorp Solutions - Manual del Producto CloudSync Pro',
    {'title': ['Manual del Producto', 'CloudSync Pro'], 'version': 'Version 2.1 - Agosto 2026'},
    [
        {'title': '1. DESCRIPCION DEL PRODUCTO',
         'text': 'CloudSync Pro es la plataforma de sincronizacion y almacenamiento en la nube de TechCorp Solutions, disenada para equipos empresariales que necesitan compartir archivos de forma segura y en tiempo real.'},
        {'title': '2. CARACTERISTICAS PRINCIPALES',
         'bullets': ['Sincronizacion en tiempo real entre dispositivos con latencia menor a 2 segundos',
                     'Cifrado de extremo a extremo (AES-256) en reposo y en transito',
                     'Capacidad de 1 TB por usuario en el plan Empresarial',
                     'Control de versiones con historial de hasta 90 dias',
                     'Sincronizacion selectiva por carpetas y bandas de ancho de banda configurables',
                     'Integracion con Microsoft 365, Google Workspace y Slack']},
        {'title': '3. PLANES Y PRECIOS',
         'bullets': ['Plan Gratuito: 5 GB, maximo 3 dispositivos, sin soporte prioritario - $0',
                     'Plan Pro: 500 GB, 5 dispositivos, soporte en horario laboral - $12 USD por usuario/mes',
                     'Plan Empresarial: 1 TB, dispositivos ilimitados, soporte 24/7 - $24 USD por usuario/mes',
                     'Plan Enterprise: almacenamiento ilimitado, SSO y API dedicada - cotizacion personalizada']},
        {'title': '4. REQUISITOS DEL SISTEMA',
         'bullets': ['Windows 10 o superior (64 bits)',
                     'macOS 12 o superior',
                     'iOS 15 y Android 11 en dispositivos moviles',
                     'Conexion a internet minima de 5 Mbps para sincronizacion',
                     'Memoria RAM recomendada de 4 GB o mas']},
        {'title': '5. SOPORTE TECNICO',
         'text': 'Los clientes del plan Empresarial cuentan con soporte 24/7 via correo (soporte@techcorp.com), chat en vivo y telefono (+52 55 1234 5678). El tiempo de respuesta garantizado para incidentes criticos es de 15 minutos.'},
    ],
)

# 2) Politica de Vacaciones y Permisos
make_pdf(
    'C:\\Users\\Raul\\Desktop\\AsistenteIA\\pdf_prueba_vacaciones.pdf',
    'TechCorp Solutions - Politica de Vacaciones y Permisos',
    {'title': ['Politica de', 'Vacaciones y Permisos'], 'version': 'Version 1.4 - Agosto 2026'},
    [
        {'title': '1. OBJETIVO',
         'text': 'Esta politica regula el derecho a vacaciones y los permisos de los colaboradores de TechCorp Solutions, garantizando el equilibrio entre la vida personal y laboral.'},
        {'title': '2. DERECHO A VACACIONES',
         'bullets': ['Primer ano: 12 dias habiles de vacaciones',
                     'Segundo ano: 14 dias habiles',
                     'Del segundo al cuarto ano: se agregan 2 dias por cada ano',
                     'A partir del quinto ano: 20 dias habiles',
                     'Los dias de vacaciones deben solicitarse con minimo 15 dias de anticipacion',
                     'Hasta 10 dias pueden acumularse para el siguiente ano; el resto caduca']},
        {'title': '3. PERMISOS REMUNERADOS',
         'bullets': ['Permiso por fallecimiento de familiar directo: 5 dias habiles',
                     'Permiso por matrimonio: 5 dias habiles',
                     'Permiso por nacimiento de hijo (papaprimaria): 20 dias naturales',
                     'Permiso por mudanza: 1 dia habil al ano',
                     'Permiso por citas medicas: hasta 4 horas por mes',
                     'Permiso de paternidad extendido: 5 dias adicionales no remunerados']},
        {'title': '4. PERMISOS NO REMUNERADOS',
         'text': 'Los colaboradores pueden solicitar permisos sin goce de sueldo por motivos personales o academicos, con una duracion maxima de 90 dias naturales al ano. La solicitud debe presentarse con al menos 30 dias de anticipacion y requiere la aprobacion del lider directo y de Recursos Humanos.'},
        {'title': '5. PROCESO DE SOLICITUD',
         'text': 'Todas las solicitudes de vacaciones y permisos se gestionan a traves del portal interno de RH (https://rh.techcorp.com). El lider directo debe aprobar o rechazar la solicitud en un plazo maximo de 5 dias habiles. En caso de rechazo, el motivo debe quedar documentado en el sistema.'},
    ],
)

# 3) Manual de Onboarding de Nuevos Colaboradores
make_pdf(
    'C:\\Users\\Raul\\Desktop\\AsistenteIA\\pdf_prueba_onboarding.pdf',
    'TechCorp Solutions - Manual de Onboarding',
    {'title': ['Manual de', 'Onboarding para Nuevos', 'Colaboradores'], 'version': 'Version 1.1 - Julio 2026'},
    [
        {'title': '1. BIENVENIDA',
         'text': 'Bienvenido a TechCorp Solutions. Este manual te guiara durante tus primeros 30 dias en la empresa, con el objetivo de que te integres rapidamente al equipo y conozcas nuestras herramientas, procesos y cultura.'},
        {'title': '2. PRIMER DIA',
         'bullets': ['Reportarse en la recepcion a las 9:00 am en la sede central (Av. Reforma 250, Piso 8)',
                     'Recibir equipo: laptop corporativa, celular, credencial de acceso y materiales',
                     'Crear o restablecer la contrasena corporativa (requisitos: 14 caracteres, mayusculas, minusculas, numeros y simbolos)',
                     'Activar la autenticacion multifactor (MFA) con la aplicacion aprobada',
                     'Tomar la foto oficial para la credencial y el perfil de intranet']},
        {'title': '3. PRIMERA SEMANA',
         'bullets': ['Sesion de induccion con Recursos Humanos (2 horas)',
                     'Alta en sistemas: correo, VPN, Slack, Jira y herramientas de facturacion',
                     'Sesion de seguridad de la informacion y firma del acuerdo de confidencialidad',
                     'Asignacion de buddy (companero guia) que acompanara durante el primer mes',
                     'Revision del plan de trabajo y objetivos del periodo de prueba']},
        {'title': '4. PRIMEROS 30 DIAS',
         'bullets': ['Completar los cursos obligatorios del Campus TechCorp (seguridad, compliance y herramientas)',
                     'Asistir a las sesiones de onboarding semanal de tu area',
                     'Reunion de retroalimentacion con el lider directo al cumplir 30 dias',
                     'Evaluacion del periodo de prueba: el resultado se comunica en un plazo de 15 dias',
                     'La confirmacion del puesto se realiza al cumplir 90 dias, conforme al contrato']},
        {'title': '5. BENEFICIOS DISPONIBLES DESDE EL DIA 1',
         'bullets': ['Seguro de gastos medicos mayores con cobertura de 50 millones de pesos',
                     'Dia de cumpleanos libre y con goce de sueldo',
                     'Fondo de ahorro con aportacion 50/50 de hasta el 13% del salario',
                     'Bono de bienvenida de 10,000 pesos pagado en la primera quincena',
                     'Acceso al gimnasio corporativo y a la sala de bienestar']},
    ],
)

# 4) Politica de Viaticos y Gastos de Viaje
make_pdf(
    'C:\\Users\\Raul\\Desktop\\AsistenteIA\\pdf_prueba_viaticos.pdf',
    'TechCorp Solutions - Politica de Viaticos y Gastos de Viaje',
    {'title': ['Politica de', 'Viaticos y', 'Gastos de Viaje'], 'version': 'Version 2.0 - Agosto 2026'},
    [
        {'title': '1. OBJETIVO',
         'text': 'Establecer los limites, procedimientos y requisitos para la solicitud, aprobacion y reembolso de gastos de viaje y viaticos de los colaboradores de TechCorp Solutions.'},
        {'title': '2. GASTOS PERMITIDOS',
         'bullets': ['Transporte aereo: clase economica para vuelos menores a 4 horas; business solo con autorizacion previa',
                     'Hotel: tarifa maxima de 2,500 pesos por noche en ciudad y 3,200 en zona metropolitana',
                     'Alimentacion: 700 pesos diarios por persona',
                     'Transporte terrestre (taxis y aplicaciones): 500 pesos diarios',
                     'Lavanderia en viajes de mas de 5 dias: hasta 400 pesos',
                     'Propinas: incluidas en los limites de cada rubro']},
        {'title': '3. LIMITES Y APROBACIONES',
         'bullets': ['Gastos hasta 15,000 pesos: aprueba el lider directo',
                     'Gastos de 15,001 a 50,000 pesos: aprueba el lider directo y el area de Finanzas',
                     'Gastos mayores a 50,000 pesos: requieren aprobacion de la direccion de area',
                     'Los anticipos de viaticos se liquidan en un plazo de 30 dias',
                     'Los gastos deben reportarse en el sistema dentro de los 10 dias habiles posteriores al regreso']},
        {'title': '4. REEMBOLSO DE GASTOS',
         'text': 'Los reembolsos se procesan contra la entrega de comprobantes fiscales validos (CFDI) a nombre de TechCorp Solutions. El tiempo de pago es de 10 a 15 dias habiles despues de la validacion de Finanzas. Los gastos sin comprobante no seran reembolsados, salvo casos excepcionales autorizados por la direccion.'},
        {'title': '5. USO DE LA TARJETA CORPORATIVA',
         'text': 'La tarjeta corporativa de TechCorp puede utilizarse para gastos de viaje dentro de los limites establecidos. Esta prohibido su uso para gastos personales o de entretenimiento. Cualquier uso indebido sera sujeto a las sanciones del codigo de conducta, incluyendo la separacion del puesto.'},
    ],
)
