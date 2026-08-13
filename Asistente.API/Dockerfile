# Dockerfile - Asistente.API (ASP.NET Core 8)
# Multi-stage build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Cache de dependencias
COPY Asistente.API/Asistente.API.csproj Asistente.API/
COPY Asistente.Web/Asistente.Web.csproj Asistente.Web/
COPY Asistente.Application/Asistente.Application.csproj Asistente.Application/
COPY Asistente.Domain/Asistente.Domain.csproj Asistente.Domain/
COPY Asistente.Shared/Asistente.Shared.csproj Asistente.Shared/
COPY Asistente.Infrastructure/Asistente.Infrastructure.csproj Asistente.Infrastructure/
RUN dotnet restore Asistente.API/Asistente.API.csproj

# Copiar código fuente y publicar
COPY . .
RUN dotnet publish Asistente.API/Asistente.API.csproj -c Release -o /app/publish --no-restore -p:ErrorOnDuplicatePublishOutputFiles=false
# Asegurar que la API use su propio appsettings (Web se publica como dependencia y colisiona)
COPY Asistente.API/appsettings*.json /app/publish/

# Imagen final
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5298
EXPOSE 5298
ENTRYPOINT ["dotnet", "Asistente.API.dll"]
