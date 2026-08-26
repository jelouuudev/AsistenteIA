from reportlab.lib.pagesizes import letter
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import inch
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, PageBreak
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

# Intentar registrar fuentes del sistema
try:
    pdfmetrics.registerFont(TTFont('Arial', 'C:/Windows/Fonts/arial.ttf'))
    pdfmetrics.registerFont(TTFont('Arial-Bold', 'C:/Windows/Fonts/arialbd.ttf'))
    font_name = 'Arial'
    font_bold = 'Arial-Bold'
except:
    font_name = 'Helvetica'
    font_bold = 'Helvetica-Bold'

def create_manual():
    output_path = "C:/Users/Raul/Desktop/AsistenteIA/Manual_Producto_CloudSync_Pro_v2.pdf"
    doc = SimpleDocTemplate(output_path, pagesize=letter)
    story = []
    styles = getSampleStyleSheet()
    
    # Estilos personalizados
    title_style = ParagraphStyle(
        'CustomTitle',
        parent=styles['Heading1'],
        fontName=font_bold,
        fontSize=28,
        textColor=(0, 0.2, 0.4),
        alignment=TA_CENTER,
        spaceAfter=20
    )
    
    subtitle_style = ParagraphStyle(
        'CustomSubtitle',
        parent=styles['Heading2'],
        fontName=font_name,
        fontSize=16,
        textColor=(0.3, 0.3, 0.3),
        alignment=TA_CENTER,
        spaceAfter=10
    )
    
    section_style = ParagraphStyle(
        'Section',
        parent=styles['Heading2'],
        fontName=font_bold,
        fontSize=14,
        textColor=(0, 0.2, 0.4),
        spaceAfter=10
    )
    
    subsection_style = ParagraphStyle(
        'Subsection',
        parent=styles['Heading3'],
        fontName=font_bold,
        fontSize=11,
        textColor=(0.2, 0.2, 0.2),
        spaceAfter=5
    )
    
    body_style = ParagraphStyle(
        'Body',
        parent=styles['Normal'],
        fontName=font_name,
        fontSize=10,
        textColor=(0.13, 0.13, 0.13),
        spaceAfter=6,
        leading=14
    )
    
    # PORTADA
    story.append(Spacer(1, 2*inch))
    story.append(Paragraph("CloudSync Pro", title_style))
    story.append(Paragraph("Manual del Producto", subtitle_style))
    story.append(Paragraph("Version 4.2 - Julio 2026", ParagraphStyle('Version', parent=styles['Normal'], fontName=font_name, fontSize=12, textColor=(0.4, 0.4, 0.4), alignment=TA_CENTER)))
    story.append(Spacer(1, 0.5*inch))
    story.append(Paragraph("CloudSync Pro es una plataforma empresarial de sincronizacion y colaboracion en la nube disenada para equipos de desarrollo de software. Ofrece integracion nativa con repositorios Git, pipelines CI/CD, gestion de configuracion y monitoreo en tiempo real.", body_style))
    story.append(Spacer(1, 0.5*inch))
    story.append(Paragraph("TechCorp Solutions S.A. de C.V.", ParagraphStyle('Company', parent=styles['Normal'], fontName=font_name, fontSize=9, textColor=(0.3, 0.3, 0.3), alignment=TA_CENTER)))
    story.append(Paragraph("Departamento de Ingenieria de Plataformas", ParagraphStyle('Dept', parent=styles['Normal'], fontName=font_name, fontSize=9, textColor=(0.3, 0.3, 0.3), alignment=TA_CENTER)))
    story.append(Paragraph("Documento confidencial - Uso interno", ParagraphStyle('Confidential', parent=styles['Normal'], fontName=font_name, fontSize=9, textColor=(0.3, 0.3, 0.3), alignment=TA_CENTER)))
    
    story.append(PageBreak())
    
    # SECCION 1: INTRODUCCION
    story.append(Paragraph("1. Introduccion", section_style))
    story.append(Paragraph("CloudSync Pro es la plataforma central de DevOps de TechCorp. Fue disenada para resolver los problemas de fragmentacion de herramientas que enfrentaba la empresa antes de su adopcion. Actualmente es utilizada por mas de 200 desarrolladores en 15 equipos distribuidos en Mexico, Colombia y Argentina.", body_style))
    story.append(Paragraph("Las principales ventajas competitivas de CloudSync Pro sobre soluciones como Jenkins o GitLab CI incluyen: despliegues blue-green automaticos, rollback en un solo comando, integracion nativa con Kubernetes y monitoreo de rendimiento post-despliegue con alertas configurables.", body_style))
    story.append(Paragraph("Esta plataforma NO reemplaza a Jira ni a Confluence. Es complementaria y se integra con ambas herramientas a traves de conectores oficiales. El equipo de producto mantiene una roadmap trimestral que incluye feedback directo de los equipos de desarrollo.", body_style))
    story.append(Paragraph("El costo de licenciamiento anual es de USD 45 por usuario activo, con descuentos del 20% para contratos plurianuales. Las licencias incluyen soporte tecnico 24/7 y actualizaciones automaticas.", body_style))
    
    story.append(PageBreak())
    
    # SECCION 2: REQUISITOS DEL SISTEMA
    story.append(Paragraph("2. Requisitos del Sistema", section_style))
    story.append(Paragraph("2.1 Requisitos de Hardware", subsection_style))
    story.append(Paragraph("Para el servidor de CloudSync Pro se requiere como minimo:", body_style))
    story.append(Paragraph("- Procesador: 4 nucleos minimos, 8 recomendados para produccion", body_style))
    story.append(Paragraph("- Memoria RAM: 16 GB minimos, 32 GB para ambientes con mas de 50 repositorios activos", body_style))
    story.append(Paragraph("- Disco duro: 500 GB SSD NVMe para el almacenamiento de artefactos y logs", body_style))
    story.append(Paragraph("- Red: Conexion de al menos 1 Gbps entre el servidor y los runners de CI/CD", body_style))
    
    story.append(Paragraph("2.2 Requisitos de Software", subsection_style))
    story.append(Paragraph("El servidor debe contar con las siguientes dependencias:", body_style))
    story.append(Paragraph("- Sistema operativo: Ubuntu 22.04 LTS o CentOS Stream 9", body_style))
    story.append(Paragraph("- Docker: version 24.0 o superior con Docker Compose v2", body_style))
    story.append(Paragraph("- Kubernetes: 1.28+ (minikube para desarrollo local, EKS o GKE para produccion)", body_style))
    story.append(Paragraph("- PostgreSQL: 15 o superior para la base de datos de metadatos", body_style))
    story.append(Paragraph("- Redis: 7.0+ para la cola de tareas y cache de sesiones", body_style))
    story.append(Paragraph("- Node.js: 20 LTS para el frontend de la interfaz web", body_style))
    
    story.append(PageBreak())
    
    # SECCION 9: GLOSARIO
    story.append(Paragraph("9. Glosario de Terminos", section_style))
    story.append(Paragraph("Definiciones de los terminos tecnicos utilizados en este manual:", body_style))
    story.append(Paragraph("<b>Blue-Green Deployment:</b> Estrategia de despliegue que mantiene dos entornos identicos (azul y verde) y alterna el trafico entre ellos.", body_style))
    story.append(Paragraph("<b>Canary Release:</b> Tecnica de despliegue gradual que expone un porcentaje pequeno de usuarios a la nueva version antes del lanzamiento completo.", body_style))
    story.append(Paragraph("<b>Pipeline:</b> Serie de pasos automatizados que se ejecutan cuando se detecta un cambio en el codigo fuente.", body_style))
    story.append(Paragraph("<b>Runner:</b> Servidor o contenedor que ejecuta las tareas definidas en un pipeline CI/CD.", body_style))
    story.append(Paragraph("<b>Rollback:</b> Proceso de revertir un despliegue a una version anterior estable.", body_style))
    story.append(Paragraph("<b>SLA (Service Level Agreement):</b> Acuerdo de nivel de servicio que define la disponibilidad y tiempo de respuesta garantizado.", body_style))
    story.append(Paragraph("<b>Webhook:</b> Mecanismo de notificacion automatica que envia datos a una URL especifica cuando ocurre un evento.", body_style))
    story.append(Paragraph("<b>Container:</b> Unidad empaquetada de software que incluye el codigo fuente, las dependencias y el runtime necesarios para ejecutarse de forma consistente en cualquier entorno.", body_style))
    story.append(Paragraph("<b>Kubernetes:</b> Plataforma de orquestacion de contenedores que automatiza el despliegue, escalado y gestion de aplicaciones containerizadas.", body_style))
    
    doc.build(story)
    print(f"PDF creado: {output_path}")
    print(f"Paginas: {doc.page}")

if __name__ == "__main__":
    create_manual()
