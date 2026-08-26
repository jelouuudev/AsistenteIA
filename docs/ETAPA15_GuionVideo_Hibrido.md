# Guion de Video — HÍBRIDO: Instalación rápida + Demo de uso (ETAPA 15)

**Duración objetivo:** 8–12 min · **Formato:** 1080p, 30 fps, MP4 (H.264) · **Idioma:** español
**Herramienta de grabación:** OBS Studio (fuente "Captura de pantalla") + micrófono
**Regla de oro:** NUNCA mostrar contraseñas/JWT en pantalla. Usar cartel "credenciales en manual de usuario".

---

## PARTE 1 — INSTALACIÓN RÁPIDA (Docker)

### Escena 1 · Intro (0:00–0:30)
- Muestra la portada del video.
- **Voz:** "En este video veremos cómo levantar el Asistente Inteligente Empresarial con Docker y cómo consultar datos reales de otro sistema mediante IA."

### Escena 2 · Levantar el stack (0:30–2:00)
- Terminal (Git Bash / PowerShell) situada en la carpeta del proyecto. Comandos copiables:
  ```bash
  cd C:\Users\Raul\Desktop\AsistenteIA
  docker compose --profile prod up -d
  docker ps
  ```
- Señala los 7 contenedores que aparecen (evidencia real verificada):

  | Contenedor | Puerto | Función |
  |---|---|---|
  | `asistenteweb` | 5206 | Interfaz web |
  | `asistenteapi` | 5298 | API REST |
  | `asistenteollama` | 11434 | IA local (DeepSeek) |
  | `asistentechroma` | 8000 | Base de conocimiento (RAG) |
  | `asistentesql` | 1433 | SQL Server |
  | `activos_app` | 8001 | Proyecto1 (Control de Activos Fijos) |
  | `activos_db` | 3307 | MySQL (no usada en esta demo) |

### Escena 3 · Acceder al sistema (2:00–3:00)
- Navegador → `http://localhost:5206`.
- Login admin: escribe usuario `admin`; en contraseña pon cartel "credencial en manual" o escríbela y bórrala. ⚠️ No dejar el password visible.

---

## PARTE 2 — DEMO DE USO

### Escena 4 · ⭐ Consulta en VIVO al Proyecto1 (3:00–5:30)  ← plato fuerte
- En el chat "Asistente General" escribe: **`muéstrame los activos fijos registrados`**
- El asistente devuelve la tabla de `ControlActivos` (SQL Server) con los 5 activos y estados reales:
  - ACT-0001 Laptop Dell Latitude 5420 → ACTIVO
  - ACT-0002 Monitor LG 24 pulgadas → ACTIVO
  - ACT-0003 Impresora HP LaserJet → ACTIVO
  - ACT-0004 Servidor Dell PowerEdge → ACTIVO
  - ACT-0005 Tablet Samsung Galaxy Tab → INACTIVO
- **Voz:** "El asistente no inventa — consulta la base de datos real del sistema de activos vía SQL Server, de forma autorizada y de solo lectura."

### Escena 5 · RAG de conocimiento (5:30–7:00)
- Pregunta: **`hablame de permisos remunerados`**
- Responde desde sus documentos de RRHH (TechCorp Solutions).
- **Voz:** "Dos fuentes: datos estructurados → BD en vivo (SQL); conocimiento → RAG documental."

### Escena 6 · Usuario del Ingeniero (7:00–8:00)
- Cierra sesión, login con `ingeniero` (cartel de credencial, sin mostrar password).
- Mismo nivel Administrador: puede hacer la misma consulta de activos.

### Escena 7 · Cierre (8:00–8:30)
- Resumen: Docker orquesta todo, RAG + SQL en vivo, listo para producción.

---

## Tips de grabación
- OBS: fuente "Captura de pantalla", 1080p 30fps, MP4.
- Microfóno para narrar.
- Nunca dejar passwords en pantalla (regla ETAPA 15).
- Si Ollama tarda (~2–3 min en CPU), dejar el reloj corriendo o acelerar el clip en edición.
- Usar la portada/thumbnail generado como imagen de intro.
