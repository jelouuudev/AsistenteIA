# =============================================================================
# MANUAL DE INSTALACIÓN
# Proyecto: Asistente Inteligente Empresarial
# ETAPA 15 - Actividad 25
# =============================================================================

## 1. Requisitos de Hardware

| Recurso | Mínimo | Recomendado |
|---|---|---|
| CPU | 4 núcleos | 8 núcleos |
| RAM | 16 GB | 32 GB |
| Disco | 100 GB SSD | 250 GB SSD |
| GPU | Opcional | NVIDIA 8GB+ VRAM |

## 2. Requisitos de Software

| Software | Versión | Descarga |
|---|---|---|
| Windows | 10/11 | Microsoft |
| Docker Desktop | 24.x | docker.com |
| SQL Server | 2022 | microsoft.com |
| Ollama | 0.1.x | ollama.com |
| .NET SDK | 8.0 | dotnet.microsoft.com |
| Git | 2.x | git-scm.com |

## 3. Instalación de SQL Server

### Opción A: SQL Server en Docker
```bash
docker pull mcr.microsoft.com/mssql/server:2022-latest
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=AsistenteSQL2026" \
    -p 1433:1433 --name asistentesql \
    -d mcr.microsoft.com/mssql/server:2022-latest
```

### Opción B: SQL Server Local
1. Descargar SQL Server 2022 Developer Edition
2. Instalar con autenticación mixta
3. Configurar SA password: `AsistenteSQL2026`
4. Habilitar TCP/IP en SQL Server Configuration Manager

## 4. Instalación de Ollama

### Windows
1. Descargar Ollama de https://ollama.com/download
2. Instalar la aplicación
3. Verificar instalación:
```bash
ollama --version
```

### Descargar modelo de IA
```bash
ollama pull deepseek-r1:7b
ollama pull nomic-embed-text
```

### Verificar modelos
```bash
ollama list
```

## 5. Instalación de ChromaDB

```bash
docker pull chromadb/chroma:latest
docker run -d --name chroma -p 8000:8000 chromadb/chroma:latest
```

## 6. Configuración del Proyecto

### 6.1 Clonar repositorio
```bash
git clone https://github.com/tu-usuario/AsistenteIA.git
cd AsistenteIA
```

### 6.2 Configurar variables de entorno
```bash
cp Configuracion/.env.production .env
# Editar .env con los valores correctos
```

### 6.3 Configurar Connection String
Editar `Asistente.API/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AsistenteIA;User Id=sa;Password=AsistenteSQL2026;TrustServerCertificate=true;"
  }
}
```

### 6.4 Configurar JWT
Editar `Asistente.API/appsettings.json`:
```json
{
  "JwtSettings": {
    "SecretKey": "TuClaveSecretaDeAlMenos32Caracteres!",
    "Issuer": "AsistenteIA",
    "Audience": "AsistenteIA_Users",
    "ExpiryMinutes": 480
  }
}
```

## 7. Migraciones

### 7.1 Crear base de datos
```bash
cd Asistente.Infrastructure
dotnet ef database update --startup-project ../Asistente.API
```

### 7.2 Verificar tablas creadas
```bash
docker exec asistentesql /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "AsistenteSQL2026" -C \
    -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE';"
```

## 8. Ejecución

### 8.1 Ejecutar con Docker Compose
```bash
cd AsistenteIA
docker compose --profile prod up -d --build
```

### 8.2 Verificar contenedores
```bash
docker ps
```

### 8.3 Verificar logs
```bash
docker logs asistenteapi
docker logs asistenteweb
```

## 9. Configuración Inicial

### 9.1 Acceder a la aplicación
- Web: http://localhost:5206
- API: http://localhost:5298

### 9.2 Usuarios por defecto
| Usuario | Contraseña | Rol |
|---|---|---|
| admin | Admin123* | Administrador |
| operador | Operador123* | Operador |
| supervisor | Supervisor123* | Supervisor |

### 9.3 Configurar asistentes
1. Ir a Configuración → Asistentes
2. Crear/editar asistentes
3. Asignar herramientas
4. Asignar fuentes de conocimiento

### 9.4 Configurar roles y permisos
1. Ir a Configuración → Roles
2. Asignar permisos a cada rol
3. Asignar roles a usuarios

## 10. Verificación Final

- [ ] Login funciona
- [ ] Chat responde
- [ ] RAG recupera documentos
- [ ] SQL funciona
- [ ] Tools funcionan
- [ ] Workflows funcionan
- [ ] Eventos funcionan
- [ ] Auditoría registra actividad
- [ ] Logs se generan correctamente
