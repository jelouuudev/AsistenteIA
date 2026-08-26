from fpdf import FPDF

FONT_PATH = "C:/Windows/Fonts/arial.ttf"
FONT_BOLD = "C:/Windows/Fonts/arialbd.ttf"

class ManualPDF(FPDF):
    def __init__(self):
        super().__init__()
        self.add_font("Arial", "", FONT_PATH, uni=True)
        self.add_font("Arial", "B", FONT_BOLD, uni=True)

    def header(self):
        if self.page_no() > 1:
            self.set_font("Arial", "B", 9)
            self.set_text_color(120, 120, 120)
            self.cell(0, 8, "CloudSync Pro - Glosario de Terminos", align="L")
            self.ln(10)

    def footer(self):
        self.set_y(-15)
        self.set_font("Arial", "I", 8)
        self.set_text_color(150, 150, 150)
        self.cell(0, 10, f"Pagina {self.page_no()}/{{nb}}", align="C")

    def section(self, num, title):
        self.set_font("Arial", "B", 14)
        self.set_text_color(0, 51, 102)
        self.cell(0, 10, f"{num}. {title}", new_x="LMARGIN", new_y="NEXT")
        self.set_draw_color(0, 51, 102)
        self.line(self.l_margin, self.get_y(), self.w - self.r_margin, self.get_y())
        self.ln(4)

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
pdf.ln(50)
pdf.set_font("Arial", "B", 28)
pdf.set_text_color(0, 51, 102)
pdf.cell(0, 15, "CloudSync Pro", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.set_font("Arial", "", 16)
pdf.set_text_color(80, 80, 80)
pdf.cell(0, 10, "Glosario de Terminos", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.ln(5)
pdf.set_font("Arial", "", 12)
pdf.set_text_color(100, 100, 100)
pdf.cell(0, 8, "Seccion 9 - Manual del Producto v4.2", align="C", new_x="LMARGIN", new_y="NEXT")

# === GLOSARIO ===
pdf.add_page()
pdf.section(9, "Glosario de Terminos")
pdf.txt("Definiciones de los terminos tecnicos utilizados en este manual:")
pdf.bul("Blue-Green Deployment: Estrategia de despliegue que mantiene dos entornos identicos (azul y verde) y alterna el trafico entre ellos.")
pdf.bul("Canary Release: Tecnica de despliegue gradual que expone un porcentaje pequeno de usuarios a la nueva version antes del lanzamiento completo.")
pdf.bul("Pipeline: Serie de pasos automatizados que se ejecutan cuando se detecta un cambio en el codigo fuente.")
pdf.bul("Runner: Servidor o contenedor que ejecuta las tareas definidas en un pipeline CI/CD.")
pdf.bul("Rollback: Proceso de revertir un despliegue a una version anterior estable.")
pdf.bul("SLA (Service Level Agreement): Acuerdo de nivel de servicio que define la disponibilidad y tiempo de respuesta garantizado.")
pdf.bul("Webhook: Mecanismo de notificacion automatica que envia datos a una URL especifica cuando ocurre un evento.")
pdf.bul("Container: Unidad empaquetada de software que incluye el codigo fuente, las dependencias y el runtime necesarios para ejecutarse de forma consistente en cualquier entorno.")
pdf.bul("Kubernetes: Plataforma de orquestacion de contenedores que automatiza el despliegue, escalado y gestion de aplicaciones containerizadas.")

output = "C:/Users/Raul/Desktop/AsistenteIA/Manual_Producto_CloudSync_Pro_Glosario.pdf"
pdf.output(output)
print(f"PDF creado: {output}")
print(f"Paginas: {pdf.page_no()}")
