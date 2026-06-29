// Copyright (c) PNC Financial Services. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dse.Extensions;

public static class LoggingExtensions
{
    extension(IServiceCollection services)
    {
        public void RemoveWindowsEventLogProvider()
        {
            const string EventLogProvider = "Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider";

            foreach (
                var descriptor in services
                    .Where(d =>
                        d.ServiceType == typeof(ILoggerProvider) && d.ImplementationType?.FullName == EventLogProvider
                    )
                    .ToList()
            )
            {
                services.Remove(descriptor);
            }
        }
    }
}
