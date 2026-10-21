using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
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
/// Editar y crear agentes desde el Web.
///
/// El fallo: el formulario de edicion exigia el campo Codigo (string no-nullable
/// =&gt; ASP.NET lo da por requerido) y los agentes del seed lo tienen VACIO.
/// El POST caia en ModelState invalido, devolvia la vista SIN los catalogos
/// (las secciones de asignaciones se veian sin ninguna casilla, como si se
/// hubieran borrado) y el guardado no se aplicaba nunca.
///
/// Aqui se fija: el codigo es opcional, el guardado llega a la API con las
/// asignaciones que trae el formulario, y si el modelo es invalido se recarga
/// el catalogo para que la pantalla siga mostrando las casillas.
/// </summary>
public class AsistentesControllerEdicionTests
{
    private static AsistentesController Controller(IApiService api, int idUsuario = 7)
    {
        var controller = new AsistentesController(api);
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

    private sealed class ProveedorSinAlmacen : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    /// <summary>
    /// El codigo es opcional: la base lo permite vacio (no hay indice unico y
    /// el seed no lo pone) y exigirlo hacia el formulario imposible de guardar.
    /// </summary>
    [Fact]
    public void ElCodigoDelAgente_NoEsRequerido()
    {
        // Ni [Required] explicito ni tipo no-nullable: ASP.NET dava por
        // obligatorio todo string no-nullable, y con el codigo vacio del seed
        // el POST de edicion caia siempre en ModelState invalido.
        var prop = typeof(ActualizarAsistenteRequest).GetProperty(nameof(ActualizarAsistenteRequest.Codigo));
        Assert.NotNull(prop);
        Assert.Null(prop!.GetCustomAttribute<RequiredAttribute>());

        var propCrear = typeof(CrearAsistenteRequest).GetProperty(nameof(CrearAsistenteRequest.Codigo));
        Assert.NotNull(propCrear);
        Assert.Null(propCrear!.GetCustomAttribute<RequiredAttribute>());

        // Y el request admite un codigo nulo (la columna es NOT NULL: el
        // servicio lo guarda como cadena vacia).
        var request = new ActualizarAsistenteRequest { Codigo = null, Nombre = "Agente" };
        Assert.Null(request.Codigo);
    }

    /// <summary>El guardado del formulario llega a la API con sus asignaciones.</summary>
    [Fact]
    public async Task Editar_LlegaALaApiConLasAsignacionesDelFormulario()
    {
        ActualizarAsistenteRequest? enviada = null;
        var api = new Mock<IApiService>();
        api.Setup(a => a.ActualizarAsistenteAsync(It.IsAny<int>(), It.IsAny<ActualizarAsistenteRequest>(), It.IsAny<int>(), It.IsAny<string>()))
            .Callback<int, ActualizarAsistenteRequest, int, string>((_, r, _, _) => enviada = r)
            .ReturnsAsync(new AsistenteDto { IdAsistente = 1, Nombre = "Asistente General" });

        await Controller(api.Object).Editar(1, new ActualizarAsistenteRequest
        {
            Codigo = null,               // el agente del seed no tiene codigo
            Nombre = "Asistente General",
            ModeloIA = "deepseek-r1:7b",
            Fuentes = new List<int> { 1, 2, 3 },
            Herramientas = new List<int> { 1, 2, 6 }
        });

        Assert.NotNull(enviada);
        Assert.Equal(new[] { 1, 2, 3 }, enviada!.Fuentes.ToArray());
        Assert.Equal(new[] { 1, 2, 6 }, enviada!.Herramientas.ToArray());
    }

    /// <summary>
    /// Si el modelo no es valido, la vista vuelve con los catalogos cargados:
    /// sin ellos, las secciones de asignaciones salen sin casillas y parece que
    /// se han borrado.
    /// </summary>
    [Fact]
    public async Task Editar_ConModeloInvalido_RebocaLaVistaConLosCatalogos()
    {
        var api = new Mock<IApiService>();
        api.Setup(a => a.GetFuentesConocimientoAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<FuenteConocimientoDto>());
        api.Setup(a => a.GetHerramientasAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<HerramientaDto>());
        api.Setup(a => a.GetWorkflowsAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<WorkflowDto>());
        api.Setup(a => a.GetRolesAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<RolDto>());
        api.Setup(a => a.GetUsuariosAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<UsuarioDto>());

        var controller = Controller(api.Object);
        // Nombre vacio: el nombre del agente si es obligatorio.
        controller.ModelState.AddModelError("Nombre", "obligatorio");

        var resultado = await controller.Editar(1, new ActualizarAsistenteRequest
        {
            Codigo = null,
            Nombre = "",
            ModeloIA = "deepseek-r1:7b"
        });

        var vista = Assert.IsType<ViewResult>(resultado);
        Assert.NotNull(vista.ViewData["Herramientas"]);
        Assert.NotNull(vista.ViewData["Fuentes"]);
        api.Verify(a => a.ActualizarAsistenteAsync(It.IsAny<int>(), It.IsAny<ActualizarAsistenteRequest>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }
}
