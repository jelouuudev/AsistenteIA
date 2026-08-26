# Documento de Políticas de IA — ETAPA 14

Este documento define las políticas de IA gestionables desde **Configuración → Seguridad → Políticas de IA**.
Cada política tiene `Tipo`, `Valor` y `Activa`. El sistema las consulta en tiempo de ejecución para gobernar la IA.

## 1. Modelo permitido
- Tipo: `ModeloPermitido`
- Valor por defecto: `deepseek-r1:7b`
- Restringe qué modelo de Ollama puede usar la plataforma.

## 2. Tamaño máximo de contexto
- Tipo: `MaxContexto`
- Valor por defecto: `8192`
- Límite de tokens de contexto enviados al modelo.

## 3. Máximo de resultados RAG
- Tipo: `MaxResultadosRag`
- Valor por defecto: `5`
- Cantidad máxima de fragmentos de documentos recuperados inyectados al prompt.

## 4. Herramientas permitidas
- Tipo: `HerramientasPermitidas`
- Valor por defecto: `DocumentSearchTool,CalculatorTool,DateTimeTool`
- Lista de herramientas habilitadas globalmente (adicional al permiso por usuario).

## 5. Tiempo máximo de respuesta
- Tipo: `TiempoMaxRespuesta`
- Valor por defecto: `120000` (ms)
- Tiempo máximo de espera antes de abortar la generación.

## 6. Máximo de ejecuciones
- Tipo: `MaxEjecuciones`
- Valor por defecto: `10`
- Límite de ejecuciones de herramientas/workflows por interacción.

## 7. Fuentes autorizadas
- Tipo: `FuentesAutorizadas`
- Valor por defecto: `*`
- `*` = todas las fuentes del asistente; se puede restringir a identificadores específicos. Se combina con
  la asignación por usuario (`UsuarioFuente`).

## 8. Relación con el gobierno
- Las políticas se combinan con los **permisos** (por rol/usuario) y con los **controles de acceso** (asistentes/fuentes/herramientas).
- La IA nunca puede modificar estas políticas (Regla 5): el contenido RAG se trata como dato, no como instrucción.
- Todo cambio de política queda registrado en `AuditoriaActividad` (TipoOperacion = Configuración, Resultado = Exitoso/Bloqueado).
