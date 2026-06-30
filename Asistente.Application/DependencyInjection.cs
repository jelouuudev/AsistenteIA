using Asistente.Application.Interfaces;
using Asistente.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Asistente.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IChatService, ChatService>();
        return services;
    }
}
