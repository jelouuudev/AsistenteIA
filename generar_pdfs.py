from fpdf import FPDF

class PDF(FPDF):
    def header(self):
        self.set_font("Helvetica", "B", 14)
        self.set_text_color(0, 82, 136)
        self.cell(0, 10, self.title_text, new_x="LMARGIN", new_y="NEXT", align="C")
        self.set_font("Helvetica", "", 10)
        self.set_text_color(100, 100, 100)
        self.cell(0, 8, self.subtitle_text, new_x="LMARGIN", new_y="NEXT", align="C")
        self.ln(4)
        self.set_draw_color(0, 82, 136)
        self.set_line_width(0.5)
        self.line(10, self.get_y(), 200, self.get_y())
        self.ln(6)

    def footer(self):
        self.set_y(-15)
        self.set_font("Helvetica", "I", 8)
        self.set_text_color(150, 150, 150)
        self.cell(0, 10, f"Pagina {self.page_no()}/{{nb}}", align="C")

    def section_title(self, num, title):
        self.set_font("Helvetica", "B", 12)
        self.set_text_color(0, 60, 100)
        self.set_fill_color(230, 240, 250)
        self.cell(0, 9, f"{num}. {title}", new_x="LMARGIN", new_y="NEXT", fill=True)
        self.ln(3)

    def item(self, text, bold_prefix="", indent=10):
        self.set_font("Helvetica", "", 10)
        self.set_text_color(50, 50, 50)
        self.set_x(10 + indent)
        if bold_prefix:
            self.set_font("Helvetica", "B", 10)
            self.cell(self.get_string_width(bold_prefix) + 1, 6, bold_prefix)
            self.set_font("Helvetica", "", 10)
            self.multi_cell(0, 6, text)
        else:
            self.cell(4, 6, "- ")
            self.multi_cell(0, 6, text)
        self.ln(1)


def create_pdf_v1(path):
    pdf = PDF()
    pdf.alias_nb_pages()
    pdf.title_text = "Lista de Precios - Capacitaciones Internas 2026"
    pdf.subtitle_text = "Departamento de Recursos Humanos - Version 1"
    pdf.set_auto_page_break(auto=True, margin=20)
    pdf.add_page()

    # Section 1
    pdf.section_title("1", "Cursos Disponibles")
    courses_v1 = [
        ("Curso de Seguridad Informatica: ", "200 USD por participante"),
        ("Curso de Gestion de Proyectos: ", "350 USD por participante"),
        ("Curso de Liderazgo y Comunicacion: ", "500 USD por participante"),
        ("Curso de Excel Avanzado: ", "150 USD por participante"),
        ("Curso de Atencion al Cliente: ", "180 USD por participante"),
    ]
    for bold, rest in courses_v1:
        pdf.item(rest, bold_prefix=bold)
    pdf.ln(4)

    # Section 2
    pdf.section_title("2", "Descuentos por Volumen")
    discounts = [
        "5-10 participantes: 10% de descuento",
        "11-20 participantes: 15% de descuento",
        "Mas de 20 participantes: 20% de descuento",
    ]
    for d in discounts:
        pdf.item(d)
    pdf.ln(4)

    # Section 3
    pdf.section_title("3", "Condiciones")
    conditions = [
        "Minimo 3 participantes por curso",
        "Los precios incluyen materiales y certificado",
        "Cancelaciones con 48 horas de anticipacion sin penalidad",
        "Reprogramacion permitida una sola vez",
    ]
    for c in conditions:
        pdf.item(c)

    pdf.output(path)


def create_pdf_v2(path):
    pdf = PDF()
    pdf.alias_nb_pages()
    pdf.title_text = "Lista de Precios - Capacitaciones Internas 2026"
    pdf.subtitle_text = "Departamento de Recursos Humanos - Version 2 (Actualizada)"
    pdf.set_auto_page_break(auto=True, margin=20)
    pdf.add_page()

    # Section 1
    pdf.section_title("1", "Cursos Disponibles")
    courses_v2 = [
        ("Curso de Seguridad Informatica: ", "450 USD por participante"),
        ("Curso de Gestion de Proyectos: ", "800 USD por participante"),
        ("Curso de Liderazgo y Comunicacion: ", "1200 USD por participante"),
        ("Curso de Excel Avanzado: ", "350 USD por participante"),
        ("Curso de Atencion al Cliente: ", "400 USD por participante"),
    ]
    for bold, rest in courses_v2:
        pdf.item(rest, bold_prefix=bold)
    pdf.ln(4)

    # Section 2
    pdf.section_title("2", "Descuentos por Volumen")
    discounts = [
        "5-10 participantes: 12% de descuento",
        "11-20 participantes: 18% de descuento",
        "Mas de 20 participantes: 25% de descuento",
    ]
    for d in discounts:
        pdf.item(d)
    pdf.ln(4)

    # Section 3
    pdf.section_title("3", "Condiciones")
    conditions = [
        "Minimo 3 participantes por curso",
        "Los precios incluyen materiales, certificado y almuerzo",
        "Cancelaciones con 72 horas de anticipacion sin penalidad",
        "Reprogramacion permitida hasta dos veces",
        "NUEVO: Se ofrece certificacion internacional por un costo adicional de 100 USD",
    ]
    for c in conditions:
        pdf.item(c)

    pdf.output(path)


if __name__ == "__main__":
    import os

    base = r"C:\Users\Raul\Desktop\AsistenteIA"
    p1 = os.path.join(base, "precios_capacitaciones_v1.pdf")
    p2 = os.path.join(base, "precios_capacitaciones_v2.pdf")

    create_pdf_v1(p1)
    create_pdf_v2(p2)

    for p in [p1, p2]:
        size = os.path.getsize(p)
        print(f"Creado: {p}  |  Tamano: {size:,} bytes")
