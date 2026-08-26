# Documento de Configuración del Motor de Prompts

## 1. Introducción

El Motor de Configuración del Asistente permite definir el comportamiento, la personalidad y el rol del modelo de IA (Ollama + DeepSeek-R1) sin modificar código fuente. Toda la configuración se gestiona desde la interfaz web (MVC) y se persiste en SQL Server.

## 2. Arquitectura del Motor

```
Usuario (Web) → Chat → ChatService → PromptSistemaService + AsistenteService → IOllamaService → Ollama
```

El motor se compone de tres capas:

- **Configuración del Asistente** (tabla `Asistente`): define el modelo, idioma, formalidad, restricciones y parámetros del modelo.
- **Prompt del Sistema** (tabla `PromptSistema`): instrucciones detalladas que guían el comportamiento del asistente.
- **Historial de cambios** (tabla `HistorialPrompt`): trazabilidad de cada modificación del prompt.

## 3. Construcción del System Prompt

El `ChatService` construye el System Prompt que se envía al modelo IA combinando la configuración del asistente y el prompt activo. A continuación se describe el formato generado:

```
Eres un asistente de IA llamado "{NombreAsistente}".
Tu propósito: {Descripcion}

Idioma: {Idioma}
Nivel de formalidad: {NivelFormalidad}
Formato de respuesta: {FormatoRespuesta}

Restricciones:
{Restricciones}

Mensaje de bienvenida:
{MensajeBienvenida}

INSTRUCCIONES DEL SISTEMA:
{ContenidoDelPromptActivo}
```

### 3.1 Comportamiento según parámetros

| Parámetro | Efecto en el prompt generado |
|-----------|------------------------------|
| **Idioma** | Se indica al modelo en qué idioma debe responder (es, en, pt, fr) |
| **NivelFormalidad** | `formal`: tono institucional; `profesional`: tono corporativo estándar; `casual`: lenguaje cotidiano; `amigable`: cálido y cercano |
| **Restricciones** | Se incluyen textualmente como reglas que el modelo debe seguir |
| **Temperatura** | Se pasa como parámetro a Ollama (0.0 = siempre la misma respuesta; 2.0 = respuestas muy variadas) |
| **MaxTokens** | Límite de tokens en la respuesta; evita respuestas truncadas o excesivamente largas |
| **FormatoRespuesta** | El modelo recibe instrucción de responder en el formato indicado |

### 3.2 Ejemplos de configuración

#### Ejemplo 1: Asistente Jurídico Formal

| Campo | Valor |
|-------|-------|
| Nombre | Asesor Jurídico |
| Idioma | es |
| Formalidad | formal |
| Restricciones | No dar asesoría legal vinculante. Citar siempre artículos de ley. |
| Temperatura | 0.3 |
| Contenido Prompt | Eres un abogado especialista en derecho laboral mexicano... |

#### Ejemplo 2: Asistente Creativo

| Campo | Valor |
|-------|-------|
| Nombre | Generador de Ideas |
| Idioma | es |
| Formalidad | casual |
| Restricciones | Evitar contenido inapropiado |
| Temperatura | 1.5 |
| Contenido Prompt | Ayudas a generar ideas creativas para campañas de marketing... |

## 4. Versionado de Prompts

Cada vez que un usuario edita un prompt, el sistema:

1. Crea una **nueva versión** (versión incrementada en 1).
2. Registra el **historial** con: contenido anterior, fecha, usuario y motivo del cambio.
3. El prompt **permanece inactivo** hasta que el usuario lo active explícitamente.

## 5. Validaciones del sistema

- **Un solo prompt activo por asistente**: al activar un prompt, los demás del mismo asistente se desactivan automáticamente.
- **Asistente inactivo**: no puede seleccionarse desde el chat ni desde el panel de pruebas.
- **Prompt inactivo**: no se incluye en la construcción del System Prompt.
- **Timeout**: si Ollama no responde en el tiempo configurado, se muestra un error al usuario.

## 6. API de integración

El motor expone endpoints REST para integración con sistemas externos:

| Método | Ruta | Propósito |
|--------|------|-----------|
| `GET` | `/api/asistentes` | Listar todos los asistentes |
| `POST` | `/api/asistentes` | Crear asistente |
| `GET` | `/api/prompts/asistente/{id}/activo` | Obtener prompt activo de un asistente |
| `POST` | `/api/prompts` | Crear prompt (versión 1) |
| `PUT` | `/api/prompts/{id}` | Actualizar prompt (nueva versión) |
| `POST` | `/api/prompts/probar` | Probar asistente con un mensaje |

## 7. Buenas prácticas

1. **Un asistente por dominio**: cree un asistente distinto para cada área de conocimiento (ej. Jurídico, Ventas, Soporte Técnico).
2. **Prompts específicos vs. genéricos**: los prompts deben ser detallados y específicos para obtener respuestas precisas.
3. **Pruebe antes de activar**: use el panel de pruebas para verificar el comportamiento antes de activar un prompt en producción.
4. **Mantenga historial**: registre siempre el motivo del cambio al editar un prompt para mantener trazabilidad.
5. **Ajuste la temperatura gradualmente**: comience con temperaturas bajas (0.3-0.5) para respuestas consistentes y aumente solo si necesita variedad creativa.
6. **Restricciones claras**: defina restricciones explícitas en el asistente para evitar respuestas no deseadas.

## 8. Troubleshooting

| Problema | Causa probable | Solución |
|----------|----------------|----------|
| El asistente no responde en el idioma configurado | El prompt activo contiene instrucciones contradictorias | Revise el contenido del prompt y las restricciones |
| Respuestas demasiado largas/cortas | MaxTokens mal configurado | Ajuste MaxTokens en la configuración del asistente |
| El modelo ignora las restricciones | Temperatura muy alta | Reduzca la temperatura (0.2-0.5) |
| No aparece el asistente en el chat | El asistente está inactivo | Active el asistente desde la administración |
| El prompt editado no se aplica | El prompt editado está inactivo | Active el prompt desde la administración |
