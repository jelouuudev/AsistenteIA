# Asistente Inteligente Empresarial

Asistente de IA conversacional basado en **DeepSeek-R1-Distill-Qwen-7B** ejecutándose localmente con **Ollama**. Aplicación web desarrollada en **ASP.NET Core 8** con arquitectura por capas.

## Arquitectura

```
AsistenteIA.slnx
├── Asistente.Web          # Interfaz MVC (Bootstrap 5 + JS Fetch API)
├── Asistente.API          # Web API REST
├── Asistente.Application  # Casos de uso
├── Asistente.Domain       # Entidades e interfaces
├── Asistente.Infrastructure # SQL Server + Servicio Ollama
└── Asistente.Shared       # Modelos/DTOs comunes
```

## Requisitos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server 2022+](https://www.microsoft.com/sql-server)
- [Ollama](https://ollama.ai)
- Modelo DeepSeek-R1-Distill-Qwen-7B (`ollama pull deepseek-r1-distill-qwen-7b`)

## Configuración rápida

1. Clonar el repositorio
2. Ejecutar `scripts/01_CrearBaseDatos.sql` y `scripts/02_CrearTablas.sql` en SQL Server
3. Configurar cadena de conexión en `Asistente.API/appsettings.json`
4. Asegurar que Ollama esté corriendo (`ollama serve`)
5. Ejecutar ambos proyectos:
   - `dotnet run --project Asistente.API`
   - `dotnet run --project Asistente.Web`
6. Abrir navegador en `http://localhost:port`

## Endpoints API

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/chat/enviar` | Enviar mensaje a la IA |

## Documentación

- [Manual de instalación](docs/ManualInstalacion.md)
- [Manual técnico](docs/ManualTecnico.md)
- [Manual de usuario](docs/ManualUsuario.md)
