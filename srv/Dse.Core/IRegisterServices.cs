// Copyright (c) PNC Financial Services. All rights reserved.

using Microsoft.Extensions.DependencyInjection;

namespace Dse;

public interface IRegisterServices
{
    public static abstract void Register(IServiceCollection services);
}
