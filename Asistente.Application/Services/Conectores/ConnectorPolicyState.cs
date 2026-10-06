using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Asistente.Application.Services.Conectores;

/// <summary>
/// Estado operativo compartido de los conectores (ETAPA 20): ventana deslizante
/// para Rate Limit y estado del Circuit Breaker. Es singleton porque el Gateway
/// es scoped y el estado debe sobrevivir entre llamadas.
/// </summary>
public class ConnectorPolicyState
{
    private readonly ConcurrentDictionary<int, Queue<DateTime>> _ventanas = new();
    private readonly ConcurrentDictionary<int, (int Fallos, DateTime? AbiertoHasta)> _circuitos = new();
    private readonly ConcurrentDictionary<string, (string Token, DateTime Expira)> _tokensOauth = new();

    /// <summary>Registra un intento y dice si el Rate Limit lo permite.</summary>
    public bool PermitePorRateLimit(int idConnector, int porMinuto)
    {
        if (porMinuto <= 0) return true;
        var ahora = DateTime.UtcNow;
        var ventana = _ventanas.GetOrAdd(idConnector, _ => new Queue<DateTime>());
        lock (ventana)
        {
            while (ventana.Count > 0 && (ahora - ventana.Peek()).TotalMinutes >= 1)
                ventana.Dequeue();
            if (ventana.Count >= porMinuto) return false;
            ventana.Enqueue(ahora);
            return true;
        }
    }

    /// <summary>True si el circuito está abierto (rechazo rápido, sin llamar fuera).</summary>
    public bool CircuitoAbierto(int idConnector)
    {
        if (!_circuitos.TryGetValue(idConnector, out var estado)) return false;
        if (estado.AbiertoHasta == null) return false;
        if (DateTime.UtcNow >= estado.AbiertoHasta.Value)
        {
            _circuitos.TryRemove(idConnector, out _);
            return false;
        }
        return true;
    }

    public void RegistrarExito(int idConnector) => _circuitos.TryRemove(idConnector, out _);

    public void RegistrarFallo(int idConnector, int umbral, int segundosAbierto)
    {
        if (umbral <= 0) return;
        var actual = _circuitos.GetOrAdd(idConnector, (0, null));
        var fallos = actual.Fallos + 1;
        DateTime? abiertoHasta = fallos >= umbral
            ? DateTime.UtcNow.AddSeconds(Math.Max(segundosAbierto, 1))
            : null;
        _circuitos[idConnector] = (fallos, abiertoHasta);
    }

    public bool TryGetToken(string clave, out string token)
    {
        token = string.Empty;
        if (!_tokensOauth.TryGetValue(clave, out var t)) return false;
        if (DateTime.UtcNow >= t.Expira.AddSeconds(-30)) return false;
        token = t.Token;
        return true;
    }

    public void GuardarToken(string clave, string token, int expiraEnSegundos)
        => _tokensOauth[clave] = (token, DateTime.UtcNow.AddSeconds(Math.Max(expiraEnSegundos, 60)));
}
