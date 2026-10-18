using System.Text;
using Asistente.API.Middleware;
using Asistente.Application;
using Asistente.Infrastructure;
using Asistente.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// QuestPDF exige configurar licencia (Community = gratuita) o lanza excepción al generar.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "AsistenteIA")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = false;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Asistente Inteligente Empresarial API",
        Version = "v1",
        Description = "API para el Asistente Inteligente Empresarial basado en IA Local con Ollama",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Asistente IA Team",
            Email = "support@asistenteia.com"
        }
    });

    // Incluir comentarios XML
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // JWT para Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Ingrese su token JWT"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Migraciones + verificación de esquema + seed, antes de aceptar tráfico.
// DbInitializer aplica las migraciones pendientes (MigrateAsync) y comprueba que
// toda tabla que declara el modelo exista de verdad.
//
// Esto NO se traga el error a propósito. Antes sí lo hacía, y por eso una base
// desalineada (ConfiguracionesRAG, dos meses) arrancaba igual y solo se
// manifiestaba como un 500 en cada petición de chat: un contenedor que sirve
// errores es peor que uno que no arranca. Con restart: always de docker-compose el
// contenedor reintenta solo en cuanto la causa desaparece.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await Asistente.Infrastructure.Data.DbInitializer.SeedAsync(services);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex,
            "No se pudieron aplicar las migraciones, verificar el esquema o sembrar la base de datos. " +
            "La aplicación no continúa con un esquema inconsistente.");
        throw;
    }
}

// Swagger solo en Development por defecto. Para el stack local de demo y el
// video de aceptación se puede encender con Swagger__Habilitado=true (ver
// docker-compose.yml): los endpoints siguen exigiendo JWT igual.
var swaggerHabilitado = builder.Configuration.GetValue<bool>("Swagger:Habilitado", false);
if (app.Environment.IsDevelopment() || swaggerHabilitado)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors("PermitirTodo");
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RateLimitingMiddleware>();
app.MapControllers();

try
{
    Log.Information("Iniciando aplicación AsistenteIA");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar");
}
finally
{
    Log.CloseAndFlush();
}
