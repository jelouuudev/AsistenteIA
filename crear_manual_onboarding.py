from fpdf import FPDF

FONT_PATH = "C:/Windows/Fonts/arial.ttf"
FONT_BOLD = "C:/Windows/Fonts/arialbd.ttf"
OUTPUT = "C:/Users/Raul/Desktop/AsistenteIA/Manual_Onboarding_TechCorp.pdf"


class ManualPDF(FPDF):
    def __init__(self):
        super().__init__()
        self.add_font("Arial", "", FONT_PATH, uni=True)
        self.add_font("Arial", "B", FONT_BOLD, uni=True)

    def header(self):
        if self.page_no() > 1:
            self.set_font("Arial", "B", 9)
            self.set_text_color(120, 120, 120)
            self.cell(0, 8, "TechCorp - Manual de Onboarding v1.0", align="L")
            self.ln(10)

    def footer(self):
        self.set_y(-15)
        self.set_font("Arial", "", 8)
        self.set_text_color(150, 150, 150)
        self.cell(0, 10, f"Pagina {self.page_no()}/{{nb}}", align="C")

    def section(self, num, title):
        self.set_font("Arial", "B", 14)
        self.set_text_color(0, 70, 90)
        self.cell(0, 10, f"{num}. {title}", new_x="LMARGIN", new_y="NEXT")
        self.set_draw_color(0, 70, 90)
        self.line(self.l_margin, self.get_y(), self.w - self.r_margin, self.get_y())
        self.ln(4)

    def sub(self, num, title):
        self.set_font("Arial", "B", 11)
        self.set_text_color(51, 51, 51)
        self.cell(0, 8, f"{num} {title}", new_x="LMARGIN", new_y="NEXT")
        self.ln(2)

    def txt(self, t):
        self.set_font("Arial", "", 10)
        self.set_text_color(33, 33, 33)
        self.multi_cell(0, 5.5, t)
        self.ln(3)

    def bul(self, t):
        self.set_font("Arial", "", 10)
        self.set_text_color(33, 33, 33)
        self.cell(6, 5.5, "-")
        self.multi_cell(0, 5.5, t)
        self.ln(1)


pdf = ManualPDF()
pdf.alias_nb_pages()
pdf.set_auto_page_break(auto=True, margin=20)

# === PORTADA ===
pdf.add_page()
pdf.ln(45)
pdf.set_font("Arial", "B", 26)
pdf.set_text_color(0, 70, 90)
pdf.cell(0, 14, "Manual de Onboarding", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.set_font("Arial", "", 14)
pdf.set_text_color(80, 80, 80)
pdf.cell(0, 10, "Guia de Incorporacion de Personal Nuevo", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.ln(5)
pdf.set_font("Arial", "", 12)
pdf.set_text_color(100, 100, 100)
pdf.cell(0, 8, "Version 1.0 - Julio 2026", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.ln(18)
pdf.set_font("Arial", "", 10)
pdf.set_text_color(60, 60, 60)
pdf.multi_cell(0, 6, (
    "Este documento describe el proceso de incorporacion de nuevos colaboradores "
    "en TechCorp Solutions S.A. de C.V. Incluye checklist de primer dia, accesos "
    "tecnologicos, capacitacion obligatoria y criterios de evaluacion del periodo "
    "de prueba."
), align="C")
pdf.ln(12)
pdf.set_font("Arial", "", 9)
pdf.cell(0, 6, "TechCorp Solutions S.A. de C.V.", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.cell(0, 6, "Departamento de Recursos Humanos", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.cell(0, 6, "Documento interno - Uso exclusivo del personal autorizado", align="C", new_x="LMARGIN", new_y="NEXT")

# === 1. INTRODUCCION ===
pdf.add_page()
pdf.section(1, "Introduccion")
pdf.txt(
    "El onboarding es el proceso formal mediante el cual un colaborador nuevo "
    "se integra a la cultura, procesos y herramientas de TechCorp. Un onboarding "
    "exitoso reduce el tiempo hasta productividad plena y mejora la retencion "
    "durante los primeros 90 dias."
)
pdf.txt(
    "Este manual es obligatorio para managers, buddy asignado y el area de RRHH. "
    "Las excepciones deben aprobarse por escrito por el Director de Personas."
)

# === 2. OBJETIVOS ===
pdf.section(2, "Objetivos del Onboarding")
pdf.bul("Garantizar que el colaborador tenga accesos y equipo el primer dia laboral.")
pdf.bul("Transmitir valores, politicas de seguridad y codigo de conducta.")
pdf.bul("Asignar un buddy (companero guia) durante las primeras 4 semanas.")
pdf.bul("Completar capacitaciones obligatorias antes del dia 15.")
pdf.bul("Evaluar el desempeno al dia 30, 60 y 90 con criterios medibles.")

# === 3. ROLES Y RESPONSABILIDADES ===
pdf.section(3, "Roles y Responsabilidades")
pdf.sub("3.1", "Recursos Humanos")
pdf.txt(
    "RRHH coordina la pre-incorporacion: contrato, alta en nomina, cuenta de correo "
    "corporativo y agenda de bienvenida. Tambien registra el checklist en Workday "
    "y confirma que el colaborador firmo las politicas de seguridad y confidencialidad."
)
pdf.sub("3.2", "Manager Directo")
pdf.txt(
    "El manager define objetivos de los primeros 90 dias, asigna el buddy, "
    "revisa el plan de capacitacion tecnica y realiza las evaluaciones formales "
    "en los hitos de 30, 60 y 90 dias."
)
pdf.sub("3.3", "Buddy Asignado")
pdf.txt(
    "El buddy es un companero del mismo equipo con al menos 6 meses de antiguedad. "
    "Acompana al nuevo ingreso en dudas operativas diarias, presenta al equipo "
    "y valida que los accesos a Jira, GitHub, Slack y CloudSync Pro funcionen."
)
pdf.sub("3.4", "Colaborador Nuevo")
pdf.txt(
    "Debe completar las capacitaciones obligatorias, leer las politicas internas, "
    "reportar incidencias de acceso el mismo dia y asistir a las sesiones de "
    "induccion cultural los martes y jueves de la primera semana."
)

# === 4. PROCESO DE INCORPORACION ===
pdf.section(4, "Proceso de Incorporacion")
pdf.sub("4.1", "Pre-Ingreso (7 dias antes)")
pdf.bul("Enviar carta oferta firmada y documentos de identidad a RRHH.")
pdf.bul("Crear cuenta de correo @techcorp.com y usuario de Active Directory.")
pdf.bul("Solicitar laptop, monitor y kit de bienvenida al area de TI.")
pdf.bul("Agendar reunion de bienvenida con manager y buddy.")
pdf.bul("Preparar accesos a Slack (#general, #equipo), Jira y Confluence.")

pdf.sub("4.2", "Primer Dia")
pdf.txt(
    "El primer dia inicia a las 09:00 en la oficina de Polanco o por videollamada "
    "si el colaborador es remoto. La agenda incluye: entrega de equipo, tour "
    "virtual de herramientas, firma de politicas y presentacion al equipo."
)
pdf.bul("09:00 - Bienvenida con RRHH y entrega de gafete / credenciales.")
pdf.bul("10:00 - Configuracion de laptop y VPN con Soporte TI.")
pdf.bul("11:30 - Reunion con manager: objetivos y expectativas.")
pdf.bul("13:00 - Almuerzo de equipo (presencial) o cafe virtual (remoto).")
pdf.bul("15:00 - Sesion con buddy: repositorios, board de Jira y rituales del equipo.")
pdf.bul("17:00 - Cierre del dia: checklist de accesos firmado en Workday.")

pdf.sub("4.3", "Primera Semana")
pdf.txt(
    "Durante la primera semana el colaborador no debe recibir tickets criticos "
    "de produccion. El foco es capacitacion, lectura de documentacion tecnica "
    "y un primer aporte pequeno (documentacion o tarea de baja complejidad)."
)
pdf.bul("Completar curso de Seguridad de la Informacion (2 horas, LMS interno).")
pdf.bul("Completar curso de Introduccion a CloudSync Pro (3 horas).")
pdf.bul("Leer la Guia de Desarrollo de Software v3.1.")
pdf.bul("Asistir a Daily Standup y una Sprint Review como oyente.")
pdf.bul("Entregar un resumen de aprendizajes al buddy el viernes.")

pdf.sub("4.4", "Primeros 30 Dias")
pdf.txt(
    "Al dia 30 el manager realiza la primera evaluacion formal. Se espera que el "
    "colaborador haya cerrado al menos 3 historias de usuario o tareas equivalentes, "
    "con revision de codigo aprobada por un senior."
)

# === 5. ACCESOS Y HERRAMIENTAS ===
pdf.section(5, "Accesos y Herramientas")
pdf.sub("5.1", "Herramientas Obligatorias")
pdf.bul("Slack: comunicacion diaria. Canal obligatorio #anuncios-rrhh.")
pdf.bul("Microsoft Teams: reuniones formales y 1:1 con el manager.")
pdf.bul("Jira: gestion de trabajo. Proyecto del equipo asignado.")
pdf.bul("GitHub / CloudSync Pro: codigo fuente y pipelines.")
pdf.bul("Confluence: documentacion de arquitectura y runbooks.")
pdf.bul("Workday: vacaciones, recibos y evaluaciones de desempeno.")

pdf.sub("5.2", "Niveles de Acceso")
pdf.txt(
    "Por defecto el colaborador nuevo recibe rol Viewer en produccion y "
    "rol Developer en ambientes de desarrollo y staging. El acceso a "
    "produccion con permisos de despliegue requiere aprobacion del tech lead "
    "despues del dia 60 y completar el curso de Despliegues Seguros."
)

pdf.sub("5.3", "Soporte de Accesos")
pdf.txt(
    "Para problemas de acceso el primer punto de contacto es el buddy. "
    "Si no se resuelve en 2 horas, abrir ticket en #soporte-ti con prioridad Alta. "
    "El SLA de TI para altas de acceso en onboarding es de 4 horas habiles."
)

# === 6. CAPACITACION OBLIGATORIA ===
pdf.section(6, "Capacitacion Obligatoria")
pdf.bul("Seguridad de la Informacion - plazo: dia 7.")
pdf.bul("Codigo de Etica y Conducta - plazo: dia 7.")
pdf.bul("Introduccion a CloudSync Pro - plazo: dia 15.")
pdf.bul("Proteccion de Datos Personales (LFPDPPP) - plazo: dia 15.")
pdf.bul("Despliegues Seguros (solo roles tecnicos) - plazo: dia 60.")
pdf.txt(
    "Todas las capacitaciones se registran en el LMS. El incumplimiento "
    "bloquea la evaluacion de 30 dias y puede extender el periodo de prueba."
)

# === 7. EVALUACION DEL PERIODO DE PRUEBA ===
pdf.section(7, "Evaluacion del Periodo de Prueba")
pdf.sub("7.1", "Criterios de Evaluacion")
pdf.bul("Calidad del trabajo entregado (revisiones de codigo / entregables).")
pdf.bul("Cumplimiento de capacitaciones y politicas.")
pdf.bul("Colaboracion con el equipo y comunicacion asertiva.")
pdf.bul("Autonomia creciente entre el dia 30 y el dia 90.")
pdf.bul("Alineacion con valores de TechCorp: excelencia, transparencia y ownership.")

pdf.sub("7.2", "Resultados Posibles")
pdf.txt(
    "Al dia 90 el manager y RRHH emiten uno de tres resultados: Confirmacion "
    "definitiva, Extension de prueba por 30 dias adicionales (solo una vez), "
    "o No confirmacion con plan de salida acompanada."
)

# === 8. CHECKLIST RAPIDO ===
pdf.section(8, "Checklist Rapido del Manager")
pdf.bul("Asignar buddy 5 dias antes del ingreso.")
pdf.bul("Definir 3 objetivos medibles para los primeros 30 dias.")
pdf.bul("Verificar que TI entrego equipo y accesos el dia 0.")
pdf.bul("Agendar 1:1 semanal durante el primer mes.")
pdf.bul("Completar evaluaciones en Workday en dias 30, 60 y 90.")
pdf.bul("Celebrar el cierre de onboarding con el equipo.")

# === 9. PREGUNTAS FRECUENTES ===
pdf.section(9, "Preguntas Frecuentes")
pdf.txt(
    "P: Que pasa si el colaborador es 100% remoto?\n"
    "R: El proceso es el mismo. El kit se envia 3 dias antes y la bienvenida "
    "se hace por Teams. El buddy debe agendar al menos 3 sesiones sincronicas "
    "en la primera semana."
)
pdf.txt(
    "P: Puedo cambiar de buddy?\n"
    "R: Si, con aprobacion del manager. El cambio debe registrarse en Workday "
    "para no perder continuidad del checklist."
)
pdf.txt(
    "P: Cual es el horario de las inducciones culturales?\n"
    "R: Martes y jueves de 10:00 a 11:00 (hora Ciudad de Mexico) durante "
    "la primera semana del ingreso."
)

# === 10. CONTACTO ===
pdf.section(10, "Contacto")
pdf.bul("Correo RRHH onboarding: onboarding@techcorp.com")
pdf.bul("Slack: #onboarding-ayuda")
pdf.bul("Telefono interno: ext. 3100")
pdf.bul("Horario de atencion: Lunes a Viernes de 9:00 a 18:00")
pdf.bul("Responsable del proceso: Lic. Mariana Lopez - mariana.lopez@techcorp.com")

pdf.output(OUTPUT)
print(f"PDF creado: {OUTPUT}")
