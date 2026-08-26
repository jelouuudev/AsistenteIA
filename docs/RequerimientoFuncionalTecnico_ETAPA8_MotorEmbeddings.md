# PROYECTO Asistente Inteligente Empresarial basado en IA Local

## Documento de Requerimiento Funcional y Técnico

## ETAPA 8 - Implementación del Motor de Embeddings, Base Vectorial y Recuperación Semántica (RAG)

---

## 1. Objetivo

Implementar el motor de indexación semántica del Asistente Inteligente Empresarial, permitiendo generar embeddings a partir de los fragmentos (chunks) procesados en la Etapa 7, almacenarlos en una base vectorial y recuperar automáticamente la información más relevante para responder preguntas utilizando la técnica Retrieval-Augmented Generation (RAG). El sistema deberá desacoplar la lógica de generación de embeddings y almacenamiento vectorial mediante interfaces, permitiendo reemplazar proveedores sin afectar la arquitectura.

## 2. Objetivos específicos

Al finalizar esta etapa el sistema deberá permitir:

- Generar embeddings para cada chunk procesado.
- Almacenar embeddings en una base vectorial.
- Indexar documentos automáticamente.
- Recuperar los fragmentos más relevantes según una consulta.
- Construir el contexto RAG para el modelo de IA.
- Reindexar documentos cuando cambien sus versiones.
- Administrar el ciclo de vida del índice vectorial.

## 3. Tecnologías obligatorias

### Backend
- ASP.NET Core 8
- C#

### Persistencia
- SQL Server
- Entity Framework Core

### Base vectorial
- ChromaDB (implementación inicial)

### Embeddings
- Modelo de embeddings ejecutado localmente mediante Ollama o un proveedor configurable.

### IA
- Ollama
- DeepSeek-R1-Distill-Qwen-7B (para generación de respuestas)

## 4. Alcance

### Esta etapa incluye:
- Generación de embeddings.
- Indexación vectorial.
- Recuperación semántica.
- Construcción del contexto RAG.
- Integración del RAG con el chat.

### No incluye:
- Ejecución de herramientas (Function Calling).
- Consultas automáticas a SQL Server.
- Integración con APIs externas.

## 5. Modelo de Datos

Crear las siguientes tablas:

### DocumentoIndexado
| Campo | Tipo |
|---|---|
| IdDocumentoIndexado | PK |
| IdDocumentoProcesado | FK |
| FechaIndexacion | DateTime |
| Estado | string |
| TotalChunks | int |
| TotalEmbeddings | int |
| Observaciones | string |

### EmbeddingConfiguracion
| Campo | Tipo |
|---|---|
| IdConfiguracion | PK |
| Proveedor | string |
| ModeloEmbeddings | string |
| BaseVectorial | string |
| CantidadResultados | int |
| PuntajeMinimo | decimal |
| Activo | bool |

### EstadoIndexacion (valores sugeridos)
- Pendiente
- EnProceso
- Indexado
- Error

## 6. Arquitectura

Se deberán definir las siguientes interfaces:

```csharp
public interface IEmbeddingProvider
{
    Task<float[]> GenerateEmbeddingAsync(string text);
}

public interface IVectorStore
{
    Task IndexAsync(VectorDocument document);
    Task<IEnumerable<VectorSearchResult>> SearchAsync(
        string query, int topK);
    Task DeleteDocumentAsync(Guid documentId);
}
```

La implementación inicial será:
- `OllamaEmbeddingProvider`
- `ChromaVectorStore`

La capa Application nunca deberá depender directamente de ChromaDB.

## 7. Actividades a desarrollar

### Actividad 1 - Servicio de generación de embeddings

El servicio deberá:
- Recibir un chunk.
- Solicitar el embedding.
- Validar la respuesta.
- Registrar errores.

### Actividad 2 - Proveedor de embeddings

El modelo utilizado deberá configurarse desde SQL Server o appsettings.json. No se permitirá codificar el nombre del modelo en la aplicación.

### Actividad 3 - Repositorio vectorial

Crear una implementación para ChromaDB utilizando la interfaz `IVectorStore`. Todas las operaciones deberán ejecutarse mediante servicios desacoplados.

### Actividad 4 - Indexación automática

Cuando un documento cambie a estado Procesado:
- Generar embeddings.
- Registrar los vectores.
- Actualizar el estado del documento.
- Registrar la fecha de indexación.

### Actividad 5 - Recuperación semántica

Ante una consulta del usuario:
- Generar embedding de la pregunta.
- Consultar la base vectorial.
- Recuperar los fragmentos más relevantes.
- Ordenarlos por similitud.

### Actividad 6 - Construcción del contexto RAG

El sistema deberá construir el contexto utilizando:
- Prompt del sistema.
- Configuración del asistente.
- Memoria conversacional.
- Fragmentos recuperados.
- Pregunta del usuario.

El contexto deberá generarse automáticamente antes de invocar el modelo.

### Actividad 7 - Configuración del motor RAG

Crear una pantalla para administrar:
- Cantidad máxima de resultados (Top-K).
- Puntaje mínimo de similitud.
- Modelo de embeddings.
- Base vectorial activa.
- Longitud máxima del contexto recuperado.

Toda la configuración deberá almacenarse en SQL Server.

### Actividad 8 - Administración de índices

Crear un módulo para:
- Reindexar un documento.
- Reindexar una categoría.
- Reindexar todos los documentos.
- Eliminar índices obsoletos.
- Consultar el estado de indexación.

### Actividad 9 - Monitoreo

Crear una pantalla donde el administrador pueda visualizar:
- Total de documentos indexados.
- Total de chunks.
- Total de embeddings.
- Tiempo promedio de indexación.
- Estado de la base vectorial.

### Actividad 10 - Integración con el chat

Modificar el flujo de conversación para que, antes de consultar al modelo, se ejecute automáticamente el proceso RAG. El usuario no deberá percibir diferencias en la interfaz.

## 8. Requisitos no funcionales

La solución deberá cumplir con:
- Clean Architecture.
- Principios SOLID.
- Programación asíncrona.
- DTO.
- FluentValidation.
- Serilog.
- Desacoplamiento entre proveedores.
- Sin dependencias directas entre la capa Application y la base vectorial.

## 9. Casos de prueba

### Caso 1 - Procesar un documento
**Resultado esperado:** Se generan correctamente los embeddings y el documento queda indexado.

### Caso 2 - Modificar un documento
**Resultado esperado:** Se elimina el índice anterior y se genera uno nuevo.

### Caso 3 - Realizar una pregunta existente en los manuales
**Resultado esperado:** El sistema recupera los fragmentos relevantes antes de responder.

### Caso 4 - Realizar una pregunta sin relación con los documentos
**Resultado esperado:** No se recupera contexto documental y la respuesta depende únicamente del modelo y del prompt configurado.

### Caso 5 - Modificar el valor Top-K
**Resultado esperado:** La recuperación semántica utiliza la nueva configuración sin necesidad de recompilar.

### Caso 6 - Consultar el panel de monitoreo
**Resultado esperado:** Se visualiza el estado del índice y las métricas principales.

## 10. Entregables

El desarrollador deberá entregar:

1. Solución actualizada.
2. Scripts SQL.
3. Migraciones de Entity Framework Core.
4. Implementación del proveedor de embeddings.
5. Implementación de la base vectorial (ChromaDB).
6. Servicio de indexación.
7. Motor RAG.
8. Módulo de administración de índices.
9. Panel de monitoreo.
10. Manual técnico.
11. Manual de usuario.
12. Documento de arquitectura del Motor RAG.
13. Video demostrativo (10 a 15 minutos) mostrando:
    - Generación de embeddings.
    - Indexación de documentos.
    - Recuperación semántica.
    - Construcción del contexto.
    - Respuesta del asistente utilizando información documental.
    - Reindexación de un documento.

## 11. Definición de Hecho (Definition of Done)

La etapa se considerará finalizada cuando:

- Todos los documentos procesados puedan indexarse automáticamente.
- Los embeddings se almacenen correctamente en la base vectorial.
- El motor RAG recupere información relevante antes de generar una respuesta.
- La arquitectura permita reemplazar el proveedor de embeddings y la base vectorial sin modificar la lógica de negocio.
- La solución compile sin errores ni advertencias críticas.
- Las pruebas funcionales y unitarias se ejecuten satisfactoriamente.

## 12. Criterios de aceptación

La etapa será aprobada cuando:

- El asistente responda utilizando información obtenida mediante búsqueda semántica.
- El contexto enviado al modelo incorpore automáticamente los fragmentos relevantes.
- La indexación y reindexación de documentos funcionen de manera automática y controlada.
- El sistema quede preparado para incorporar, en la siguiente etapa, múltiples colecciones documentales y estrategias avanzadas de recuperación sin realizar cambios estructurales.
