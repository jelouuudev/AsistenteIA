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
            self.cell(0, 8, "CloudSync Pro - Manual del Producto v4.2", align="L")
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
pdf.ln(50)
pdf.set_font("Arial", "B", 28)
pdf.set_text_color(0, 51, 102)
pdf.cell(0, 15, "CloudSync Pro", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.set_font("Arial", "", 16)
pdf.set_text_color(80, 80, 80)
pdf.cell(0, 10, "Manual del Producto", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.ln(5)
pdf.set_font("Arial", "", 12)
pdf.set_text_color(100, 100, 100)
pdf.cell(0, 8, "Version 4.2 - Julio 2026", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.ln(20)
pdf.set_font("Arial", "", 10)
pdf.set_text_color(60, 60, 60)
pdf.multi_cell(0, 6, (
    "CloudSync Pro es una plataforma empresarial de sincronizacion y colaboracion "
    "en la nube disenada para equipos de desarrollo de software. Ofrece integracion "
    "nativa con repositorios Git, pipelines CI/CD, gestion de configuracion y "
    "monitoreo en tiempo real."
), align="C")
pdf.ln(10)
pdf.set_font("Arial", "", 9)
pdf.set_text_color(80, 80, 80)
pdf.cell(0, 6, "TechCorp Solutions S.A. de C.V.", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.cell(0, 6, "Departamento de Ingenieria de Plataformas", align="C", new_x="LMARGIN", new_y="NEXT")
pdf.cell(0, 6, "Documento confidencial - Uso interno", align="C", new_x="LMARGIN", new_y="NEXT")

# === 1. INTRODUCCION ===
pdf.add_page()
pdf.section(1, "Introduccion")
pdf.txt("CloudSync Pro es la plataforma central de DevOps de TechCorp. Fue disenada para resolver los problemas de fragmentacion de herramientas que enfrentaba la empresa antes de su adopcion. Actualmente es utilizada por mas de 200 desarrolladores en 15 equipos distribuidos en Mexico, Colombia y Argentina.")
pdf.txt("Las principales ventajas competitivas de CloudSync Pro sobre soluciones como Jenkins o GitLab CI incluyen: despliegues blue-green automaticos, rollback en un solo comando, integracion nativa con Kubernetes y monitoreo de rendimiento post-despliegue con alertas configurables.")
pdf.txt("Esta plataforma NO reemplaza a Jira ni a Confluence. Es complementaria y se integra con ambas herramientas a traves de conectores oficiales. El equipo de producto mantiene una roadmap trimestral que incluye feedback directo de los equipos de desarrollo.")
pdf.txt("El costo de licenciamiento anual es de USD 45 por usuario activo, con descuentos del 20% para contratos plurianuales. Las licencias incluyen soporte tecnico 24/7 y actualizaciones automaticas.")

# === 2. REQUISITOS DEL SISTEMA ===
pdf.section(2, "Requisitos del Sistema")
pdf.sub("2.1", "Requisitos de Hardware")
pdf.txt("Para el servidor de CloudSync Pro se requiere como minimo:")
pdf.bul("Procesador: 4 nucleos minimos, 8 recomendados para produccion")
pdf.bul("Memoria RAM: 16 GB minimos, 32 GB para ambientes con mas de 50 repositorios activos")
pdf.bul("Disco duro: 500 GB SSD NVMe para el almacenamiento de artefactos y logs")
pdf.bul("Red: Conexion de al menos 1 Gbps entre el servidor y los runners de CI/CD")

pdf.sub("2.2", "Requisitos de Software")
pdf.txt("El servidor debe contar con las siguientes dependencias:")
pdf.bul("Sistema operativo: Ubuntu 22.04 LTS o CentOS Stream 9")
pdf.bul("Docker: version 24.0 o superior con Docker Compose v2")
pdf.bul("Kubernetes: 1.28+ (minikube para desarrollo local, EKS o GKE para produccion)")
pdf.bul("PostgreSQL: 15 o superior para la base de datos de metadatos")
pdf.bul("Redis: 7.0+ para la cola de tareas y cache de sesiones")
pdf.bul("Node.js: 20 LTS para el frontend de la interfaz web")

pdf.sub("2.3", "Requisitos del Cliente")
pdf.txt("La interfaz web de CloudSync Pro funciona en Chrome 90+, Firefox 88+, Safari 15+ y Edge 90+. Se recomienda usar Chrome o Firefox para la mejor experiencia. El modo oscuro esta disponible desde la version 4.0 y se activa automaticamente segun la preferencia del sistema operativo.")

# === 3. ARQUITECTURA ===
pdf.add_page()
pdf.section(3, "Arquitectura de la Plataforma")
pdf.txt("CloudSync Pro sigue una arquitectura de microservicios desplegados en Kubernetes. Los componentes principales son:")
pdf.bul("API Gateway (Kong): Punto de entrada unico para todas las solicitudes HTTP. Maneja autenticacion via JWT, rate limiting y routing a los microservicios internos.")
pdf.bul("Servicio de Repositorios: Encargado de la gestion de repositorios Git. Soporta GitHub, GitLab y Bitbucket. Implementa webhooks para recibir eventos de push y pull request.")
pdf.bul("Servicio de Pipelines: Motor de ejecucion de pipelines CI/CD. Soporta YAML como formato de definicion y paraleliza stages automaticamente.")
pdf.bul("Servicio de Despliegues: Gestiona blue-green deployments, canary releases y rollback. Se integra con Kubernetes via el API server.")
pdf.bul("Servicio de Monitoreo: Recopila metricas de despliegue, latencia de respuesta y error rates. Expone un dashboard via Grafana.")
pdf.bul("Servicio de Notificaciones: Envia alertas por Slack, correo electronico y webhooks personalizados.")
pdf.txt("Todos los servicios se comunican asincronamente via Apache Kafka para eventos de alto volumen, y via gRPC para llamadas de baja latencia entre servicios internos. El patron CQRS se aplica en el servicio de repositorios para separar operaciones de lectura y escritura.")

# === 4. INSTALACION ===
pdf.section(4, "Instalacion y Configuracion")
pdf.sub("4.1", "Instalacion con Docker Compose")
pdf.txt("Para ambientes de desarrollo y pruebas, la forma mas rapida de instalar CloudSync Pro es con Docker Compose. Ejecute los siguientes comandos:")
pdf.bul("git clone https://github.com/techcorp/cloudsync-pro.git")
pdf.bul("cd cloudsync-pro")
pdf.bul("cp .env.example .env")
pdf.bul("docker compose up -d")
pdf.txt("Una vez ejecutado este comando, la plataforma estara disponible en http://localhost:8080. El usuario administrador por defecto es admin@techcorp.com con contrasena changeme123. Es OBLIGATORIO cambiar esta contrasena en el primer inicio de sesion.")

pdf.sub("4.2", "Instalacion en Produccion")
pdf.txt("Para ambientes de produccion, se recomienda usar Helm charts oficiales. El chart incluye configuracion para High Availability, backups automaticos de PostgreSQL, y certificados TLS via cert-manager. Los valores por defecto del chart estan optimizados para un cluster de 3 nodos con 8 GB de RAM cada uno.")
pdf.txt("Antes de desplegar en produccion, es necesario configurar las siguientes variables de entorno: DATABASE_URL, REDIS_URL, JWT_SECRET (minimo 32 caracteres), SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASSWORD, y SLACK_WEBHOOK_URL. Ninguna de estas variables debe almacenarse en texto plano; se recomienda usar Kubernetes Secrets o Vault de HashiCorp.")

pdf.sub("4.3", "Configuracion Inicial Post-Instalacion")
pdf.txt("Despues de la instalacion, el administrador debe completar los siguientes pasos: (1) Crear los teams y asignar roles, (2) Conectar al menos un proveedor de repositorios (GitHub/GitLab), (3) Configurar los runners de CI/CD en los servidores de build, (4) Definir las politicas de despliegue por ambiente (dev/staging/production), (5) Configurar las alertas de notificacion.")

# === 5. USO DIARIO ===
pdf.add_page()
pdf.section(5, "Uso Diario del Desarrollador")
pdf.sub("5.1", "Crear un Repositorio")
pdf.txt("Para crear un repositorio, navegue a la seccion Repositorios en el menu lateral y haga clic en Nuevo Repositorio. Puede crear un repositorio vacio, importar desde GitHub/GitLab, o clonar un template predefinido. Los templates disponibles incluyen: API REST (.NET 8), Frontend (React + TypeScript), Microservicio (Go), y Data Pipeline (Python + Airflow).")
pdf.txt("Al crear un repositorio se generan automaticamente: un archivo .gitignore apropiado para el lenguaje seleccionado, un pipeline basico con stages de build y test, un webhook que notifica a Slack cuando el build falla, y una branch principal protegida que requiere pull request con al menos 2 aprobaciones.")

pdf.sub("5.2", "Configurar un Pipeline CI/CD")
pdf.txt("Los pipelines se definen en un archivo cloudsync.yml en la raiz del repositorio. Los stages se ejecutan en orden secuencial por defecto. Si un stage falla, los stages siguientes no se ejecutan a menos que se configure ignore_errors: true. El coverage minimo para que un build pase la etapa de testing es del 80% por defecto, configurable en el archivo de configuracion del proyecto.")
pdf.txt("Los runners de CI/CD pueden ser compartidos o dedicados. Los runners compartidos son ideales para proyectos pequenos mientras que los dedicados se recomiendan para proyectos criticos que requieren hardware especializado o acceso a redes privadas.")

pdf.sub("5.3", "Realizar un Despliegue")
pdf.txt("Los despliegues se ejecutan desde la pestana Deployments. El sistema soporta tres estrategias: Blue-Green (despliegue completo con switch de trafico), Canary (despliegue gradual del 5% al 100% en intervalos configurables), y Rolling Update (reemplazo progresivo de instancias). La estrategia por defecto para produccion es Blue-Green con health check automatico. Si el health check falla, el sistema realiza rollback automatico en menos de 30 segundos.")

pdf.sub("5.4", "Gestion de Ramas")
pdf.txt("CloudSync Pro implementa politicas de branching basadas en Git Flow. Las ramas develop, release y main se protegen automaticamente. Los pull requests requieren al menos 2 revisiones de codigo y que los checks de CI pasen antes de permitir el merge. La integracion con SonarQube asegura que no se mezcle codigo con deuda tecnica critica.")

# === 6. RESOLUCION DE PROBLEMAS ===
pdf.add_page()
pdf.section(6, "Resolucion de Problemas")
pdf.sub("6.1", "Errores Comunes")
pdf.txt("Los errores mas frecuentes y sus soluciones son:")
pdf.bul("Error 502 Bad Gateway: Verificar que todos los microservicios estan corriendo con docker compose ps. Si algun servicio esta restarted, revisar los logs con docker compose logs [servicio].")
pdf.bul("Timeout en pipelines: Incrementar el timeout en cloudsync.yml. El valor por defecto es 30 minutos. Para proyectos con builds largos se recomienda usar cache de Docker layers.")
pdf.bul("Fallo de autenticacion JWT: Verificar que JWT_SECRET coincide entre el API Gateway y los microservicios. Regenerar el secret si es necesario con el comando cloudsync regenerate-secret.")
pdf.bul("Error de conexion a PostgreSQL: Verificar que la variable DATABASE_URL usa el formato postgresql://user:password@host:port/database. No usar sslmode=require en ambientes de desarrollo local.")

pdf.sub("6.2", "Logs y Diagnostico")
pdf.txt("Todos los logs se centralizan en Elasticsearch y son consultables desde la interfaz web en la seccion Observability. Los logs se retienen por 30 dias en produccion y 7 dias en staging. Para diagnostico avanzado, use el comando cloudsync diagnose --component [nombre] que genera un reporte HTML con las metricas de salud del sistema.")

pdf.sub("6.3", "Rendimiento y Escalabilidad")
pdf.txt("Si el sistema presenta lentitud, verifique: (1) El uso de CPU y memoria en el dashboard de Kubernetes, (2) La latencia de Redis con redis-cli ping, (3) La conectividad de red entre pods con kubectl exec, (4) El espacio en disco de los volumes persistidos. El servicio de pipelines puede escalar horizontalmente anadiendo runners adicionales sin necesidad de reiniciar servicios existentes.")

pdf.sub("6.4", "Backup y Recuperacion")
pdf.txt("Los backups de PostgreSQL se realizan automaticamente cada 6 horas y se almacenan en S3 con retencion de 90 dias. Para restaurar un backup, use el comando cloudsync restore --date [fecha]. Los artefactos de build se retienen por 30 dias y los logs por 60 dias. El RPO garantizado es de 6 horas y el RTO es de 30 minutos para una recuperacion completa del sistema.")

# === 7. SEGURIDAD ===
pdf.add_page()
pdf.section(7, "Seguridad")
pdf.sub("7.1", "Autenticacion y Autorizacion")
pdf.txt("CloudSync Pro soporta autenticacion via LDAP/Active Directory, OAuth2 (Google, GitHub, Microsoft), y SAML 2.0 para SSO empresarial. La autorizacion se basa en roles (Admin, Maintainer, Developer, Viewer) con permisos granulares por repositorio y por ambiente.")
pdf.txt("Se recomienda habilitar 2FA (autenticacion de dos factores) para todos los usuarios. Los tokens de acceso API tienen un TTL maximo de 24 horas y deben rotarse periodicamente. El rate limiting por defecto es de 1000 requests por minuto por usuario.")

pdf.sub("7.2", "Cifrado de Datos")
pdf.txt("Todos los datos en transito se cifran con TLS 1.3. Los datos en reposo se cifran con AES-256-GCM. Los secrets (credenciales de bases de datos, tokens de API) se almacenan en Vault y nunca se exponen en logs o variables de entorno. Los backups se cifran con una clave de cifrado separada que se almacena de forma segura.")

pdf.sub("7.3", "Auditoria y Cumplimiento")
pdf.txt("CloudSync Pro genera logs de auditoria para todas las acciones sensibles: creacion/eliminacion de repositorios, cambios de permisos, despliegues a produccion, y acceso a secrets. Los logs de auditoria se almacenan de forma inmutable por 1 ano y son consultables desde la seccion Admin > Audit Log. El sistema cumple con SOC 2 Type II y ISO 27001.")

# === 8. COSTOS Y LICENCIAMIENTO ===
pdf.section(8, "Costos y Licenciamiento")
pdf.txt("CloudSync Pro ofrece tres planes de licenciamiento:")
pdf.bul("Plan Starter (USD 15/usuario/mes): Hasta 10 repositorios, 5 runners compartidos, 100 GB de almacenamiento de artefactos, soporte por correo electronico.")
pdf.bul("Plan Professional (USD 35/usuario/mes): Repositorios ilimitados, 20 runners dedicados, 500 GB de almacenamiento, soporte 24/7 por chat, integracion con LDAP.")
pdf.bul("Plan Enterprise (USD 45/usuario/mes): Todo lo del Plan Professional mas SAML SSO, auditoria avanzada, SLA del 99.9%, gestor de cuenta dedicado, y personalizacion de la plataforma.")
pdf.txt("Todos los planes incluyen un periodo de prueba gratuito de 30 dias sin tarjeta de credito. Los descuentos por volumen son: 10% para 50-100 usuarios, 15% para 101-500 usuarios, y 20% para mas de 500 usuarios. Los contratos anuales tienen un descuento adicional del 10%.")

# === 9. GLOSARIO ===
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

# === 10. SOPORTE ===
pdf.section(10, "Soporte y Contacto")
pdf.txt("Para soporte tecnico, puede contactar a nuestro equipo de ingenieria de plataformas:")
pdf.bul("Correo electronico: soporte-plataformas@techcorp.com")
pdf.bul("Telefono interno: ext. 4200")
pdf.bul("Slack: #soporte-cloudsync")
pdf.bul("Horario de soporte: Lunes a Viernes de 8:00 a 20:00 horas (hora de Ciudad de Mexico)")
pdf.bul("Para emergencias fuera de horario: +52 55 9876 5432 (disponible 24/7)")
pdf.txt("El tiempo de respuesta garantizado es: incidencias criticas (sistema caido) - 30 minutos; incidencias altas (funcionalidad degradada) - 2 horas; incidencias medias (funcionalidad afectada pero con workaround) - 8 horas; incidencias bajas (solicitudes de mejoras) - 3 dias habiles.")

output = "C:/Users/Raul/Desktop/AsistenteIA/Manual_Producto_CloudSync_Pro.pdf"
pdf.output(output)
print(f"PDF creado: {output}")
print(f"Paginas: {pdf.page_no()}")
