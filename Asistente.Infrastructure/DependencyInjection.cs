using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Asistente.Infrastructure.Repositories;
using Asistente.Infrastructure.Services;
using Asistente.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asistente.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AsistenteDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IConversacionRepository, ConversacionRepository>();
        services.AddScoped<IMensajeRepository, MensajeRepository>();
        services.AddScoped<IUnitOfWork>(sp =>
            sp.GetRequiredService<AsistenteDbContext>());

        services.Configure<OllamaConfig>(configuration.GetSection("Ollama"));

        services.AddHttpClient<IOllamaService, OllamaService>((sp, client) =>
        {
            var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaConfig>>();
            client.BaseAddress = new Uri(config.Value.Url);
            client.Timeout = TimeSpan.FromSeconds(config.Value.TimeoutSegundos + 5);
        });

        return services;
    }
}
