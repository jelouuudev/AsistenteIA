# Manual de Usuario

## Acceso al sistema

1. Asegúrese de que Ollama esté ejecutándose
2. Abra la API y el Web (ver Manual de Instalación)
3. Abra su navegador en la dirección del proyecto Web

## Interfaz de chat

La interfaz consta de:

- **Encabezado**: Muestra "Asistente Inteligente Empresarial"
- **Área de conversación**: Muestra el historial de mensajes
- **Campo de texto**: Para escribir sus preguntas
- **Botón Enviar**: Envía el mensaje a la IA
- **Botón Limpiar**: Limpia la conversación actual
- **Indicador de carga**: Animación mientras la IA genera respuesta

## Cómo usar

1. Escriba su pregunta en el campo de texto
2. Presione Enter o haga clic en "Enviar"
3. Espere la respuesta de la IA
4. Continúe la conversación o limpie el historial

## Casos de prueba

### Caso 1: Pregunta normal
- Escriba "¿Cuál es la capital de Francia?"
- Resultado: La IA responde "París"

### Caso 2: Mensaje vacío
- Deje el campo vacío y presione Enviar
- Resultado: Mensaje "El mensaje no puede estar vacío."

### Caso 3: Servicio no disponible
- Detenga Ollama e intente enviar un mensaje
- Resultado: Mensaje de error indicando que Ollama no está disponible

### Caso 4: Verificar registro en BD
- Use SQL Server Management Studio para consultar:
  ```sql
  SELECT * FROM AsistenteIA.dbo.Conversacion
  SELECT * FROM AsistenteIA.dbo.Mensaje
  ```

### Caso 5: Cambiar modelo
- Edite `appsettings.json` de la API y cambie el modelo en `Ollama.Modelo`
- Resultado: La aplicación usa el nuevo modelo sin recompilar
