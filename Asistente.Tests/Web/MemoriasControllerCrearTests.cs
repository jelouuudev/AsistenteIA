using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Asistente.Shared;
using Asistente.Web.Controllers;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Asistente.Tests.Web;

/// <summary>
/// Creación de memorias desde el formulario Web (ETAPA 21).
///
/// El formulario NO ofrece la casilla "Requiere permiso MEMORIA:{código}": ese
/// permiso por código no tiene dónde concederse (el catálogo de Seguridad solo
/// admite permisos existentes y no hay forma de crear uno nuevo), así que una
/// memoria creada con esa exigencia quedaba invisible e indestructible para
/// todos, incluido el administrador.
///
/// Lo que gobierna el acceso es el ámbito: Usuario, Agente y Workflow solo la
/// ve su dueño, Organizacion es compartida, y escribir en los ámbitos
/// compartidos exige el permiso MEMORIA_ADMINISTRAR.
///
/// La API sí conserva la capacidad de crear una memoria reservada (para uso
/// programático, donde el permiso se ha concedido antes por otra vía); por eso
/// el Web fuerza el valor en vez de simplemente no enviarlo: si no lo enviara,
/// la API lo protegería por defecto y la memoria volvería a quedar reservada.
/// </summary>
public class MemoriasControllerCrearTests
{
    private static MemoriasController Controller(IApiService api, int idUsuario = 7)
    {
        var controller = new MemoriasController(api, NullLogger<MemoriasController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()) }, "Test"))
            }
        };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new ProveedorSinAlmacen());
        return controller;
    }

    /// <summary>TempData que no guarda nada: en el test solo se lee lo que la acción escribe.</summary>
    private sealed class ProveedorSinAlmacen : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static Mock<IApiService> ApiQueCaptura(out Func<CrearMemoryRequest?> captura)
    {
        var api = new Mock<IApiService>();
        CrearMemoryRequest? enviada = null;
        api.Setup(a => a.CrearMemoriaAsync(It.IsAny<CrearMemoryRequest>(), It.IsAny<int>(), It.IsAny<string>()))
            .Callback<CrearMemoryRequest, int, string>((r, _, _) => enviada = r)
            .ReturnsAsync(new MemoryDto { Codigo = "MEM-NUEVA", VersionActual = 1 });
        captura = () => enviada;
        return api;
    }

    /// <summary>Lo que se crea desde el formulario se lee sin autorización especial.</summary>
    [Fact]
    public async Task Crear_DesdeElFormulario_LlegaAbiertaALaApi()
    {
        var api = ApiQueCaptura(out var captura);

        await Controller(api.Object).Crear(new CrearMemoryRequest
        {
            Codigo = "MEM-NUEVA",
            Titulo = "Nueva",
            Contenido = "contenido",
            Ambito = "Organizacion"
        });

        Assert.NotNull(captura());
        Assert.False(captura()!.RequierePermiso);
    }

    /// <summary>
    /// Aunque alguien manipule el POST pidiendo la reserva, la UI no la concede:
    /// una memoria que exige un permiso sin via para obtenerlo no debe poder
    /// crearse desde aquí.
    /// </summary>
    [Fact]
    public async Task Crear_DesdeElFormulario_NoDejaReservarLaMemoria()
    {
        var api = ApiQueCaptura(out var captura);

        await Controller(api.Object).Crear(new CrearMemoryRequest
        {
            Codigo = "MEM-NUEVA",
            Titulo = "Nueva",
            Contenido = "contenido",
            Ambito = "Organizacion",
            RequierePermiso = true
        });

        Assert.NotNull(captura());
        Assert.False(captura()!.RequierePermiso);
    }
}
