# Manual de Instalación

## Requisitos del sistema

- Windows 10/11, Linux o macOS
- .NET SDK 8.0
- SQL Server 2022 o superior (o SQL Server Express)
- Ollama
- Git

## Paso 1: Instalar .NET SDK 8.0

Descargar e instalar desde: https://dotnet.microsoft.com/download/dotnet/8.0

Verificar instalación:
```bash
dotnet --version
```

## Paso 2: Instalar SQL Server

Descargar e instalar SQL Server 2022 Express o Developer desde:
https://www.microsoft.com/sql-server/sql-server-downloads

## Paso 3: Instalar Ollama

Descargar e instalar desde: https://ollama.ai/download

## Paso 4: Descargar el modelo

```bash
ollama pull deepseek-r1-distill-qwen-7b
```

## Paso 5: Clonar el proyecto

```bash
git clone <url-del-repositorio>
cd AsistenteIA
```

## Paso 6: Configurar base de datos

Ejecutar los scripts SQL en orden:
1. `scripts/01_CrearBaseDatos.sql`
2. `scripts/02_CrearTablas.sql`

## Paso 7: Configurar cadena de conexión

Editar `Asistente.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AsistenteIA;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

## Paso 8: Iniciar Ollama

```bash
ollama serve
```

## Paso 9: Ejecutar la aplicación

En dos terminales separadas:

```bash
# Terminal 1 - API
cd AsistenteIA
dotnet run --project Asistente.API

# Terminal 2 - Web
cd AsistenteIA
dotnet run --project Asistente.Web
```

## Paso 10: Acceder

Abrir navegador en la URL del proyecto Web (por defecto http://localhost:port)
