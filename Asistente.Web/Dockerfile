# Dockerfile - Asistente.Web (ASP.NET Core MVC 8)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Asistente.Web/Asistente.Web.csproj Asistente.Web/
COPY Asistente.API/Asistente.API.csproj Asistente.API/
COPY Asistente.Application/Asistente.Application.csproj Asistente.Application/
COPY Asistente.Domain/Asistente.Domain.csproj Asistente.Domain/
COPY Asistente.Shared/Asistente.Shared.csproj /dev/null 2>/dev/null || true
COPY Asistente.Shared/Asistente.Shared.csproj Asistente.Shared/
COPY Asistente.Infrastructure/Asistente.Infrastructure.csproj Asistente.Infrastructure/
RUN dotnet restore Asistente.Web/Asistente.Web.csproj

COPY . .
RUN dotnet publish Asistente.Web/Asistente.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5206
EXPOSE 5206
ENTRYPOINT ["dotnet", "Asistente.Web.dll"]
