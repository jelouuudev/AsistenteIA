# ETAPA 16 — Plataforma Multiagente: Guion de Video Demostrativo

**Duración:** 15-20 minutos (objetivo ~18 min)
**Idioma:** Español
**Restricción obligatoria:** NO mostrar contraseñas ni credenciales en pantalla.
**Debe demostrar (mínimo):** los 15 puntos listados en el RF.

---

## 0. CHECKLIST PRE-GRABACIÓN (hazlo antes de grabar)

1. **Docker Desktop levantado** (`docker compose --profile prod up -d`). Verificar que `asistentesql`, `asistentechroma`, `asistenteollama`, `asistenteapi`, `asistenteweb` están "Up".
2. **Login de admin listo** (usa autocompletado del navegador o sesión ya iniciada; no tecles la contraseña frente a cámara).
3. **Ollama "caliente":** antes de grabar, manda 1 consulta corta por el chat para que el modelo `deepseek-r1:7b` cargue en memoria (en CPU tarda; esto evita que la primera pregunta del video se quede vacía).
4. **Plan de 2 agentes con capacidades distintas:**
   - Agente A = **"Comercial"**: fuentes RAG de ventas + Tools (SqlQueryTool, ReportTool) + rol "Ventas".
   - Agente B = **"RRHH"**: solo fuentes RAG de políticas de personal (sin Tools, sin SQL) + rol "RRHH".
   Así el punto 14 (capacidades diferentes) queda demostrado de forma obvia.
5. Si Ollama se satura durante la grabación: `docker restart asistenteollama` y haz **una** consulta. No hagas 2 seguidas.

---

## 1. INTRODUCCIÓN (0:00 - 1:30)
- Objetivo: evolucionar de 1 asistente IA a una **plataforma multiagente** (V2.0).
- Mostrar el menú y la sección **Asistentes** (ahora "Agentes") + el **Dashboard**.
- Enfocar: decir que cada agente tendrá propósito, prompt, modelo, fuentes, tools, workflows y permisos propios.

## 2. CREAR AGENTE (1:30 - 3:00) — **Punto 1**
- Asistentes → **Nuevo Agente**.
- Enfocar: el campo **Código** (único, ej. `COMERCIAL-01`).
- Clic "Crear". Confirmar que aparece en estado **Borrador** (badge gris).

## 3. CONFIGURAR PROPÓSITO (3:00 - 4:00) — **Punto 2**
- Editar el agente → campo **Objetivo** (ej. "Atender consultas de ventas y cotizaciones").
- Enfocar: explicar que el propósito guía al agente y aparece en el contexto del system prompt.

## 4. CONFIGURAR SYSTEM PROMPT (4:00 - 5:30) — **Punto 3**
- Editar → **Prompt del sistema** (ej. "Eres el agente comercial de TechCorp. Usa solo datos de ventas. Formato profesional.").
- Enfocar: aclarar que este prompt **tiene prioridad** sobre el prompt genérico de la plataforma (Regla de ETAPA 16).

## 5. SELECCIONAR MODELO (5:30 - 6:15) — **Punto 4**
- Editar → **Modelo IA** = `deepseek-r1:7b`.
- Enfocar: el agente puede usar un modelo distinto al de otros agentes.

## 6. ASIGNAR FUENTES RAG (6:15 - 7:30) — **Punto 5**
- Editar → sección "Asignaciones" → **Fuentes de conocimiento** (multi-select): elegir las fuentes de ventas.
- Enfocar: **Regla 2** — el agente SOLO consultará estas fuentes (RAG restringido).

## 7. ASIGNAR TOOLS (7:30 - 8:45) — **Punto 6**
- Editar → **Herramientas**: marcar `SqlQueryTool` y `ReportTool`.
- Enfocar: el agente podrá ejecutar estas herramientas (Tool Orchestrator respeta la asignación).

## 8. ASIGNAR WORKFLOW (8:45 - 9:45) — **Punto 7**
- Editar → **Workflows**: marcar un workflow de ejemplo (ej. "Generar cotización").
- Enfocar: el agente puede disparar ese workflow autorizado.

## 9. ASIGNAR PERMISOS (9:45 - 11:00) — **Punto 8**
- Editar → **Roles autorizados** (marcar "Ventas") y **Usuarios autorizados** (directos, opcional).
- Enfocar: **Regla 1** — solo usuarios con ese rol (o asignados directo) verán/usarán el agente.
- Tip: para demostrarlo en el chat (punto 11-12), ten preparado un login de un usuario con rol "Ventas".

## 10. PROBAR EL AGENTE (11:00 - 13:00) — **Punto 9**
- En el listado: **Enviar a prueba** (badge amarillo "Prueba").
- Usar la opción **▶ Probar** para conversar y validar comportamiento.
- Enfocar: mostrar que el agente usa su prompt + sus fuentes/tools. (Mantén la pregunta corta por Ollama CPU.)

## 11. PUBLICAR (13:00 - 14:00) — **Punto 10**
- **Publicar** → badge verde "Publicado".
- Enfocar: solo se publica tras probar; ahora está disponible para usuarios autorizados.

## 12. INICIAR CONVERSACIÓN (14:00 - 16:00) — **Punto 11**
- Ir a **Chat**, loguarse como usuario con rol "Ventas".
- Enfocar: el **selector de agentes solo muestra agentes autorizados** (filtro por rol/usuario).
- Seleccionar "Comercial" → iniciar conversación.

## 13. UTILIZAR EL AGENTE (16:00 - 17:30) — **Punto 12**
- Preguntar algo que use sus capacidades (ej. "¿Cuántas ventas hubo este mes?" → usa SqlQueryTool; o una consulta de política de ventas → usa RAG).
- Enfocar: la respuesta proviene de SUS fuentes/tools, no de las de otros agentes.

## 14. CREAR SEGUNDO AGENTE (17:30 - 19:00) — **Punto 13**
- Repetir 2-11 con el agente **"RRHH"**: propósito de personal, prompt de RRHH, modelo igual, **solo fuentes de RRHH**, **sin Tools**, rol "RRHH".

## 15. CAPACIDADES DIFERENTES (19:00 - 20:30) — **Punto 14**
- En Chat, loguearse con rol "RRHH" → solo aparece "RRHH".
- Preguntar a RRHH por una política → responde por RAG.
- Preguntar a RRHH por datos de ventas/SQL → **no puede** (no tiene SqlQueryTool) → demuestra que cada agente tiene capacidades distintas.
- (Opcional) comparar respuestas de ambos agentes a la misma pregunta.

## 16. AUDITORÍA (20:30 - 22:00) — **Punto 15**
- Ir a **Auditoría** → mostrar los registros de las interacciones.
- Enfocar: cada entrada trae **nombre del agente** e **Id/Versión del agente** (trazabilidad multiagente).
- Cerrar recordando que toda interacción queda auditada por agente + versión.

## 17. CIERRE (22:00 - 23:00)
- Resumen: agentes especializados, versionados, publicables, con UI de admin y dashboard.
- Mencionar que es la base para ETAPA 17 (Agent Orchestrator).

---

## NOTAS DE PRODUCCIÓN
- 1080p, 30 fps.
- Resalta con el cursor los **badges de estado** (Borrador/Prueba/Publicado) y los **selects de asignación**.
- No grabes la escritura de contraseñas (usa sesión ya iniciada o autocompletado).
- Si Ollama tarda/responde vacío: reinicia el contenedor y vuelve a intentar UNA vez; explicando en voz que es limitación de CPU local.
- Música de fondo suave opcional, sin tapar el audio explicativo.
