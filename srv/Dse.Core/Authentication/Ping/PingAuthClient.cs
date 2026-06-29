// Copyright (c) PNC Financial Services. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;

namespace Dse.Authentication.Ping;

public interface IPingAuthClient
{
    public Task<IReadOnlyDictionary<string, string>?> DecodeAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default
    );
}

[ExcludeFromCodeCoverage]
public sealed class PingAuthClient(IHttpClientFactory httpClientFactory) : IPingAuthClient
{
    private const string UserInfoPath = "/idp/userinfo.openid?schema=openid&access_token=";

    public async Task<IReadOnlyDictionary<string, string>?> DecodeAccessTokenAsync(
        string accessToken,
        CancellationToken cancellationToken = default
    )
    {
        var client = httpClientFactory.CreateClient(PingAuthDefaults.HttpClientName);

        using var response = await client.GetAsync(
            $"{UserInfoPath}{Uri.EscapeDataString(accessToken)}",
            cancellationToken
        );

        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Dictionary<string, string>?>(cancellationToken)
            : null;
    }
}
