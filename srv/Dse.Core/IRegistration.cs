// Copyright (c) PNC Financial Services. All rights reserved.

using Microsoft.Extensions.Hosting;

namespace Dse;

public interface IRegistration
{
    public static abstract void Register(IHostApplicationBuilder builder);
}
