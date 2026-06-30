// Copyright (c) PNC Financial Services. All rights reserved.

using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Options;
using ServiceScan.SourceGenerator;

namespace Dse.Api.Scanning;

public static partial class ServiceCollectionExtensions
{
    [ScanForTypes(AttributeFilter = typeof(OptionsAttribute), Handler = nameof(AddOption), AssemblyNameFilter = "Dse.*")]
    public static partial IServiceCollection AddDseOptions(this IServiceCollection services);

    private static void AddOption<TOptions>(IServiceCollection services)
        where TOptions : class
    {
        var attr = typeof(TOptions).GetCustomAttribute<OptionsAttribute>();

        var builder = services.AddOptions<TOptions>(attr?.Name).ValidateDataAnnotations().ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<TOptions>>(s => new FluentValidateOptions<TOptions>(s, builder.Name));

        if (attr?.Path is { } path)
        {
            builder.BindConfiguration(path);
        }
    }

    private sealed class FluentValidateOptions<T>(IServiceProvider sp, string? optionsName) : IValidateOptions<T>
        where T : class
    {
        public ValidateOptionsResult Validate(string? name, T options)
        {
            if (optionsName is not null && optionsName != name)
            {
                return ValidateOptionsResult.Skip;
            }

            ArgumentNullException.ThrowIfNull(options);

            using var scope = sp.CreateScope();
            var type = options.GetType().Name;
            var validators = scope.ServiceProvider.GetServices<IValidator<T>>();

            List<string> errors =
            [
                .. validators.SelectMany(validator =>
                    validator.Validate(options) is not { IsValid: false } result
                        ? []
                        : result.Errors.Select(failure =>
                            $"Validation failed for {type}.{failure.PropertyName} with the error: {failure.ErrorMessage}"
                        )
                ),
            ];

            return errors.Count > 0 ? ValidateOptionsResult.Fail(errors) : ValidateOptionsResult.Success;
        }
    }
}
