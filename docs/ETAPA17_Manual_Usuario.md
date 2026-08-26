# Manual de Usuario — ETAPA 17: Agent Orchestrator

**Asistente Inteligente Empresarial** · Versión 2.0

---

## ¿Qué es el Agent Orchestrator?

Es la "central de coordinación" del sistema. Permite que **una sola pregunta** sea resuelta por varios
agentes especializados trabajando juntos (por ejemplo: Comercial consulta la base de datos, Soporte busca en
el manual, y Reportes redacta el resumen), sin que ningún agente hable directamente con otro: todo pasa por
el Orchestrator, que además registra cómo se construyó la respuesta.

## Acceso

Desde el menú superior → **Agent Orchestrator (ETAPA 17)** (requiere rol Administrador/Operador/Superador).
Para administración de reglas, se requiere **Administrador**.

---

## 1. Ejecutar una solicitud orquestada

1. Entra a **Agent Orchestrator**.
2. Selecciona el **Agente principal** (el que recibe la pregunta).
3. Escribe la **solicitud** (una sola frase puede involucrar varios agentes), ej.:
   > "Analiza las ventas del mes, prepara un resumen ejecutivo y compáralo con el procedimiento del manual."
4. Pulsa **Ejecutar Orchestrator**.
5. Verás la **respuesta consolidada** (una sola, de todos los agentes) y el desglose de trazas.

> También puedes activarlo desde el **Chat** normal: elige un asistente y marca la casilla
> **"Permitir colaboración multi-agente (Orchestrator)"** antes de enviar el mensaje.

## 2. Dashboard de ejecuciones

Muestra: total de ejecuciones, completadas, con error, tiempo promedio, y la lista de ejecuciones con
agentes participantes, profundidad y herramientas. Desde ahí puedes abrir **Trazas** de cada una.

## 3. Visualizador de trazas

Explica paso a paso cómo se construyó la respuesta: orden de agentes, acción, estado, tiempo y los
eventos registrados (Inicio, SeleccionAgentes, EjecucionNodo, Consolidacion, Fin). Útil para auditoría
y soporte.

## 4. Reglas de colaboración (solo Administrador)

Define qué agentes pueden colaborar (Regla 3). Por defecto **nadie puede** colaborar salvo que exista
una regla explícita que lo permita.

- **Crear**: elige Agente origen → Agente destino, marca *Permitido* (o denegar), define prioridad y si está activa.
- **Eliminar**: borra la regla.
- Ejemplo: `Comercial → Reportes = Sí`, `Comercial → RRHH = No`.

## 5. Seguridad y límites

- Solo colaboran agentes **autorizados** para ti y permitidos por regla.
- El Orchestrator limita la **profundidad** y la **cantidad de agentes** por solicitud (configurable).
- Cada ejecución queda **auditada** completamente (Regla 6).

## 6. Manejo de errores

Si un agente falla, el Orchestrator decide según la configuración: continúa omitiéndolo, reintenta, o
cancela. Siempre se registra en la traza.
