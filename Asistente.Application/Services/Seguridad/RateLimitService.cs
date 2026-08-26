using System.Collections.Concurrent;
using System.Diagnostics;
using Asistente.Application.Interfaces;

namespace Asistente.Application.Services.Seguridad;

/// <summary>
/// Rate Limiting en memoria por clave (usuario o IP) con ventana deslizante (Actividad 13 / Caso 5).
/// Evita abuso: solicitudes por minuto, por usuario, ejecuciones de herramientas/SQL/workflows.
/// Nota: en escenarios multi-instancia se recomienda un backend distribuido (Redis); aquí se
/// presenta la implementación local exigida por el RF.
/// </summary>
public class RateLimitService : IRateLimitService
{
    private readonly ConcurrentDictionary<string, (Stopwatch Inicio, int Conteo)> _ventanas = new();

    public (bool Permitido, int IntentosRestantes, int SegundosBloqueo) RegistrarYVerificar(
        string clave, int maxPorVentana, int ventanaSegundos)
    {
        var ahora = Stopwatch.GetTimestamp();
        var ventanaTicks = ventanaSegundos * Stopwatch.Frequency;

        var ventana = _ventanas.AddOrUpdate(clave,
            _ => (Stopwatch.StartNew(), 1),
            (_, existente) =>
            {
                if (existente.Inicio.ElapsedTicks > ventanaTicks)
                    return (Stopwatch.StartNew(), 1); // reinicia ventana
                return (existente.Inicio, existente.Conteo + 1);
            });

        var restantes = maxPorVentana - ventana.Conteo;
        if (ventana.Conteo > maxPorVentana)
        {
            var segundosRestantes = (int)System.Math.Ceiling(
                (ventanaTicks - ventana.Inicio.ElapsedTicks) / (double)Stopwatch.Frequency);
            return (false, 0, System.Math.Max(0, segundosRestantes));
        }
        return (true, System.Math.Max(0, restantes), 0);
    }
}
