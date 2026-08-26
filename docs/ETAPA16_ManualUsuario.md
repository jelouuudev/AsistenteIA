# ETAPA 16 — Plataforma Multiagente: Manual de Usuario

**Proyecto:** Asistente Inteligente Empresarial (IA Local) — Versión 2.0
**Audiencia:** Administradores y usuarios finales

---

## 1. ¿Qué es la Plataforma Multiagente?

Ahora la plataforma permite crear y gestionar **varios agentes de IA especializados**, cada uno con su propósito, instrucciones, modelo, conocimiento y permisos. Ejemplos: un Agente de Soporte, uno Comercial, uno de RRHH, uno de Base de Datos.

Mantiene toda la seguridad, auditoría, RAG, herramientas y workflows de la Versión 1.0.

---

## 2. Acceso de administrador

1. Inicie sesión como **Administrador**.
2. En el menú, vaya a **Asistentes** (ahora "Agentes"). Verá el listado con el estado de cada agente.

---

## 3. Crear un agente

1. Clic en **Nuevo Agente**.
2. Complete:
   - **Código** (obligatorio, único): identificador corto, ej. `SOPORTE-01`.
   - **Nombre**: nombre visible.
   - **Descripción** y **Objetivo**: para qué sirve.
   - **Modelo IA**: ej. `deepseek-r1:7b`.
   - **Prompt del sistema**: instrucciones propias del agente (tiene prioridad sobre el prompt genérico).
   - **Asignaciones**: seleccione Fuentes de conocimiento (RAG), Herramientas, Workflows, Roles autorizados y Usuarios autorizados.
3. Clic en **Crear Agente**. Queda en estado **Borrador**.

---

## 4. Ciclo de vida del agente

| Estado | Significado | Acción para avanzar |
|--------|-----------|---------------------|
| **Borrador** | En edición | Editar y luego "Enviar a prueba" |
| **Prueba** | Listo para validar | "Publicar" cuando pase las pruebas |
| **Publicado** | Disponible para usuarios autorizados | Puede desactivarse |
| **Deshabilitado** | Fuera de servicio | Reactivar cuando se requiera |

**Flujo recomendado:** Borrador → (probar) → Prueba → (validar) → Publicado.

---

## 5. Probar antes de publicar

- En el listado, use el ícono **▶ Probar** para enviar el agente a prueba.
- Use la opción **Prueba** para conversar con el agente y validar su comportamiento.
- Solo publique un agente que haya pasado las pruebas.

---

## 6. Versiones

- Cada agente tiene un número de **Versión** (inicia en 1).
- Al hacer **Nueva Versión** (botón en "Versiones"), se incrementa y se guarda un snapshot de la configuración (modelo, estado, asignaciones).
- Esto permite auditar cómo evolucionó el agente.

---

## 7. Duplicar un agente

- El ícono **⧉ Duplicar** crea una copia del agente (con sus asignaciones) en estado **Borrador** y con un nuevo código. Útil para crear variantes rápidamente.

---

## 8. Usar un agente en el chat

1. En **Chat**, el selector de agentes solo muestra los agentes **autorizados para usted** (según su rol o asignación directa).
2. Seleccione el agente deseado.
3. Converse normalmente: el agente usará su prompt, fuentes y herramientas propias.

> Si recibe "No tienes autorización para utilizar este agente", solicite al administrador que le asigne el rol o el agente.

---

## 9. Dashboard Multiagente

- En **Asistentes → Dashboard** verá tarjetas con: total de agentes, publicados, en prueba, borradores, deshabilitados y versiones acumuladas, más una tabla resumen con las asignaciones de cada agente.

---

## 10. Notas de seguridad

- Un agente solo consulta las **fuentes que usted le asignó** (Regla 2).
- Un usuario solo usa agentes para los que tiene **rol o asignación** (Regla 1).
- Todas las interacciones quedan registradas en la **auditoría** con el agente y su versión.
