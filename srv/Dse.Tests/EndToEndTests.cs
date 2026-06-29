// Copyright (c) PNC Financial Services. All rights reserved.

using System.Diagnostics;
using CliWrap;

namespace Dse.Tests;

public sealed class EndToEndTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task RunEndToEndTests()
    {
        await using var host = new ApiHost(testOutputHelper, [new("Ping:HeaderName", "X-Ping")]);
        host.StartServer();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        var result = await Cli.Wrap("npx")
            .WithArguments([.. CoreEnvironment.NodePrefix.Split(' '), "pnpm", "e2e"])
            .WithValidation(CommandResultValidation.None)
            .WithEnvironmentVariables(new Dictionary<string, string?>
            {
                ["TEST_API_BASE_URL"] = host.BaseAddress,
                ["PWDEBUG"] = Debugger.IsAttached ? "1" : null,
                ["CI"] = CoreEnvironment.IsRelease ? "1" : null,
            })
            .WithWorkingDirectory(CoreEnvironment.RepoRoot)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(testOutputHelper.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(testOutputHelper.WriteLine))
            .ExecuteAsync(cts.Token);

        result.ExitCode.Should().Be(0);
    }
}
