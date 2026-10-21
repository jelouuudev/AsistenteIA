using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Application.Services.Memoria;
using Asistente.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asistente.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AsistenteDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Ensure database is created/migrated
        await context.Database.MigrateAsync();
        await VerificarEsquemaAsync(context);

        // Seed Roles
        if (!await context.Roles.AnyAsync())
        {
            var roles = new[]
            {
                new Rol { Nombre = "Administrador", Descripcion = "Puede acceder a todo el sistema.", Activo = true },
                new Rol { Nombre = "Operador", Descripcion = "Puede utilizar el asistente.", Activo = true },
                new Rol { Nombre = "Supervisor", Descripcion = "Puede consultar auditorías.", Activo = true }
            };

            await context.Roles.AddRangeAsync(roles);
            await context.SaveChangesAsync();
        }

        // Seed CategoriaDocumento
        if (!await context.CategoriasDocumento.AnyAsync())
        {
            var categorias = new[]
            {
                new CategoriaDocumento { Nombre = "Manual Usuario", Descripcion = "Manuales dirigidos a usuarios finales.", Activo = true },
                new CategoriaDocumento { Nombre = "Manual Técnico", Descripcion = "Documentación técnica de sistemas.", Activo = true },
                new CategoriaDocumento { Nombre = "Procedimientos", Descripcion = "Guías de procedimientos operativos y organizacionales.", Activo = true },
                new CategoriaDocumento { Nombre = "Políticas", Descripcion = "Líneas de conducta y políticas empresariales.", Activo = true },
                new CategoriaDocumento { Nombre = "Normativas", Descripcion = "Reglas, estándares y normativas aplicables.", Activo = true },
                new CategoriaDocumento { Nombre = "FAQ", Descripcion = "Preguntas frecuentes y respuestas rápidas.", Activo = true },
                new CategoriaDocumento { Nombre = "Capacitaciones", Descripcion = "Material y guías de capacitación del personal.", Activo = true }
            };

            await context.CategoriasDocumento.AddRangeAsync(categorias);
            await context.SaveChangesAsync();
        }

        // Seed Users
        var adminRol = await context.Roles.FirstAsync(r => r.Nombre == "Administrador");
        var operadorRol = await context.Roles.FirstAsync(r => r.Nombre == "Operador");
        var supervisorRol = await context.Roles.FirstAsync(r => r.Nombre == "Supervisor");

        // Create admin if not exists
        if (!await context.Usuarios.AnyAsync(u => u.UsuarioNombre == "admin"))
        {
            var admin = new Usuario
            {
                UsuarioNombre = "admin",
                Nombres = "Administrador",
                Apellidos = "Principal",
                Correo = "admin@asistenteia.local",
                PasswordHash = passwordHasher.HashPassword("Admin123*"),
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            admin.UsuarioRoles.Add(new UsuarioRol { Rol = adminRol });
            await context.Usuarios.AddAsync(admin);
        }

        // Create operador if not exists
        if (!await context.Usuarios.AnyAsync(u => u.UsuarioNombre == "operador"))
        {
            var operador = new Usuario
            {
                UsuarioNombre = "operador",
                Nombres = "Operador",
                Apellidos = "Asistente",
                Correo = "operador@asistenteia.local",
                PasswordHash = passwordHasher.HashPassword("Operador123*"),
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            operador.UsuarioRoles.Add(new UsuarioRol { Rol = operadorRol });
            await context.Usuarios.AddAsync(operador);
        }

        // Create supervisor if not exists
        if (!await context.Usuarios.AnyAsync(u => u.UsuarioNombre == "supervisor"))
        {
            var supervisor = new Usuario
            {
                UsuarioNombre = "supervisor",
                Nombres = "Supervisor",
                Apellidos = "Auditoria",
                Correo = "supervisor@asistenteia.local",
                PasswordHash = passwordHasher.HashPassword("Supervisor123*"),
                Activo = true,
                FechaCreacion = DateTime.UtcNow
            };
            supervisor.UsuarioRoles.Add(new UsuarioRol { Rol = supervisorRol });
            await context.Usuarios.AddAsync(supervisor);
        }

        await context.SaveChangesAsync();

        // Seed EmbeddingConfiguracion
        if (!await context.ConfiguracionesEmbedding.AnyAsync())
        {
            var config = new EmbeddingConfiguracion
            {
                Proveedor = "Ollama",
                ModeloEmbeddings = "nomic-embed-text",
                BaseVectorial = "ChromaDB",
                CantidadResultados = 5,
                PuntajeMinimo = 0.5,
                LongitudMaximaContexto = 4000,
                Activo = true
            };

            await context.ConfiguracionesEmbedding.AddAsync(config);
            await context.SaveChangesAsync();
        }

        // Seed Herramientas del Motor de Herramientas (ETAPA 11)
        if (!await context.Herramientas.AnyAsync())
        {
            var herramientas = new[]
            {
                new Herramienta
                {
                    Nombre = "Búsqueda Documental",
                    Codigo = "DocumentSearchTool",
                    Descripcion = "Recupera fragmentos de documentos internos mediante búsqueda semántica (Motor RAG).",
                    Categoria = "ConsultaDocumental",
                    Activa = true,
                    RequierePermiso = true,
                    FechaRegistro = DateTime.UtcNow
                },
                new Herramienta
                {
                    Nombre = "Consulta SQL",
                    Codigo = "SqlQueryTool",
                    Descripcion = "Ejecuta consultas SELECT de solo lectura sobre la base de datos empresarial autorizada.",
                    Categoria = "ConsultaSQL",
                    Activa = true,
                    RequierePermiso = true,
                    FechaRegistro = DateTime.UtcNow
                },
                new Herramienta
                {
                    Nombre = "Calculadora",
                    Codigo = "CalculatorTool",
                    Descripcion = "Evalúa expresiones matemáticas de forma segura.",
                    Categoria = "Utilidad",
                    Activa = true,
                    RequierePermiso = false,
                    FechaRegistro = DateTime.UtcNow
                },
                new Herramienta
                {
                    Nombre = "Fecha y Hora",
                    Codigo = "DateTimeTool",
                    Descripcion = "Devuelve la fecha y hora actual del servidor.",
                    Categoria = "Utilidad",
                    Activa = true,
                    RequierePermiso = false,
                    FechaRegistro = DateTime.UtcNow
                },
                new Herramienta
                {
                    Nombre = "Generador de Reportes",
                    Codigo = "ReportTool",
                    Descripcion = "Genera reportes estructurados en Markdown a partir de datos obtenidos.",
                    Categoria = "Reporte",
                    Activa = true,
                    RequierePermiso = false,
                    FechaRegistro = DateTime.UtcNow
                },
                new Herramienta
                {
                    Nombre = "Conector via Gateway",
                    Codigo = "GatewayConnectorTool",
                    Descripcion = "Consume un sistema externo (ERP, CRM, correo, SharePoint, API REST/SOAP) a través del API Gateway Empresarial. Parámetros: conector (código registrado), operacion, recurso (ruta o destinatario), metodo (GET/POST/PUT/DELETE/PATCH), cuerpo.",
                    Categoria = "Integracion",
                    Activa = true,
                    RequierePermiso = true,
                    FechaRegistro = DateTime.UtcNow
                }
            };

            await context.Herramientas.AddRangeAsync(herramientas);
            await context.SaveChangesAsync();
        }

        // ETAPA 20: la herramienta del Gateway debe existir aunque la tabla ya
        // tenga filas (el seed de arriba solo corre con tabla vacía).
        if (!await context.Herramientas.AnyAsync(h => h.Codigo == "GatewayConnectorTool"))
        {
            await context.Herramientas.AddAsync(new Herramienta
            {
                Nombre = "Conector via Gateway",
                Codigo = "GatewayConnectorTool",
                Descripcion = "Consume un sistema externo (ERP, CRM, correo, SharePoint, API REST/SOAP) a través del API Gateway Empresarial. Parámetros: conector (código registrado), operacion, recurso (ruta o destinatario), metodo (GET/POST/PUT/DELETE/PATCH), cuerpo.",
                Categoria = "Integracion",
                Activa = true,
                RequierePermiso = true,
                FechaRegistro = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // ETAPA 21: herramienta de memoria + catálogos (idempotentes).
        if (!await context.Herramientas.AnyAsync(h => h.Codigo == "MemoryTool"))
        {
            await context.Herramientas.AddAsync(new Herramienta
            {
                Nombre = "Memoria institucional",
                Codigo = "MemoryTool",
                Descripcion = "Consulta y guarda memoria institucional (formatos de reportes, definiciones, preferencias, contexto de workflows). Parámetros: operacion (buscar|leer|guardar), texto, codigo, titulo, contenido, ambito (Usuario|Agente|Organizacion|Workflow).",
                Categoria = "Memoria",
                Activa = true,
                RequierePermiso = true,
                FechaRegistro = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var ambitosEtapa21 = new (string Codigo, string Nombre, string Descripcion)[]
        {
            ("Conversacional", "Conversacional", "Contexto de conversaciones"),
            ("Usuario", "Usuario", "Preferencias y datos del usuario"),
            ("Agente", "Agente", "Conocimiento propio del agente"),
            ("Organizacion", "Organización", "Conocimiento institucional compartido"),
            ("Workflow", "Workflow", "Estado persistente de workflows")
        };
        foreach (var (codigo, nombre, descripcion) in ambitosEtapa21)
        {
            if (!await context.MemoryScopes.AnyAsync(s => s.Codigo == codigo))
            {
                await context.MemoryScopes.AddAsync(new MemoryScope
                    { Codigo = codigo, Nombre = nombre, Descripcion = descripcion, Activo = true });
            }
        }
        await context.SaveChangesAsync();

        var categoriasEtapa21 = new (string Codigo, string Nombre)[]
        {
            ("FormatoReporte", "Formato de reporte"),
            ("Definicion", "Definición institucional"),
            ("Preferencia", "Preferencia de usuario"),
            ("Hecho", "Hecho operativo"),
            ("Procedimiento", "Procedimiento")
        };
        foreach (var (codigo, nombre) in categoriasEtapa21)
        {
            if (!await context.MemoryCategories.AnyAsync(c => c.Codigo == codigo))
            {
                await context.MemoryCategories.AddAsync(new MemoryCategory
                    { Codigo = codigo, Nombre = nombre, Activo = true });
            }
        }
        await context.SaveChangesAsync();

        // Política global por defecto: 365 días, desactiva (no purga).
        if (!await context.MemoryRetentionPolicies.AnyAsync(p => p.IdScope == null))
        {
            await context.MemoryRetentionPolicies.AddAsync(new MemoryRetentionPolicy
            {
                IdScope = null,
                DiasRetencion = 365,
                EliminarAlExpirar = false,
                ConservarTrazabilidad = true,
                Activo = true
            });
            await context.SaveChangesAsync();
        }

        // Seed ConfiguracionOrchestrator
        if (!await context.ConfiguracionesOrchestrator.AnyAsync())
        {
            await context.ConfiguracionesOrchestrator.AddAsync(new ConfiguracionOrchestrator
            {
                Habilitado = true,
                Prioridad = 100,
                TiempoMaximoEjecucionMs = 30000,
                MaxEjecucionesSimultaneas = 4,
                RequiereAutorizacion = true,
                FechaActualizacion = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // Seed ConfiguracionWorkflow
        if (!await context.ConfiguracionesWorkflow.AnyAsync())
        {
            await context.ConfiguracionesWorkflow.AddAsync(new ConfiguracionWorkflow
            {
                ReintentosMaximos = 2,
                TiempoMaximoPasoMs = 30000,
                TiempoMaximoFlujoMs = 180000,
                ConfirmacionesObligatorias = true,
                LimitePasosPorWorkflow = 10,
                FechaActualizacion = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // Seed Workflow de ejemplo: "Resumen de Documento" (resume el contenido de un
        // documento recién indexado; se dispara vía DOC_INDEXADO, cuando los vectores ya
        // existen en la base vectorial — no vía DOC_PROCESADO, que llega antes de indexar).
        if (!await context.Workflows.AnyAsync())
        {
            var adminId = (await context.Usuarios.FirstOrDefaultAsync(u => u.UsuarioNombre == "admin"))?.IdUsuario ?? 1;
            var workflow = new Workflow
            {
                Nombre = "Resumen de Documento",
                Codigo = "ReporteClientes",
                Descripcion = "Resume el contenido de un documento recién indexado y genera un PDF. Flujo de ejemplo de la ETAPA 12.",
                Disparadores = "resumen del documento;resumir documento;resumen documento",
                Version = 1,
                Estado = EstadoWorkflow.Activo,
                FechaCreacion = DateTime.UtcNow,
                UsuarioCreacion = adminId,
                Pasos = new List<WorkflowPaso>
                {
                    new WorkflowPaso
                    {
                        Orden = 1,
                        Nombre = "Obtener contenido del documento",
                        Herramienta = "DocumentSearchTool",
                        Parametros = "{\"consulta\":\"Resumen del contenido\",\"documento\":\"{{Nombre}}\"}",
                        RequiereConfirmacion = false,
                        ReintentosMaximos = 2,
                        TiempoMaximoMs = 30000,
                        EstrategiaError = EstrategiaError.Cancelar
                    },
                    new WorkflowPaso
                    {
                        Orden = 2,
                        Nombre = "Generar resumen",
                        Herramienta = "ReportTool",
                        Parametros = "{\"datos\":\"{{resultado}}\",\"titulo\":\"Resumen: {{Nombre}}\"}",
                        RequiereConfirmacion = true,
                        ReintentosMaximos = 1,
                        TiempoMaximoMs = 30000,
                        EstrategiaError = EstrategiaError.Omitir
                    },
                    new WorkflowPaso
                    {
                        Orden = 3,
                        Nombre = "Presentar resultado",
                        Herramienta = "DateTimeTool",
                        Parametros = "{}",
                        RequiereConfirmacion = false,
                        ReintentosMaximos = 1,
                        TiempoMaximoMs = 15000,
                        EstrategiaError = EstrategiaError.Omitir
                    }
                }
            };
            await context.Workflows.AddAsync(workflow);
            await context.SaveChangesAsync();
        }

        // ===== ETAPA 21: memorias de ejemplo =====
        // Sin estos datos el guion del video y el escenario de aceptacion
        // ("genera el mismo reporte mensual que usamos el trimestre pasado")
        // no son reproducibles: la base recien creada nasce sin ninguna
        // memoria. Se siembran idempotentes (por Codigo) y con contenido real
        // para poder mostrar versionado, permisos y retencion en vivo.
        await SeedMemoriasEjemploAsync(context);

        // ===== ETAPA 13: Motor de Eventos Empresariales =====
        // Configuración global del motor
        if (!await context.ConfiguracionEventoMotor.AnyAsync())
        {
            await context.ConfiguracionEventoMotor.AddAsync(new ConfiguracionEventoMotor
            {
                IdConfiguracion = 1,
                ReintentosMaximos = 3,
                IntervaloReintentoMs = 2000,
                TiempoMaximoEventoMs = 120000,
                EventosSimultaneosMax = 5,
                FrecuenciaProcesadorMs = 2000,
                FechaActualizacion = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // Evento empresarial de ejemplo
        if (!await context.EventosEmpresariales.AnyAsync())
        {
            var adminId = (await context.Usuarios.FirstOrDefaultAsync(u => u.UsuarioNombre == "admin"))?.IdUsuario ?? 1;
            var evento = new EventoEmpresarial
            {
                Codigo = "DOC_PROCESADO",
                Nombre = "Documento Procesado",
                Descripcion = "Se dispara automáticamente cuando un documento termina de procesarse en el sistema (ETAPA 13).",
                Categoria = "Documento",
                Activo = true,
                FechaCreacion = DateTime.UtcNow,
                UsuarioCreacion = adminId
            };
            await context.EventosEmpresariales.AddAsync(evento);
            await context.SaveChangesAsync();

            // El resumen se dispara vía DOC_INDEXADO (vectores listos), no vía DOC_PROCESADO
            // (que llega antes de indexar y siempre encontraba la búsqueda vacía).
            var eventoIndexado = new EventoEmpresarial
            {
                Codigo = "DOC_INDEXADO",
                Nombre = "Documento Indexado",
                Descripcion = "Se dispara automáticamente cuando un documento queda con vectores en la base vectorial (RAG listo).",
                Categoria = "Documento",
                Activo = true,
                FechaCreacion = DateTime.UtcNow,
                UsuarioCreacion = adminId
            };
            await context.EventosEmpresariales.AddAsync(eventoIndexado);
            await context.SaveChangesAsync();

            // Regla que asocia DOC_INDEXADO al flujo "Resumen de Documento"
            var workflowReporte = await context.Workflows.FirstOrDefaultAsync(w => w.Codigo == "ReporteClientes");
            if (workflowReporte != null)
            {
                await context.ReglasEvento.AddAsync(new ReglaEvento
                {
                    IdEvento = eventoIndexado.IdEvento,
                    IdWorkflow = workflowReporte.IdWorkflow,
                    Condicion = "",
                    Prioridad = 1,
                    Activa = true
                });
                await context.SaveChangesAsync();
            }
        }

        // ===== ETAPA 14: Seguridad, Gobierno, Auditoría y Observabilidad =====

        // 1) Roles iniciales (Actividad 2)
        var rolesDefinidos = new[] { "Administrador", "Supervisor", "Usuario", "Operador" };
        foreach (var nombre in rolesDefinidos)
        {
            if (!await context.Roles.AnyAsync(r => r.Nombre == nombre))
            {
                await context.Roles.AddAsync(new Rol { Nombre = nombre, Descripcion = $"Rol {nombre}", Activo = true });
            }
        }
        await context.SaveChangesAsync();

        var rolAdmin = await context.Roles.FirstAsync(r => r.Nombre == "Administrador");
        var rolSupervisor = await context.Roles.FirstAsync(r => r.Nombre == "Supervisor");
        var rolUsuario = await context.Roles.FirstAsync(r => r.Nombre == "Usuario");
        var rolOperador = await context.Roles.FirstAsync(r => r.Nombre == "Operador");

        // 2) Permisos por módulo (Actividad 3).
        // Solo los que el código exige con VerificarPermisoAsync/TienePermisoAsync o el
        // mapeo herramienta→permiso. Los demás nunca se verificaban y se eliminaron.
        var permisosDefinidos = new (string Codigo, string Nombre, string Modulo)[]
        {
            ("FUENTES_ADMINISTRAR", "Administrar fuentes", "Fuentes"),
            ("ASISTENTES_ADMINISTRAR", "Asignar asistentes a usuarios", "Asistentes"),
            ("HERRAMIENTAS_CONSULTAR", "Consultar herramientas", "Herramientas"),
            ("SQL_CONSULTAR", "Consultar SQL", "SQL"),
            ("AUDITORIA_CONSULTAR", "Consultar auditoría", "Auditoria"),
            // Escritura en ámbitos compartidos de memoria (Organizacion, Agente,
            // Workflow, Conversacional). El ámbito Usuario propio sigue siendo
            // autoservicio: cada usuario escribe sus preferencias sin permiso.
            (MemoryManager.PermisoAdministrarMemoria, "Administrar memoria compartida", "Memoria")
        };

        var mapaPermisos = new Dictionary<string, Permiso>();
        foreach (var (codigo, nombre, modulo) in permisosDefinidos)
        {
            var permiso = await context.Permisos.FirstOrDefaultAsync(p => p.Codigo == codigo);
            if (permiso == null)
            {
                permiso = new Permiso { Codigo = codigo, Nombre = nombre, Modulo = modulo, Activo = true };
                await context.Permisos.AddAsync(permiso);
                await context.SaveChangesAsync();
            }
            mapaPermisos[codigo] = permiso;
        }

        // 3) Asignación de permisos por rol (mínimo privilegio)
        async Task AsignarAsync(Rol rol, params string[] codigos)
        {
            foreach (var c in codigos)
            {
                if (!mapaPermisos.TryGetValue(c, out var p)) continue;
                if (!await context.RolPermisos.AnyAsync(rp => rp.IdRol == rol.IdRol && rp.IdPermiso == p.IdPermiso))
                    await context.RolPermisos.AddAsync(new RolPermiso { IdRol = rol.IdRol, IdPermiso = p.IdPermiso });
            }
            await context.SaveChangesAsync();
        }

        // Solo siembra inicial: si ya hay asignaciones se respetan los cambios
        // hechos desde la matriz (no resucitar permisos desmarcados al reiniciar).
        if (!await context.RolPermisos.AnyAsync())
        {
            await AsignarAsync(rolAdmin, permisosDefinidos.Select(p => p.Codigo).ToArray());
            await AsignarAsync(rolSupervisor,
                "HERRAMIENTAS_CONSULTAR", "SQL_CONSULTAR", "AUDITORIA_CONSULTAR");
            await AsignarAsync(rolUsuario,
                "HERRAMIENTAS_CONSULTAR", "SQL_CONSULTAR");
            await AsignarAsync(rolOperador,
                "HERRAMIENTAS_CONSULTAR", "SQL_CONSULTAR");
        }

        // MEMORIA_ADMINISTRAR sí se otorga de forma idempotente, aunque ya haya
        // asignaciones: sin él, escribir en un ámbito compartido queda bloqueado
        // para TODO el mundo, incluido el administrador. El guard de arriba
        // ("solo siembra inicial") evita resucitar permisos que un administrador
        // desmarcó a propósito; este permiso no se puede desmarcar, porque su
        // ausencia deja el sistema sin quién pueda mantener la memoria común.
        await AsignarAsync(rolAdmin, MemoryManager.PermisoAdministrarMemoria);

        // 4) Políticas de IA por defecto (Actividad 12)
        if (!await context.PoliticasIA.AnyAsync())
        {
            var politicas = new[]
            {
                new PoliticaIA { Nombre = "Modelo permitido", Tipo = "ModeloPermitido", Valor = "deepseek-r1:7b", Activa = true },
                new PoliticaIA { Nombre = "Tamaño máximo de contexto", Tipo = "MaxContexto", Valor = "8192", Activa = true },
                new PoliticaIA { Nombre = "Máximo de resultados RAG", Tipo = "MaxResultadosRag", Valor = "5", Activa = true },
                new PoliticaIA { Nombre = "Herramientas permitidas", Tipo = "HerramientasPermitidas", Valor = "DocumentSearchTool,CalculatorTool,DateTimeTool", Activa = true },
                new PoliticaIA { Nombre = "Tiempo máximo de respuesta (ms)", Tipo = "TiempoMaxRespuesta", Valor = "120000", Activa = true },
                new PoliticaIA { Nombre = "Máximo de ejecuciones", Tipo = "MaxEjecuciones", Valor = "10", Activa = true },
                new PoliticaIA { Nombre = "Fuentes autorizadas", Tipo = "FuentesAutorizadas", Valor = "*", Activa = true }
            };
            await context.PoliticasIA.AddRangeAsync(politicas);
            await context.SaveChangesAsync();
        }

        // 5) Fuentes de conocimiento de ejemplo (ETAPA 15 - seed para BD nueva en Docker)
        if (!await context.FuentesConocimiento.AnyAsync())
        {
            var adminId = (await context.Usuarios.FirstOrDefaultAsync(u => u.UsuarioNombre == "admin"))?.IdUsuario ?? 1;
            var fuentes = new[]
            {
                new FuenteConocimiento { Nombre = "Manuales de Usuario", Codigo = "MANUALES_USUARIO", Descripcion = "Manuales dirigidos a usuarios finales.", Tipo = TipoFuente.Manual, Activo = true, Prioridad = 5, UsuarioCreacion = adminId },
                new FuenteConocimiento { Nombre = "Documentación Técnica", Codigo = "DOC_TECNICA", Descripcion = "Documentación técnica de sistemas.", Tipo = TipoFuente.Manual, Activo = true, Prioridad = 4, UsuarioCreacion = adminId },
                new FuenteConocimiento { Nombre = "Procedimientos Operativos", Codigo = "PROCEDIMIENTOS", Descripcion = "Guías de procedimientos operativos.", Tipo = TipoFuente.Manual, Activo = true, Prioridad = 3, UsuarioCreacion = adminId },
                new FuenteConocimiento { Nombre = "Políticas y Normativas", Codigo = "POLITICAS", Descripcion = "Políticas y normativas empresariales.", Tipo = TipoFuente.Manual, Activo = true, Prioridad = 2, UsuarioCreacion = adminId },
                new FuenteConocimiento { Nombre = "Base de Datos Empresarial", Codigo = "SQL_EMPRESARIAL", Descripcion = "Fuente SQL autorizada para consultas.", Tipo = TipoFuente.Manual, Activo = true, Prioridad = 1, UsuarioCreacion = adminId }
            };
            await context.FuentesConocimiento.AddRangeAsync(fuentes);
            await context.SaveChangesAsync();
        }

        // 6) Asistentes de ejemplo (ETAPA 15 - seed para BD nueva en Docker)
        if (!await context.Asistentes.AnyAsync())
        {
            var asistentes = new Asistente.Domain.Entities.Asistente[]
            {
                new Asistente.Domain.Entities.Asistente
                {
                    Nombre = "Asistente General",
                    Descripcion = "Asistente empresarial general para consultas de usuarios.",
                    ModeloIA = "deepseek-r1:7b",
                    Activo = true,
                    Idioma = "es",
                    NivelFormalidad = "profesional",
                    FormatoRespuesta = "texto",
                    Temperatura = 0.3,
                    MaxTokens = 8192,
                    TimeoutSegundos = 600,
                    MensajeBienvenida = "Hola, soy el Asistente General. ¿En qué puedo ayudarte hoy?"
                },
                new Asistente.Domain.Entities.Asistente
                {
                    Nombre = "Asistente de Soporte Técnico",
                    Descripcion = "Resuelve dudas técnicas y de procedimientos.",
                    ModeloIA = "deepseek-r1:7b",
                    Activo = true,
                    Idioma = "es",
                    NivelFormalidad = "profesional",
                    FormatoRespuesta = "texto",
                    Temperatura = 0.2,
                    MaxTokens = 8192,
                    TimeoutSegundos = 600,
                    MensajeBienvenida = "Bienvenido al soporte técnico. Describe tu incidencia."
                },
                new Asistente.Domain.Entities.Asistente
                {
                    Nombre = "Asistente de RRHH",
                    Descripcion = "Atiende consultas de recursos humanos y onboarding.",
                    ModeloIA = "deepseek-r1:7b",
                    Activo = true,
                    Idioma = "es",
                    NivelFormalidad = "profesional",
                    FormatoRespuesta = "texto",
                    Temperatura = 0.3,
                    MaxTokens = 8192,
                    TimeoutSegundos = 600,
                    MensajeBienvenida = "Hola, soy el Asistente de RRHH. ¿En qué puedo ayudarte?"
                }
            };
            await context.Asistentes.AddRangeAsync(asistentes);
            await context.SaveChangesAsync();
        }

        // Vincular CADA asistente con TODAS las fuentes activas (control de
        // conocimiento). Idempotente y fuera del bloque de creacion: antes solo
        // se hacia con los asistentes recien creados (context.Asistentes.Local),
        // asi que el Asistente General se quedo con 2 de las 5 fuentes y su
        // busqueda documental no veia mas que una parte del material. Un
        // asistente sin fuentes asignadas es un agente mudo.
        var todasFuentes = await context.FuentesConocimiento.Where(f => f.Activo).ToListAsync();
        foreach (var a in await context.Asistentes.ToListAsync())
        {
            foreach (var f in todasFuentes)
            {
                if (!await context.AsistentesFuentes.AnyAsync(af => af.IdAsistente == a.IdAsistente && af.IdFuente == f.IdFuente))
                    await context.AsistentesFuentes.AddAsync(new AsistenteFuente { IdAsistente = a.IdAsistente, IdFuente = f.IdFuente, Activo = true });
            }
        }
        await context.SaveChangesAsync();

        // 7) Asignación de asistentes y fuentes al administrador (control de acceso)
        var adminUsuario = await context.Usuarios.FirstOrDefaultAsync(u => u.UsuarioNombre == "admin");
        if (adminUsuario != null)
        {
            var todosAsistentes = await context.Asistentes.ToListAsync();
            foreach (var a in todosAsistentes)
                if (!await context.UsuarioAsistentes.AnyAsync(ua => ua.IdUsuario == adminUsuario.IdUsuario && ua.IdAsistente == a.IdAsistente))
                    await context.UsuarioAsistentes.AddAsync(new UsuarioAsistente { IdUsuario = adminUsuario.IdUsuario, IdAsistente = a.IdAsistente, Activo = true });

            var todasFuentesAdmin = await context.FuentesConocimiento.ToListAsync();
            foreach (var f in todasFuentesAdmin)
                if (!await context.UsuarioFuentes.AnyAsync(uf => uf.IdUsuario == adminUsuario.IdUsuario && uf.IdFuente == f.IdFuente))
                    await context.UsuarioFuentes.AddAsync(new UsuarioFuente { IdUsuario = adminUsuario.IdUsuario, IdFuente = f.IdFuente, Activo = true });

            await context.SaveChangesAsync();
        }

        await CifrarContenidoPreexistenteAsync(context);
    }

    /// <summary>
    /// Cifra el contenido de memoria escrito ANTES de que existiera el cifrado.
    ///
    /// El conversor de valor del modelo cifra al escribir y descifra al leer, pero no
    /// puede hacer nada con las filas que ya estaban en la base en claro: el
    /// descifrador las devuelve tal cual (las que no llevan prefijo las trata como
    /// previas al cifrado) y se quedarían en claro indefinidamente. Este pasito las
    /// reescribe para que también pasen por el cifrado.
    ///
    /// Dos detalles que no son opcionales:
    ///
    /// - La detección va por SQL crudo. Un LINQ sobre Contenido no sirve porque EF
    ///   aplica el MISMO conversor al argumento de la consulta que a la columna: el
    ///   predicado viaja cifrado, es decir compara enc:v1:% contra enc(v1:...) y no
    ///   puede casar nunca. Con el NOT de la detección eso no queda en cero filas,
    ///   queda en TODAS: se re-cifraría la memoria entera en cada arranque.
    ///   Solo se buscan los Id por SQL y luego se cargan por Id.
    ///
    /// - Ese SQL crudo tiene que llevar {0} y el valor por parámetro. Con un string
    ///   interpolado EF deja el marcador DENTRO de las comillas, el SQL acaba siendo
    ///   NOT LIKE '@p0%' (texto literal que no casa con nada) y se vuelve al mismo
    ///   fallo: todo seleccionado, todo reescrito. El interpolado también dispara el
    ///   aviso EF1002 de inyección SQL, y por una razón: efectivamente no parametriza.
    ///
    /// - Se marca la propiedad como modificada y se deja que guarde el conversor.
    ///   Escribir el texto a mano saltaría el mismo punto único que se instaló
    ///   justamente para que no se pueda olvidar ninguna escritura.
    ///
    /// Idempotente: al volver a arrancar no queda ninguna fila sin prefijo y no
    /// escribe nada (la detección devuelve cero y sale antes de tocar nada). Un
    /// fallo aquí corta el arranque, igual que el resto de la verificación:
    /// preferible a levantarse con datos en claro.
    /// </summary>
    internal static async Task CifrarContenidoPreexistenteAsync(AsistenteDbContext context)
    {
        var prefijo = MemoryCifrador.Prefijo;
        // SqlQueryRaw con {0}, no interpolacion: un "$...{prefijo}..." con SqlQuery
        // no parametriza. EF deja el marcador DENTRO de las comillas y el SQL acaba
        // siendo NOT LIKE '@p0%' (texto literal que no casa con nada), asi que NOT
        // LIKE vale para TODA la fila y en cada arranque se reescribe la memoria
        // entera: se cifra de nuevo y el contenido cambia de cadena cada boot.
        // Con {0} el valor viaja como parametro de verdad (y sin el aviso EF1002).
        var patrones = prefijo + "%";
        var memorias = await context.Database.SqlQueryRaw<int>(
                "SELECT IdMemory AS [Value] FROM [MemoryRecords] WHERE [Contenido] NOT LIKE {0}", patrones)
            .ToListAsync();
        var versiones = await context.Database.SqlQueryRaw<int>(
                "SELECT IdVersion AS [Value] FROM [MemoryVersions] WHERE [Contenido] NOT LIKE {0}", patrones)
            .ToListAsync();

        if (memorias.Count == 0 && versiones.Count == 0) return;

        foreach (var memoria in await context.MemoryRecords
                     .Where(m => memorias.Contains(m.IdMemory)).ToListAsync())
        {
            context.Entry(memoria).Property(m => m.Contenido).IsModified = true;
        }

        foreach (var version in await context.MemoryVersions
                     .Where(v => versiones.Contains(v.IdVersion)).ToListAsync())
        {
            context.Entry(version).Property(v => v.Contenido).IsModified = true;
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Comprueba que toda tabla que declara el modelo exista realmente en la base.
    ///
    /// Existe por un motivo concreto: EF decide qué migraciones aplicar comparando
    /// el MODELO contra el SNAPSHOT, nunca contra la base real. Si una migración
    /// crea una tabla con un nombre distinto al que su propio snapshot registra
    /// (pasó con 20260810162521_Etapa14_SeguridadGobernanza, que creó
    /// 'ConfiguracionRAG' mientras el modelo dice 'ConfiguracionesRAG'), EF ve que
    /// modelo y esquema coinciden, no genera rename y MigrateAsync no tiene nada que
    /// aplicar. La deriva queda oculta hasta que un endpoint revienta con
    /// "Invalid object name": en este proyecto, dos meses.
    ///
    /// Esta verificación la delata en el arranque, con el nombre de la tabla, en
    /// lugar de dejar que se manifieste como un 500 por petición. Ante
    /// desalineación corta el arranque: un contenedor sirviendo errores es peor que
    /// uno que reinicia y reintenta (el compose usa restart: always).
    /// </summary>
    private static async Task VerificarEsquemaAsync(AsistenteDbContext context)
    {
        // GetTableName() es null en los tipos mapeados a vista, así que este filtro
        // ya los deja fuera.
        var declaradas = context.Model.GetEntityTypes()
            .Where(e => e.GetTableName() != null)
            .Select(e => new
            {
                Tabla = e.GetTableName(),
                Entidad = e.ClrType?.Name
            })
            .Where(t => !string.IsNullOrWhiteSpace(t.Tabla))
            .ToList();

        if (declaradas.Count == 0) return;

        var existentes = await context.Database
            .SqlQueryRaw<string>("SELECT TABLE_NAME AS Value FROM INFORMATION_SCHEMA.TABLES")
            .ToListAsync();

        var faltantes = declaradas
            .Where(d => !existentes.Contains(d.Tabla!, StringComparer.OrdinalIgnoreCase))
            .GroupBy(d => d.Tabla, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Key} (entidad {g.First().Entidad})")
            .ToList();

        if (faltantes.Count == 0) return;

            throw new InvalidOperationException(
                "El esquema de la base de datos no coincide con el modelo: " +
                $"{faltantes.Count} tabla(s) declarada(s) por EF no existen. " +
                $"Faltan: {string.Join(", ", faltantes)}. " +
                "Suele indicar una migración cuyo SQL contradice su propio snapshot; " +
                "revisa la última migración aplicada y crea la que corrija el nombre.");
    }

    /// <summary>
    /// Memorias de ejemplo de la ETAPA 21. Cubren los cinco ambitos (Usuario,
    /// Agente, Organizacion, Workflow y, de forma indirecta, el conversacional
    /// que se crea en cada chat) y los tres casos de uso de la especificacion:
    /// formato de reporte reutilizable (Caso 1), definicion institucional para
    /// el agente (Caso 2) y estado persistente de workflow (Caso 3).
    ///
    /// SECRETA nace con RequierePermiso: nadie la lee sin el permiso
    /// MEMORIA:SECRETA, ni siquiera el administrador. Es la memoria que hace
    /// visible el 403 del guion del video.
    ///
    /// Idempotente por Codigo: si la base ya tiene la memoria, no se toca.
    /// El contenido se cifra solo: lo hace el conversor de valor de EF Core.
    /// </summary>
    private static async Task SeedMemoriasEjemploAsync(AsistenteDbContext context)
    {
        var scopes = await context.MemoryScopes.ToDictionaryAsync(s => s.Codigo, s => s.IdScope);
        var categorias = await context.MemoryCategories.ToDictionaryAsync(c => c.Codigo, c => c.IdCategory);
        if (scopes.Count == 0 || categorias.Count == 0) return;

        // Los duenos de memoria con ambito con dueño (Usuario, Agente, Workflow)
        // tienen que existir: si no estan, esa memoria no se siembra.
        var adminId = (await context.Usuarios.FirstOrDefaultAsync(u => u.UsuarioNombre == "admin"))?.IdUsuario;
        var asistenteId = (await context.Asistentes.FirstOrDefaultAsync(a => a.Nombre == "Asistente General"))?.IdAsistente;
        var workflowId = (await context.Workflows.FirstOrDefaultAsync(w => w.Codigo == "ReporteClientes"))?.IdWorkflow;

        var ejemplos = new (string Codigo, string Titulo, string Ambito, string Categoria, bool RequierePermiso,
                            int? Propietario, string Contenido)[]
        {
            ("FORM-REPORTE-MENSUAL", "Formato de reporte mensual", "Organizacion", "FormatoReporte", false, null,
             "FORMATO OFICIAL DEL REPORTE MENSUAL (vigente desde el trimestre anterior)\n" +
             "1. Resumen ejecutivo: cinco lineas, impacto y conclusion.\n" +
             "2. Indicadores clave: tabla con indicador, meta, real y variacion.\n" +
             "3. Avances del mes: una linea por area, con responsable.\n" +
             "4. Incidencias y riesgos: causa, impacto y mitigacion.\n" +
             "5. Cierre y proximos pasos: tres commitments con fecha.\n" +
             "Normas: una pagina por seccion, cifras con unidad, tono ejecutivo, sin anexos."),

            ("SECRETA", "Credencial de la API corporativa (restringida)", "Organizacion", "Definicion", true, null,
             "DATO RESTRINGIDO DE DEMOSTRACION. Token ficticio: sk-demo-0000-no-usar.\n" +
             "Solo se lee con el permiso MEMORIA:SECRETA. Sin ese permiso, la respuesta es 403 " +
             "y el contenido no se expone ni en el error."),

            ("DEF-VIATICOS", "Definicion institucional de viaticos", "Organizacion", "Definicion", false, null,
             "Viatico diario: 35 USD. Requiere factura y aval de viagem.\n" +
             "Liquidacion a 15 dias desde la aprobacion.\n" +
             "Categoria contable: gasto operacional. Si el viaje supera 3 dias, la tarifa sube a 50 USD.\n" +
             "El agente comercial debe aplicar esta definicion, no la suya."),

            ("PROC-REPORTE-COMERCIAL", "Procedimiento del reporte comercial mensual", "Organizacion", "Procedimiento", false, null,
             "1. Descargar las ventas del CRM del mes en curso.\n" +
             "2. Contrastar cada indicador contra el presupuesto del formato institucional vigente.\n" +
             "3. Comentar toda variacion superior al 10 por ciento.\n" +
             "4. Cerrar con el forecast del mes siguiente.\n" +
             "Se usa siempre el formato guardado en memoria institucional, no una plantilla nueva."),

            ("PREF-TONO-DIRECTO", "Preferencia de tono del administrador", "Usuario", "Preferencia", false, adminId,
             "Responder en espanol, frases cortas, sin emojis. Resumir primero y luego detallar. " +
             "Si falta respaldo documental, decirlo en vez de rellenar."),

            ("CONOCIMIENTO-ASISTENTE-GENERAL", "Conocimiento propio del Asistente General", "Agente", "Hecho", false, asistenteId,
             "El Asistente General prioriza la documentacion indexada antes de responder, " +
             "avisa cuando no encuentra respaldo y consulta la memoria institucional para no repetir trabajo ya hecho."),

            ("ESTADO-REPORTE-CLIENTES", "Estado del flujo ReporteClientes", "Workflow", "Hecho", false, workflowId,
             "Ultimo paso completado: 2 de 3 (consolidacion de ventas).\n" +
             "Pendiente: seccion de incidencias.\n" +
             "Al retomar el flujo se retoma desde el paso 3 con este estado.")
        };

        var nuevas = new List<MemoryRecord>();
        foreach (var (codigo, titulo, ambito, categoria, requierePermiso, propietario, contenido) in ejemplos)
        {
            if (await context.MemoryRecords.AnyAsync(m => m.Codigo == codigo)) continue;
            if (!scopes.TryGetValue(ambito, out var idScope)) continue;
            if (!categorias.TryGetValue(categoria, out var idCategory)) continue;
            // Ambito con dueno sin dueno: no se siembra (quedaria invisible para todos).
            if (ambito is "Usuario" or "Agente" or "Workflow" && !propietario.HasValue) continue;

            var memoria = new MemoryRecord
            {
                Codigo = codigo,
                Titulo = titulo,
                Contenido = contenido,
                IdScope = idScope,
                IdCategory = idCategory,
                IdPropietario = propietario,
                Activo = true,
                VersionActual = 1,
                RequierePermiso = requierePermiso,
                Indexada = false,
                FechaCreacion = DateTime.UtcNow,
                FechaActualizacion = DateTime.UtcNow
            };
            nuevas.Add(memoria);
        }

        if (nuevas.Count == 0) return;

        await context.MemoryRecords.AddRangeAsync(nuevas);
        await context.SaveChangesAsync();

        // Version 1 inmutable de cada memoria sembrada: el versionado empieza en
        // la v1, igual que en el alta desde la interfaz.
        var versiones = nuevas.Select(m => new MemoryVersion
        {
            IdMemory = m.IdMemory,
            Numero = 1,
            Contenido = m.Contenido,
            Motivo = "Seed inicial (memoria de ejemplo de la ETAPA 21)",
            Fecha = DateTime.UtcNow
        }).ToList();

        await context.MemoryVersions.AddRangeAsync(versiones);
        await context.SaveChangesAsync();
    }
}
