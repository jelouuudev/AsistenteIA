using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// Asignación de asistentes y fuentes a un usuario (Seguridad).
///
/// El fallo que se repite: el formulario de estas pantallas generaba los
/// checkboxes con Html.CheckBoxFor(m => m[i].Autorizado), que emite los campos
/// como "[0].Autorizado" (sin prefijo). La acción recibe
/// List&lt;AsistenteAutorizadoDto&gt; items, y el binder solo reconoce lo que
/// llega con el prefijo "items[...]": el binding devolvía null y la acción
/// aplicaba "borrar todo y reasignar" con una lista vacía. Resultado: al
/// guardar desde la Web se perdían TODAS las asignaciones de asistentes (y de
/// fuentes) del usuario.
///
/// Aquí se fija el contrato: la lista llega con prefijo, y si no llega no se
/// toca nada.
/// </summary>
public class SeguridadAsignacionesTests
{
    private static SeguridadController Controller(IApiService api, int idUsuario = 7)
    {
        var controller = new SeguridadController(api);
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
    /// Sin lista recibida no se llama a la API. La API borra y reasigna: con
    /// una lista vacía, un fallo de binding se convertía en un borrado de todas
    /// las asignaciones del usuario.
    /// </summary>
    [Fact]
    public async Task AsignarAsistentes_SinListaNoLlamaALaApi()
    {
        var api = new Mock<IApiService>();
        api.Setup(a => a.AsignarAsistentesUsuarioAsync(It.IsAny<int>(), It.IsAny<AsignarAsistentesUsuarioRequest>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await Controller(api.Object).AsignarAsistentes(1, null!);

        api.Verify(a => a.AsignarAsistentesUsuarioAsync(It.IsAny<int>(), It.IsAny<AsignarAsistentesUsuarioRequest>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AsignarFuentes_SinListaNoLlamaALaApi()
    {
        var api = new Mock<IApiService>();
        api.Setup(a => a.AsignarFuentesUsuarioAsync(It.IsAny<int>(), It.IsAny<AsignarFuentesUsuarioRequest>(), It.IsAny<int>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await Controller(api.Object).AsignarFuentes(1, null!);

        api.Verify(a => a.AsignarFuentesUsuarioAsync(It.IsAny<int>(), It.IsAny<AsignarFuentesUsuarioRequest>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>Lo marcado llega a la API; lo desmarcado, no.</summary>
    [Fact]
    public async Task AsignarAsistentes_EnviaSoloLosMarcados()
    {
        AsignarAsistentesUsuarioRequest? enviada = null;
        var api = new Mock<IApiService>();
        api.Setup(a => a.AsignarAsistentesUsuarioAsync(It.IsAny<int>(), It.IsAny<AsignarAsistentesUsuarioRequest>(), It.IsAny<int>(), It.IsAny<string>()))
            .Callback<int, AsignarAsistentesUsuarioRequest, int, string>((_, r, _, _) => enviada = r)
            .Returns(Task.CompletedTask);

        await Controller(api.Object).AsignarAsistentes(1, new List<AsistenteAutorizadoDto>
        {
            new AsistenteAutorizadoDto { IdAsistente = 1, Autorizado = true },
            new AsistenteAutorizadoDto { IdAsistente = 2, Autorizado = false },
            new AsistenteAutorizadoDto { IdAsistente = 3, Autorizado = true }
        });

        Assert.NotNull(enviada);
        Assert.Equal(new[] { 1, 3 }, enviada!.IdsAsistentes.ToArray());
    }

    [Fact]
    public async Task AsignarFuentes_EnviaSoloLasMarcadas()
    {
        AsignarFuentesUsuarioRequest? enviada = null;
        var api = new Mock<IApiService>();
        api.Setup(a => a.AsignarFuentesUsuarioAsync(It.IsAny<int>(), It.IsAny<AsignarFuentesUsuarioRequest>(), It.IsAny<int>(), It.IsAny<string>()))
            .Callback<int, AsignarFuentesUsuarioRequest, int, string>((_, r, _, _) => enviada = r)
            .Returns(Task.CompletedTask);

        await Controller(api.Object).AsignarFuentes(1, new List<FuenteAutorizadaDto>
        {
            new FuenteAutorizadaDto { IdFuente = 1, Autorizada = true },
            new FuenteAutorizadaDto { IdFuente = 2, Autorizada = false }
        });

        Assert.NotNull(enviada);
        Assert.Equal(new[] { 1 }, enviada!.IdsFuentes.ToArray());
    }

    /// <summary>
    /// El binding depende del nombre de los campos. Si alguien vuelve a poner
    /// Html.CheckBoxFor(m => m[i].X) en estas vistas, los campos salen como
    /// "[0].X" y el POST deja de traer la lista: se pierde el binding y con el
    /// se lleva por delante las asignaciones del usuario. Se fija el contrato
    /// de las vistas.
    /// </summary>
    [Theory]
    [InlineData("AsignarAsistentes.cshtml", "items[@i].Autorizado")]
    [InlineData("AsignarFuentes.cshtml", "items[@i].Autorizada")]
    public void LaVista_EmiteLosCamposConElPrefijoDeLaAccion(string vista, string prefijoEsperado)
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Asistente.Web", "Views", "Seguridad", vista);
        ruta = Path.GetFullPath(ruta);
        Assert.True(File.Exists(ruta), $"No se encuentra la vista {vista} en {ruta}");

        var html = File.ReadAllText(ruta);

        Assert.Contains(prefijoEsperado, html);
        // Ningún campo puede quedar sin prefijo: "[0].Autorizado" no lo encuentra
        // el binder de List<T> items.
        Assert.DoesNotContain("name=\"[0]", html);
        Assert.DoesNotContain("CheckBoxFor(m => m[i]", html);
    }
}
