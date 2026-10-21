using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Ecolink.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Tự động quét và đăng ký tất cả các Validator trong assembly Ecolink.Application
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
