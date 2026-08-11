# Estrategia de Versionamiento y Control de Código Fuente — ETAPA 15

## 1. Ramas (Git Flow simplificado)
- `main` — código en producción. Solo recibe merges desde `release/*` o `hotfix`. Protegida.
- `develop` — integración continua de features. Base para nuevas features.
- `feature/*` — nuevas funcionalidades (ej. `feature/nuevo-asistente`).
- `bugfix/*` — correcciones sobre `develop` o `main`.
- `release/*` — preparación de versión (ej. `release/v1.1.0`).
- `hotfix/*` — parches urgentes en producción.

## 2. Reglas
- No se commitean secretos, API keys, tokens ni connection strings sensibles (ver `.gitignore`).
- Todo commit en `main`/`develop` debe compilar y pasar las pruebas.
- Los merges a `main` requieren tag de versión (`v1.0.0`, `v1.0.1`, `v1.1.0`, `v2.0.0`).
- Mensajes de commit descriptivos (español).

## 3. Versionado semántico (SemVer)
`MAYOR.MINOR.PATCH`
- MAYOR: cambios incompatibles / nueva etapa grande.
- MINOR: nuevas funcionalidades compatibles.
- PATCH: correcciones de errores.

Versión actual: **v1.0.0** (definida en `VERSION`).

## 4. Tags y registro
| Versión | Cambios | Fecha | Responsable |
|---------|---------|-------|-------------|
| v1.0.0  | Entrega final ETAPAs 1–15: plataforma completa de IA local con seguridad, gobierno, auditoría, observabilidad y despliegue. | 2026-08-10 | Equipo AsistenteIA |

## 5. Inicialización de ramas sugerida
```
git checkout -b develop main
git checkout -b feature/etapa15 develop
# ... trabajar ...
git checkout develop && git merge feature/etapa15
git checkout main && git merge develop
git tag -a v1.0.0 -m "Release v1.0.0"
```
