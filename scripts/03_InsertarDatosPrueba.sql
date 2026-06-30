USE AsistenteIA;
GO

-- Insertar una conversación de ejemplo
INSERT INTO Conversacion (FechaInicio, Estado)
VALUES (GETUTCDATE(), 'Activa');

DECLARE @IdConv INT = SCOPE_IDENTITY();

-- Insertar mensajes de ejemplo
INSERT INTO Mensaje (IdConversacion, Rol, Contenido, FechaHora, TiempoRespuestaMs)
VALUES
    (@IdConv, 'User', 'Hola, ¿quién eres?', GETUTCDATE(), NULL),
    (@IdConv, 'Assistant', 'Soy un asistente de IA basado en DeepSeek, ejecutándome localmente con Ollama.', DATEADD(SECOND, 5, GETUTCDATE()), 5000);

SELECT * FROM Conversacion;
SELECT * FROM Mensaje;
GO
