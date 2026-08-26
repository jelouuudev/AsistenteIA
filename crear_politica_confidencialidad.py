from fpdf import FPDF

class PDF(FPDF):
    def header(self):
        self.set_font('Helvetica', 'B', 10)
        self.cell(0, 10, 'TechCorp Solutions - Politica de Confidencialidad', 0, 0, 'C')
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
pdf.cell(0, 15, 'Politica de Manejo de Informacion', 0, 1, 'C')
pdf.cell(0, 15, 'Confidencial y Secreto de Negocio', 0, 1, 'C')
pdf.set_font('Helvetica', '', 14)
pdf.cell(0, 10, 'TechCorp Solutions S.A. de C.V.', 0, 1, 'C')
pdf.set_font('Helvetica', '', 11)
pdf.cell(0, 8, 'Version 2.1 - Agosto 2026', 0, 1, 'C')
pdf.ln(15)

# Seccion 1
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '1. OBJETO Y ALCANCE', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'La presente Politica de Confidencialidad tiene por objeto establecer las reglas '
    'para la clasificacion, manejo, almacenamiento y divulgacion de la informacion '
    'confidencial y los secretos de negocio de TechCorp Solutions S.A. de C.V. (en '
    'adelante "TechCorp").'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Esta politica aplica a todos los empleados, directivos, contratistas, proveedores '
    'y cualquier tercero que tenga acceso a informacion de TechCorp, ya sea dentro o '
    'fuera de las instalaciones, incluyendo el trabajo remoto.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Fecha de ultima actualizacion: 1 de agosto de 2026. La persona responsable de '
    'vigilar el cumplimiento de esta politica es el Oficial de Seguridad de la '
    'Informacion.'
))
pdf.ln(5)

# Seccion 2
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '2. DEFINICIONES', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Informacion Confidencial: Toda informacion que no sea de dominio publico y cuya '
    'divulgacion pudiera causar un dano a TechCorp, incluyendo datos de clientes, '
    'listas de proveedores, estructuras de costos, bases de datos, algoritmos y '
    'documentos internos.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Secreto de Negocio: Toda informacion que cumpla los siguientes requisitos: (a) '
    'confiere una ventaja competitiva a TechCorp, (b) no es generalmente conocida ni '
    'facilmente accesible, y (c) ha sido objeto de medidas razonables para mantener '
    'su confidencialidad. Incluye formulas, metodologias, planes de negocio, listas '
    'de clientes y codigo fuente propietario.'
))
pdf.ln(5)

# Seccion 3
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '3. CLASIFICACION DE LA INFORMACION', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Toda la informacion de TechCorp debe clasificarse en uno de los siguientes '
    'niveles al momento de su creacion:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.1 Publica', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Informacion destinada a difusion abierta: folletos, sitio web publico, '
    'comunicados de prensa y material de marketing autorizado.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.2 Interna', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Informacion de uso interno de TechCorp cuya divulgacion no causaria un dano '
    'grave pero que no debe salir de la organizacion: organigramas, calendarios, '
    'manuales administrativos y politicas generales.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.3 Confidencial', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Informacion restringida cuyo acceso se limita a personal autorizado con base '
    'en sus funciones: datos personales de clientes, contratos, precios, estados '
    'financieros y evaluaciones de desempeno.'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.4 Secreta', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Informacion de maxima proteccion: secretos de negocio, codigo fuente '
    'propietario, algoritmos, formula de precios y planes estrategicos. El acceso a '
    'informacion clasificada como Secreta se otorga exclusivamente bajo el '
    'principio de minimo privilegio (need to know).'
))
pdf.ln(5)

# Seccion 4
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '4. MANEJO Y ALMACENAMIENTO', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El personal debe cumplir las siguientes reglas de manejo:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    '- Los documentos clasificados como Confidencial o Secreto deben marcarse con '
    'la leyenda "CONFIDENCIAL" o "SECRETO" en la primera pagina\n'
    '- La informacion Confidencial debe almacenarse cifrada con AES-256 en los '
    'repositorios autorizados de TechCorp\n'
    '- La informacion clasificada como Secreta no puede imprimirse, copiarse ni '
    'extraerse de las instalaciones sin autorizacion expresa del Oficial de '
    'Seguridad de la Informacion\n'
    '- Los documentos fisicos Confidenciales deben destruirse con trituradora '
    'industrial al termino de su vigencia\n'
    '- Queda prohibido enviar informacion Confidencial a cuentas de correo '
    'personales o almacenarla en servicios en la nube no autorizados'
))
pdf.ln(5)

# Seccion 5
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '5. ACUERDOS DE CONFIDENCIALIDAD (NDA)', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Todo empleado, contratista o proveedor debe firmar un Acuerdo de '
    'Confidencialidad (NDA) antes de recibir acceso a cualquier informacion '
    'Confidencial o Secreta.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Reglas aplicables a los NDA:\n\n'
    '- El NDA debe firmarse de forma previa e indelegable por la persona que '
    'recibira el acceso\n'
    '- La vigencia de la obligacion de confidencialidad es de 5 anos contados a '
    'partir de la terminacion de la relacion laboral o contractual\n'
    '- La informacion marcada como Secreto de Negocio debe protegerse por tiempo '
    'indefinido mientras conserve su caracter de secreto\n'
    '- Los NDA de terceros deben revisarse por el area legal antes de su firma\n'
    '- Una copia del NDA firmado se resguarda en el expediente personal del '
    'colaborador'
))
pdf.ln(5)

# Seccion 6
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '6. ACCESO Y AUTORIZACION', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El acceso a la informacion se otorga conforme al principio de minimo '
    'privilegio: cada colaborador tendra acceso unicamente a la informacion '
    'necesaria para desempenar sus funciones.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    '- Las solicitudes de acceso a informacion Secreta se evaluan por el Oficial '
    'de Seguridad de la Informacion en un plazo de 5 dias habiles\n'
    '- El acceso se revoca de inmediato al terminar la relacion laboral o '
    'contractual\n'
    '- Cada trimestre se realiza una revision de accesos para verificar que '
    'sigan siendo necesarios\n'
    '- Queda prohibido compartir credenciales de acceso entre colaboradores'
))
pdf.ln(5)

# Seccion 7
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '7. TRANSFERENCIA A TERCEROS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'La informacion Confidencial o Secreta solo puede transferirse a terceros '
    'cuando:\n\n'
    '- Exista un NDA vigente firmado por el tercero\n'
    '- La transferencia sea necesaria para la prestacion del servicio contratado\n'
    '- El area legal y el Oficial de Seguridad hayan autorizado la transferencia\n'
    '- El tercero cuente con medidas de seguridad equivalentes a las de TechCorp'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    'Cualquier transferencia no autorizada de informacion Secreta sera tratada '
    'como falta grave conforme a la seccion 8 de esta politica.'
))
pdf.ln(5)

# Seccion 8
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '8. SANCIONES POR INCUMPLIMIENTO', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El incumplimiento de esta politica se sanciona conforme a la gravedad de la '
    'falta:'
))
pdf.ln(3)
pdf.multi_cell(0, 7, (
    '- Falta leve (divulgacion accidental de informacion Interna): amonestacion '
    'escrita y capacitacion obligatoria\n'
    '- Falta grave (divulgacion de informacion Confidencial sin autorizacion): '
    'suspension de 3 a 15 dias sin goce de sueldo\n'
    '- Falta muy grave (divulgacion de secretos de negocio o informacion Secreta, '
    'o reincidencia): separacion del puesto\n\n'
    'Ademas de las sanciones laborales, el incumplimiento podra dar lugar a '
    'acciones legales por danos y perjuicios y, en su caso, a la denuncia penal '
    'correspondiente.'
))
pdf.ln(5)

# Seccion 9
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '9. DURACION DE LA OBLIGACION', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Las obligaciones de confidencialidad contenidas en esta politica y en los '
    'NDA subsisten despues de la terminacion de la relacion con TechCorp.'
))
pdf.ln(2)
pdf.multi_cell(0, 7, (
    '- Informacion Confidencial: obligacion de resguardo por 5 anos\n'
    '- Secretos de negocio: obligacion de resguardo por tiempo indefinido\n'
    '- Al terminar la relacion, el colaborador debe devolver o destruir toda la '
    'informacion en su poder y confirmarlo por escrito\n\n'
    'La infraccion de las obligaciones de confidencialidad despues de la '
    'terminacion de la relacion puede perseguirse por la via civil y penal.'
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
    'Oficial de Seguridad de la Informacion:\n'
    'Nombre: Ing. Jorge Luis Hernandez Camacho\n'
    'Correo: seguridad@techcorp.com\n'
    'Telefono: +52 55 1234 5678 ext. 1600\n\n'
    'Los reportes de divulgacion accidental de informacion deben enviarse a '
    'seguridad@techcorp.com dentro de las 24 horas siguientes a que el colaborador '
    'tenga conocimiento de la divulgacion.'
))

pdf.output('C:\\Users\\Raul\\Desktop\\AsistenteIA\\politica_confidencialidad.pdf')
print("PDF Politica de Confidencialidad creado exitosamente")
