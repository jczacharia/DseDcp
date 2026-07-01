// Copyright (c) PNC Financial Services. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dse.Extensions;

public static class LoggingExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection RemoveWindowsEventLogProvider()
        {
            const string eventLogProvider = "Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider";

            foreach (
                var descriptor in services
                    .Where(d => d.ServiceType == typeof(ILoggerProvider) && d.ImplementationType?.FullName == eventLogProvider)
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }

            return services;
        }
    }
}
