using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniversityApp.Infrastructure.Data;

namespace UniversityApp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseInMemoryDatabase("UniversityDb"));

        return services;
    }
}
