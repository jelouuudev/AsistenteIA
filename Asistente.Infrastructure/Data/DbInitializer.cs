using System;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
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
                }
            };

            await context.Herramientas.AddRangeAsync(herramientas);
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
            ("AUDITORIA_CONSULTAR", "Consultar auditoría", "Auditoria")
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

            // Vincular cada asistente con todas las fuentes (control de conocimiento)
            var todasFuentes = await context.FuentesConocimiento.ToListAsync();
            foreach (var a in context.Asistentes.Local)
            {
                foreach (var f in todasFuentes)
                {
                    if (!await context.AsistentesFuentes.AnyAsync(af => af.IdAsistente == a.IdAsistente && af.IdFuente == f.IdFuente))
                        await context.AsistentesFuentes.AddAsync(new AsistenteFuente { IdAsistente = a.IdAsistente, IdFuente = f.IdFuente, Activo = true });
                }
            }
            await context.SaveChangesAsync();
        }

        // 7) Asignación de asistentes y fuentes al administrador (control de acceso)
        var adminUsuario = await context.Usuarios.FirstOrDefaultAsync(u => u.UsuarioNombre == "admin");
        if (adminUsuario != null)
        {
            var todosAsistentes = await context.Asistentes.ToListAsync();
            foreach (var a in todosAsistentes)
                if (!await context.UsuarioAsistentes.AnyAsync(ua => ua.IdUsuario == adminUsuario.IdUsuario && ua.IdAsistente == a.IdAsistente))
                    await context.UsuarioAsistentes.AddAsync(new UsuarioAsistente { IdUsuario = adminUsuario.IdUsuario, IdAsistente = a.IdAsistente, Activo = true });

            var todasFuentes = await context.FuentesConocimiento.ToListAsync();
            foreach (var f in todasFuentes)
                if (!await context.UsuarioFuentes.AnyAsync(uf => uf.IdUsuario == adminUsuario.IdUsuario && uf.IdFuente == f.IdFuente))
                    await context.UsuarioFuentes.AddAsync(new UsuarioFuente { IdUsuario = adminUsuario.IdUsuario, IdFuente = f.IdFuente, Activo = true });

            await context.SaveChangesAsync();
        }
    }
}
