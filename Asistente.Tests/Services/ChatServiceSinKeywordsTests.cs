using Asistente.Application.Services;
using Xunit;

namespace Asistente.Tests.Services;

/// <summary>
/// El servicio de chat ya no decide por listas de términos: la herramienta se elige por
/// similitud con la DESCRIPCIÓN de cada una (configuración en BD) y las acciones de
/// confirmación/cancelación se reconocen por token EXACTO. Estos tests fijan ese
/// comportamiento, en particular el falso positivo que existía antes.
/// </summary>
public class ChatServiceSinKeywordsTests
{
    // ===== Confirmación / cancelación: coincidencia de mensaje completo =====

    [Theory]
    [InlineData("sí")]
    [InlineData("Si")]
    [InlineData("confirmar")]
    [InlineData("CONFIRMO")]
    [InlineData("dale")]
    [InlineData("ok")]
    [InlineData("de acuerdo")]
    public void Confirmacion_AceptaTokensExactos(string mensaje)
        => Assert.True(ChatService.EsMensajeConfirmacion(mensaje));

    [Theory]
    [InlineData("no quiero cancelar nada")]
    [InlineData("no por favor cancelar la operacion")]
    [InlineData("confirmame el total de ventas")]
    [InlineData("¿puedes cancelar el pedido?")]
    [InlineData("hola")]
    [InlineData("")]
    public void Cancelacion_NoSeDisparaConPalabrasDentroDeUnaFrase(string mensaje)
    {
        // Regresión del bug: `mensaje.Contains("cancelar")` hacia que
        // "no quiero cancelar nada" CANCELARA la operación.
        Assert.False(ChatService.EsMensajeCancelacion(mensaje));
    }

    [Theory]
    [InlineData("confirmame el total de ventas")]
    [InlineData("necesito confirmar los datos")]
    [InlineData("confirma el total de la venta")]
    public void Confirmacion_NoSeDisparaConPalabrasDentroDeUnaFrase(string mensaje)
        => Assert.False(ChatService.EsMensajeConfirmacion(mensaje));

    [Fact]
    public void Acciones_RespetanAcentosYCasing()
    {
        Assert.True(ChatService.EsMensajeConfirmacion("SÍ"));
        Assert.True(ChatService.EsMensajeConfirmacion("Confirmo"));
        Assert.True(ChatService.EsMensajeCancelacion("Cancela"));
    }

    // ===== Calculadora: extracción por forma, sin prefijos =====

    [Theory]
    [InlineData("cuánto es 2+2", "2+2")]
    [InlineData("calcula 5*3", "5*3")]
    [InlineData("resuelve (4+6)/2", "(4+6)/2")]
    [InlineData("3 + 4", "3 + 4")]
    [InlineData("¿cuánto es 12-4?", "12-4")]
    public void Calculadora_ExtraeExpresionPorForma(string mensaje, string esperado)
    {
        var expr = ChatService.ExtraerExpresionMatematica(mensaje);
        Assert.NotNull(expr);
        // Solo interesan los dígitos y operadores: el prefijo textual se descarta.
        Assert.Equal(esperado.Replace(" ", ""), expr!.Replace(" ", ""));
    }

    [Theory]
    [InlineData("muéstrame los clientes")]
    [InlineData("confirma el total de la venta")]
    [InlineData("hola")]
    public void Calculadora_NoDisparaConMensajesQueNoSonExpresiones(string mensaje)
        => Assert.Null(ChatService.ExtraerExpresionMatematica(mensaje));
}
