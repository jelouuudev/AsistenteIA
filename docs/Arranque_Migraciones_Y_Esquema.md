# Arranque de la API: migraciones, esquema y comportamiento ante fallos

## Qué ocurre al arrancar

1. `DbInitializer.SeedAsync` aplica las migraciones pendientes (`Database.MigrateAsync()`).
2. `VerificarEsquemaAsync` comprueba que **toda tabla que declara el modelo de EF exista
   realmente en la base**.
3. Solo entonces se siembra (roles, permisos, ámbitos de memoria, catálogo).
4. Si cualquiera de los tres pasos falla, la aplicación **no arranca**.

## Por qué el paso 2 existe

EF decide qué migraciones aplicar comparando el **modelo** contra el **snapshot**, nunca
contra la base de datos real. Si una migración crea una tabla con un nombre distinto al que
su propio snapshot registra, EF ve que modelo y esquema coinciden, no genera el rename y
`MigrateAsync` no tiene nada que aplicar. La desalineación queda oculta hasta que un
endpoint revienta con `Invalid object name`.

Pasó de verdad: la migración `20260810162521_Etapa14_SeguridadGobernanza` creó
`ConfiguracionRAG` mientras el modelo dice `ConfiguracionesRAG`. El chat devolvió **500 en
cada petición** que disparaba RAG durante casi dos meses, sin que nada lo delatara en el
arranque.

`VerificarEsquemaAsync` delata ese caso en el arranque, con el nombre de la tabla, en lugar
de dejarlo aparecer como un error por petición.

## Por qué ahora NO arranca si algo falla

Antes el `catch` de `Program.cs` solo escribía el log y la aplicación continuaba. Eso es
peor que no arrancar: un contenedor con el esquema roto sigue enpie, acepta tráfico y
falla petición a petición, y el síntoma apunta al endpoint en lugar de a la base de datos.

Con el comportamiento actual (relanzar la excepción) y `restart: always` de
`docker-compose.yml`, el contenedor reinicia solo hasta que la causa desaparece.

### Consecuencia en despliegues

Un esquema o un seed roto **detiene el servicio** en lugar de dejarlo sirviendo con
errores. Si algún entorno depende de que la aplicación arranque aunque la base no esté
lista, hay que revisar este punto.

## Aplicar migraciones a mano

Las migraciones se aplican solas al arrancar. Para aplicarlas sin arrancar la API:

```bash
dotnet ef database update \
  --project Asistente.Infrastructure \
  --startup-project Asistente.API \
  --connection "<cadena de conexión>"
```

`--connection` es necesario si la aplicación lee una cadena que solo resuelve dentro de la
red de Docker (por ejemplo `Server=mssql,1433`).

## Limpiar vectores de memoria huérfanos

Las memorias se indexan en la colección compartida de Chroma. Al borrar o desactivar una
memoria, su vector permanece hasta que se desindexa. `MemoryManager` lo hace en el camino
normal (`DesindexarAsync`); si una memoria se elimina por SQL directamente, el vector queda
como residuo y ocupa espacio. `MemorySafeVectorStore` no lo devuelve al RAG, pero sigue
ocupando sitio en la colección.