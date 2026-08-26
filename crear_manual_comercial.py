from fpdf import FPDF

class PDF(FPDF):
    def header(self):
        self.set_font('Helvetica', 'B', 10)
        self.cell(0, 10, 'TechCorp Solutions - Manual Comercial 2026', 0, 0, 'C')
        self.ln(15)

    def footer(self):
        self.set_y(-15)
        self.set_font('Helvetica', 'I', 8)
        self.cell(0, 10, f'Pagina {self.page_no()}/{{nb}}', 0, 0, 'C')

pdf = PDF()
pdf.alias_nb_pages()
pdf.set_auto_page_break(auto=True, margin=15)
pdf.add_page()

# Portada
pdf.set_font('Helvetica', 'B', 24)
pdf.ln(30)
pdf.cell(0, 15, 'Manual Comercial', 0, 1, 'C')
pdf.set_font('Helvetica', '', 16)
pdf.cell(0, 10, 'TechCorp Solutions S.A. de C.V.', 0, 1, 'C')
pdf.set_font('Helvetica', '', 12)
pdf.cell(0, 8, 'Version 3.0 - Agosto 2026', 0, 1, 'C')
pdf.ln(20)

# Seccion 1
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '1. INTRODUCCION', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Este manual comercial establece las politicas, procedimientos y lineamientos que rigen '
    'las actividades comerciales de TechCorp Solutions. Todos los miembros del equipo comercial '
    'deben conocer y cumplir con estas directrices para garantizar la excelencia en el servicio '
    'al cliente y el cumplimiento de los objetivos de ventas.'
))
pdf.ln(5)

# Seccion 2
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '2. PORTAFOLIO DE PRODUCTOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'TechCorp Solutions ofrece tres lineas principales de productos y servicios:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 12)
pdf.cell(0, 8, '2.1 CloudSync Pro', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Plataforma de sincronizacion en la nube para empresas. Caracteristicas principales:\n'
    '- Sincronizacion en tiempo real de hasta 10TB de datos\n'
    '- Cifrado AES-256 de extremo a extremo\n'
    '- Integracion con Microsoft 365, Google Workspace y Slack\n'
    '- Soporte tecnico 24/7 en espanol e ingles\n'
    '- Precio: $299 USD/mes por licencia empresarial\n'
    '- Descuento por volumen: 15% para 50+ licencias, 25% para 100+ licencias'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 12)
pdf.cell(0, 8, '2.2 DataSecure Enterprise', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Solucion de seguridad informatica para proteccion de datos corporativos:\n'
    '- Deteccion de amenazas en tiempo real con IA\n'
    '- Firewall de nueva generacion (NGFW)\n'
    '- Gestion centralizada de endpoints\n'
    '- Cumplimiento automatico de GDPR, HIPAA y SOX\n'
    '- Precio: $499 USD/mes por hasta 100 endpoints\n'
    '- Precio adicional: $5 USD/endpoint extra'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 12)
pdf.cell(0, 8, '2.3 SmartAnalytics Suite', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Plataforma de analisis de datos y business intelligence:\n'
    '- Dashboards personalizables con drag-and-drop\n'
    '- Reportes automatizados en PDF, Excel y PowerPoint\n'
    '- Integracion con mas de 200 fuentes de datos\n'
    '- Predictive analytics con machine learning\n'
    '- Precio: $799 USD/mes por departamento\n'
    '- Incluye 10 usuarios, $50 USD/usuario adicional'
))
pdf.ln(5)

# Seccion 3
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '3. POLITICA DE PRECIOS Y DESCUENTOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'La politica de precios de TechCorp Solutions se basa en el valor entregado al cliente. '
    'Los descuentos autorizados son:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.1 Descuentos por Volumen', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- 5-19 licencias: 5% de descuento\n'
    '- 20-49 licencias: 10% de descuento\n'
    '- 50-99 licencias: 15% de descuento\n'
    '- 100-249 licencias: 20% de descuento\n'
    '- 250+ licencias: 25% de descuento (requiere aprobacion de Direccion Comercial)'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.2 Descuentos por Contrato Anual', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Contrato anual: 10% de descuento adicional\n'
    '- Contrato bianual: 15% de descuento adicional\n'
    '- Contrato trienal: 20% de descuento adicional\n'
    '- Los descuentos por volumen y contrato anual son acumulativos'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '3.3 Descuentos Especiales', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Clientes gubernamentales: 30% de descuento (requiere documentacion oficial)\n'
    '- Organizaciones sin fines de lucro: 25% de descuento\n'
    '- Instituciones educativas: 35% de descuento\n'
    '- Partners tecnologicos certificados: 20% de descuento\n'
    '- Todos los descuentos especiales requieren aprobacion del Gerente Comercial'
))
pdf.ln(5)

# Seccion 4
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '4. PROCESO DE VENTAS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El proceso de ventas de TechCorp Solutions consta de las siguientes etapas:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '4.1 Prospeccion y Calificacion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Identificar clientes potenciales mediante investigacion de mercado\n'
    '- Calificar leads usando el metodo BANT (Budget, Authority, Need, Timeline)\n'
    '- Registrar todos los leads en el CRM Salesforce\n'
    '- Tiempo maximo de respuesta a un lead: 24 horas habiles'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '4.2 Presentacion y Demostracion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Realizar presentacion personalizada segun necesidades del cliente\n'
    '- Ofrecer demostracion gratuita de 14 dias para productos enterprise\n'
    '- Utilizar material de ventas oficial de TechCorp\n'
    '- Documentar todos los requisitos tecnicos y comerciales del cliente'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '4.3 Negociacion y Cierre', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Elaborar propuesta comercial usando la plantilla oficial\n'
    '- Negociar terminos dentro de los parametros autorizados\n'
    '- Obtener aprobaciones internas necesarias (ver seccion 5)\n'
    '- Firmar contrato y orden de compra\n'
    '- Meta de cierre: 30% de las propuestas presentadas'
))
pdf.ln(5)

# Seccion 5
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '5. APROBACIONES Y AUTORIZACIONES', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Las siguientes situaciones requieren aprobacion previa:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '5.1 Aprobacion del Gerente Comercial', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Descuentos superiores al 15%\n'
    '- Contratos con plazo de pago mayor a 60 dias\n'
    '- Propuestas con valor inferior a $10,000 USD\n'
    '- Modificaciones a los terminos estandar del contrato'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '5.2 Aprobacion del Director Comercial', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Descuentos superiores al 25%\n'
    '- Contratos con valor superior a $500,000 USD\n'
    '- Clientes nuevos sin referencias comerciales\n'
    '- Acuerdos de exclusividad o partnership'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '5.3 Aprobacion de Direccion General', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Contratos con valor superior a $1,000,000 USD\n'
    '- Acuerdos estrategicos con empresas Fortune 500\n'
    '- Inversiones en desarrollo personalizado superiores a $100,000 USD\n'
    '- Cualquier acuerdo que implique riesgo reputacional'
))
pdf.ln(5)

# Seccion 6
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '6. COMISIONES Y BONOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'El esquema de compensacion del equipo comercial incluye:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '6.1 Comisiones por Venta', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- CloudSync Pro: 8% del valor anual del contrato\n'
    '- DataSecure Enterprise: 10% del valor anual del contrato\n'
    '- SmartAnalytics Suite: 12% del valor anual del contrato\n'
    '- Servicios profesionales: 5% del valor del proyecto\n'
    '- Las comisiones se pagan mensualmente dentro de los primeros 10 dias del mes siguiente'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '6.2 Bonos por Cumplimiento de Metas', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- 100% de la meta trimestral: Bono de $5,000 USD\n'
    '- 120% de la meta trimestral: Bono de $12,000 USD\n'
    '- 150% de la meta trimestral: Bono de $20,000 USD\n'
    '- Presidente\'s Club (top 10% anual): Viaje todo pagado + $50,000 USD en efectivo\n'
    '- Las metas se establecen trimestralmente por la Direccion Comercial'
))
pdf.ln(5)

# Seccion 7
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '7. SOPORTE POST-VENTA', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'TechCorp Solutions ofrece los siguientes niveles de soporte post-venta:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '7.1 Soporte Basico (Incluido)', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Soporte por correo electronico: respuesta en 24 horas habiles\n'
    '- Acceso a base de conocimiento y documentacion tecnica\n'
    '- Actualizaciones de software menores\n'
    '- Horario: Lunes a Viernes, 9:00 a 18:00'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '7.2 Soporte Premium ($99 USD/mes)', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Soporte telefonico: respuesta en 4 horas\n'
    '- Soporte por correo electronico: respuesta en 8 horas\n'
    '- Acceso a ingeniero de cuenta dedicado\n'
    '- Actualizaciones de software mayores y menores\n'
    '- Horario: Lunes a Viernes, 8:00 a 20:00'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '7.3 Soporte Enterprise ($299 USD/mes)', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Soporte telefonico 24/7: respuesta en 1 hora\n'
    '- Soporte por correo electronico: respuesta en 2 horas\n'
    '- Equipo de soporte dedicado con SLA garantizado\n'
    '- Todas las actualizaciones de software\n'
    '- Capacitacion trimestral para el equipo del cliente\n'
    '- Revisiones de salud del sistema cada 6 meses'
))
pdf.ln(5)

# Seccion 8
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '8. POLITICA DE CANCELACION Y REEMBOLSOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Las politicas de cancelacion y reembolsos son:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '8.1 Periodo de Prueba', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Todos los productos incluyen 14 dias de prueba gratuita\n'
    '- No se requiere tarjeta de credito para iniciar la prueba\n'
    '- El cliente puede cancelar en cualquier momento durante el periodo de prueba sin cargo'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '8.2 Cancelacion Anticipada', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Contratos mensuales: Cancelacion con 30 dias de anticipacion\n'
    '- Contratos anuales: Penalizacion del 20% del saldo restante\n'
    '- Contratos multi-anuales: Penalizacion del 30% del saldo restante\n'
    '- Los reembolsos se procesan en 30 dias habiles'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '8.3 Garantia de Satisfaccion', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Si el cliente no esta satisfecho en los primeros 90 dias, se ofrece reembolso completo\n'
    '- Aplica solo para clientes nuevos en su primer contrato\n'
    '- Requiere reunion de feedback con el equipo de producto\n'
    '- Maximo un reembolso por cliente/empresa'
))
pdf.ln(5)

# Seccion 9
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '9. CODIGO DE ETICA COMERCIAL', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Todos los miembros del equipo comercial deben cumplir con:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '9.1 Principios Fundamentales', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Honestidad: No hacer promesas que no podamos cumplir\n'
    '- Transparencia: Comunicar claramente precios, limitaciones y riesgos\n'
    '- Integridad: No ofrecer sobornos o incentivos indebidos\n'
    '- Confidencialidad: Proteger la informacion de clientes y prospectos\n'
    '- Respeto: Tratar a clientes, colegas y competidores con dignidad'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '9.2 Practicas Prohibidas', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Hacer declaraciones falsas o enganosas sobre productos o servicios\n'
    '- Ofrecer descuentos no autorizados para cerrar ventas\n'
    '- Criticar o desacreditar a la competencia de manera desleal\n'
    '- Compartir informacion confidencial de clientes con terceros\n'
    '- Aceptar regalos o incentivos de clientes o proveedores\n'
    '- Violar leyes antimonopolio o de competencia desleal'
))
pdf.ln(5)

# Seccion 10
pdf.set_font('Helvetica', 'B', 14)
pdf.cell(0, 10, '10. CONTACTO Y RECURSOS', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    'Recursos y contactos utiles para el equipo comercial:'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '10.1 Contactos Internos', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- Direccion Comercial: comercial@techcorp.com, ext. 2000\n'
    '- Soporte Pre-Venta: preventa@techcorp.com, ext. 2100\n'
    '- Legal y Contratos: legal@techcorp.com, ext. 2200\n'
    '- Finanzas y Facturacion: finanzas@techcorp.com, ext. 2300\n'
    '- Soporte Post-Venta: soporte@techcorp.com, ext. 2400'
))
pdf.ln(3)

pdf.set_font('Helvetica', 'B', 11)
pdf.cell(0, 7, '10.2 Herramientas y Sistemas', 0, 1)
pdf.set_font('Helvetica', '', 11)
pdf.multi_cell(0, 7, (
    '- CRM: Salesforce (https://techcorp.my.salesforce.com)\n'
    '- Documentacion: Confluence (https://confluence.techcorp.com)\n'
    '- Presentaciones: SharePoint (https://sharepoint.techcorp.com/ventas)\n'
    '- Capacitacion: LMS (https://lms.techcorp.com)\n'
    '- Calculadoras de ROI: Portal de Ventas (https://ventas.techcorp.com)'
))

pdf.output('C:\\Users\\Raul\\Desktop\\AsistenteIA\\manual_comercial.pdf')
print("PDF Manual Comercial creado exitosamente")
