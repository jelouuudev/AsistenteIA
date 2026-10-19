using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Asistente.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Asistente.Tests.Web;

/// <summary>
/// Roles del controlador de Memorias (ETAPA 21).
///
/// Para que Supervisor pudiera ver el panel de memoria, el rol dejó de estar en el
/// controlador y pasó a cada acción: en ASP.NET [Authorize] se combina en AND, así
/// que un rol fijo a nivel de clase impide que ninguna acción se abra a otro rol.
///
/// Ese cambio es fácil de estropear sin darse cuenta: basta con añadir una acción
/// nueva sin declararle rol, o con quitarle el atributo a una, para que quede
/// abierta a cualquier usuario autenticado. El resto de la suite no lo vería, porque
/// no prueba controladores. Aquí se fija la matriz entera.
/// </summary>
public class MemoriasControllerRolesTests
{
    /// <summary>Acciones de solo lectura que sí deben ver Supervisor.</summary>
    private static readonly string[] LecturaDeObservabilidad = { "Dashboard", "Retencion" };

    private static IEnumerable<MethodInfo> Acciones()
        => typeof(MemoriasController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName);

    [Fact]
    public void ElPanel_DeMemoria_Existe()
    {
        // Es entregable de las secciones 7 y 17; si desapareciera, los tests de
        // roles seguirían en verde porque solo miran el resto de acciones.
        Assert.Contains(Acciones(), m => m.Name == "Dashboard");
    }

    /// <summary>
    /// Todo lo que crea, modifica, revierte o borra memorias es de Administrador.
    /// Un valor de Roles nulo aquí significa que la acción quedó abierta a
    /// cualquier usuario autenticado.
    /// </summary>
    [Fact]
    public void LasAccionesQueModificanMemoria_ExigenAdministrador()
    {
        var abiertas = Acciones()
            .Where(m => !LecturaDeObservabilidad.Contains(m.Name))
            .Where(m =>
            {
                var roles = m.GetCustomAttribute<AuthorizeAttribute>()?.Roles;
                return roles is null || !roles.Split(',').Select(r => r.Trim()).Contains("Administrador");
            })
            .Select(m => m.Name)
            .Distinct()
            .ToList();

        Assert.Empty(abiertas);
    }

    /// <summary>
    /// El panel y la retención son de lectura y de observabilidad: los ve también
    /// Supervisor, que ya tiene AUDITORIA_CONSULTAR en la API. Si quedaran en
    /// solo-Administrador, la mitad del requisito de observabilidad sería inútil
    /// para el rol que está para vigilar.
    /// </summary>
    [Fact]
    public void ElPanelYLaRetencion_LosVeTambienSupervisor()
    {
        foreach (var nombre in LecturaDeObservabilidad)
        {
            var accion = Acciones().Single(m => m.Name == nombre);
            var roles = accion.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

            Assert.False(string.IsNullOrWhiteSpace(roles),
                $"{nombre} no declara rol y depende solo del de la clase.");
            Assert.Contains("Supervisor", roles!.Split(',').Select(r => r.Trim()));
        }
    }

    /// <summary>
    /// Toda acción declara su propio rol y no se apoya solo en el de la clase.
    ///
    /// Es el error típico al añadir una acción nueva: si no le pones atributo,
    /// sigue funcionando (la clase exige autenticación) pero queda abierta a
    /// cualquier rol autenticado, incluido Operador, y nada lo avisa.
    /// </summary>
    [Fact]
    public void TodaAccion_DeclaraSuPropioRol()
    {
        var sinRol = Acciones()
            .Where(m => string.IsNullOrWhiteSpace(m.GetCustomAttribute<AuthorizeAttribute>()?.Roles))
            .Select(m => m.Name)
            .Distinct()
            .ToList();

        Assert.Empty(sinRol);
    }

    /// <summary>
    /// El controlador sigue exigiendo estar autenticado: es la red que cubre a
    /// cualquier acción que se añada sin declarar nada.
    /// </summary>
    [Fact]
    public void ElControlador_ExigeAutenticacion()
    {
        Assert.NotNull(typeof(MemoriasController).GetCustomAttribute<AuthorizeAttribute>());
    }
}
