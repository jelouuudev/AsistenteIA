# Manual de Instalación — Asistente Inteligente Empresarial (ETAPA 15)

## 1. Requisitos de hardware
- CPU: 4 núcleos mínimo (8+ recomendado).
- RAM: 16 GB mínimo (32 GB recomendado para Ollama + SQL).
- GPU: opcional pero recomendada (NVIDIA 8 GB+) para acelerar DeepSeek.
- Disco: 50 GB libres (modelos Ollama ~5 GB, ChromaDB según documentos).

## 2. Requisitos de software
- Windows 10/11 o Linux.
- .NET 8 SDK.
- SQL Server 2022 (Developer/Express).
- Ollama (https://ollama.com).
- Docker Desktop (para ChromaDB y contenedores).
- Git.

## 3. Instalación de SQL Server
- Instalar SQL Server 2022 y SQL Server Management Studio.
- Crear login y base `AsistenteIA` (o usar Windows Auth con Trusted_Connection).
- Ajustar `ConnectionStrings:DefaultConnection` en `appsettings.json` / `.env`.

## 4. Instalación de Ollama
- Descargar e instalar Ollama.
- `ollama pull deepseek-r1:7b` (o `qwen2.5:14b`).
- `ollama pull nomic-embed-text` (embeddings).
- Verificar: `curl http://localhost:11434/api/tags`.

## 5. Instalación de ChromaDB (RAG)
- `docker run -p 8000:8000 chromadb/chroma:0.4.24`
- O vía compose: `docker compose --profile dev up chromadb`.

## 6. Configuración del proyecto
- Clonar repo: `git clone <repo> && cd AsistenteIA`.
- `dotnet restore`.
- Copiar `.env.example` a `.env` y ajustar secretos (NO commitear `.env`).

## 7. Variables de entorno
- Ver `.env.example`: puertos, SQL_SA_PASSWORD, API_DB_CONNECTION, OLLAMA_URL, OLLAMA_MODEL, JWT_SECRET, CHROMA_AUTH.

## 8. Migraciones
- `dotnet tool install --global dotnet-ef` (si no está).
- `dotnet ef database update --project Asistente.Infrastructure --startup-project Asistente.API`
- El seed (roles, permisos, políticas, admin/operador/supervisor/usuario) se ejecuta al arrancar la API.

## 9. Ejecución (desarrollo)
- API: `dotnet run --project Asistente.API` → http://localhost:5298
- Web: `dotnet run --project Asistente.Web` → http://localhost:5206

## 10. Ejecución (producción / Docker)
- `docker compose --profile prod up -d --build`
- Web en `:5206`, API en `:5298`.

## 11. Configuración inicial
- Login admin (`admin` / contraseña del seed).
- En **Configuración → Seguridad**: crear usuarios, asignar roles, asignar asistentes/fuentes por usuario.
- Cargar documentos (PDF/TXT) para RAG.
- Verificar `/api/health` = 200.
