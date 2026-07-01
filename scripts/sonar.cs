// Copyright (c) PNC Financial Services. All rights reserved.
//
// Preflight SonarQube gate check. Mirrors the pipeline's build job against two
// shadow projects so the real project's gating record is never touched:
//
//   <key>.preflight.<user>      pinned baseline at main  → "do MY changes pass?"
//   <key>.preflight-30d.<user>  30-day new-code window   → "will the PIPELINE gate pass?"
//
// The pipeline gate judges all code changed in the last 30 days, including what
// teammates already merged — the 30-day verdict is the deployment prediction and
// drives the exit code (0 pass, 1 fail, 2 usage, 3 inconclusive).
//
//   SONAR_HOST_URL=https://sonar.pncint.net SONAR_TOKEN=<token> dotnet ./scripts/sonar.cs
//
// Run --baseline first (and again whenever main moves or the window slides): it
// analyzes main and the ~30-day-old commit in throwaway git worktrees to seed
// both baselines. --window-ref <ref> overrides the 30-day commit.

#:package CliWrap@3.*
#:package Spectre.Console@0.*
#:package YamlDotNet@16.*

using System.Text;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;
using YamlDotNet.RepresentationModel;

var sonarUrl = Arg("--url") ?? Env("SONAR_HOST_URL") ?? "http://127.0.0.1:44931";
var sonarToken = Arg("--token") ?? Env("SONAR_TOKEN");
var gateName = Arg("--gate") ?? Env("SONAR_GATE") ?? "Sonar way PNC";
var isBaselineRun = args.Contains("--baseline");

if (sonarToken is null)
{
    AnsiConsole.MarkupLine("[red]Missing SonarQube token. Set SONAR_TOKEN or pass --token <token>.[/]");
    return 2;
}

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(60));
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
var ct = cts.Token;

var repoRoot = Environment.CurrentDirectory;
repoRoot = (await GitAsync("rev-parse", "--show-toplevel")).Trim();
var shortHash = (await GitAsync("rev-parse", "--short=7", "HEAD")).Trim();

var pipeline = PipelineVariables.Load(Path.Combine(repoRoot, "azure-pipelines.yml"));
var user = Environment.UserName.ToLowerInvariant();
var deltaKey = Arg("--project-key") ?? $"{pipeline.ProjectKey}.preflight.{user}";
var windowKey = $"{pipeline.ProjectKey}.preflight-30d.{user}";
var projectVersion = $"{pipeline.Version}-preflight-{shortHash}";

AnsiConsole.Write(new FigletText("Sonar Preflight").Color(Color.SteelBlue1));
AnsiConsole.Write(
    new Panel(
        new Rows(
            Markup.FromInterpolated($"[grey]Server[/]        {sonarUrl}"),
            Markup.FromInterpolated($"[grey]Your changes[/]  {deltaKey}"),
            Markup.FromInterpolated($"[grey]30-day gate[/]   {windowKey}"),
            Markup.FromInterpolated($"[grey]Version[/]       {projectVersion}"),
            Markup.FromInterpolated($"[grey]Quality gate[/]  {gateName}")
        )
    )
        .Header(isBaselineRun ? "[steelblue1] Preflight — baseline [/]" : "[steelblue1] Preflight [/]")
        .RoundedBorder()
        .BorderColor(Color.Grey37)
);

using var http = new HttpClient { BaseAddress = new(sonarUrl), Timeout = TimeSpan.FromSeconds(30) };
http.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{sonarToken}:")));

return isBaselineRun ? await BaselineAsync() : await PreflightAsync();

// ---------------------------------------------------------------------------
// Modes
// ---------------------------------------------------------------------------

async Task<int> BaselineAsync()
{
    var baseRef = Arg("--baseline-ref") ?? await FirstExistingRefAsync("origin/main", "main", "HEAD");
    var baseCommit = (await GitAsync("rev-parse", baseRef)).Trim();

    var windowCommit =
        Arg("--window-ref") is { } wr ? (await GitAsync("rev-parse", wr)).Trim()
        : (await GitAsync("rev-list", "-1", "--before=30 days ago", baseCommit)).Trim() is { Length: > 0 } old ? old
        : (await GitAsync("rev-list", "--reverse", baseCommit)).Split('\n')[0].Trim();
    // sonar.projectDate only accepts yyyy-MM-dd.
    var windowDate = (await GitAsync("show", "-s", "--format=%cs", windowCommit)).Trim();

    Note($"Baseline for your changes: {baseRef} ({baseCommit[..7]})");
    Note($"Baseline for 30-day gate:  {windowCommit[..7]} ({windowDate}) — pass --window-ref to override");

    // Your-changes baseline: analyze main, pin the new-code period to that analysis.
    await EnsureProjectAsync(deltaKey);
    var deltaAnalysis = await ScanWorktreeAsync(deltaKey, baseCommit, projectDate: null);
    if (deltaAnalysis is null)
    {
        return 1;
    }
    if (!await PostOkAsync($"api/new_code_periods/set?project={Esc(deltaKey)}&type=SPECIFIC_ANALYSIS&value={deltaAnalysis}"))
    {
        AnsiConsole.MarkupLine("[red]✖ Could not pin the your-changes baseline.[/]");
        return 1;
    }
    Ok($"Your-changes baseline pinned to {baseRef}");

    // 30-day baseline: recreate the project (backdated analyses must be first),
    // analyze the old commit with sonar.projectDate, then adopt the 30-day window.
    await PostOkAsync($"api/projects/delete?project={Esc(windowKey)}");
    await EnsureProjectAsync(windowKey);
    if (await ScanWorktreeAsync(windowKey, windowCommit, projectDate: windowDate) is null)
    {
        return 1;
    }
    if (!await PostOkAsync($"api/new_code_periods/set?project={Esc(windowKey)}&type=NUMBER_OF_DAYS&value=30"))
    {
        AnsiConsole.MarkupLine("[red]✖ Could not set the 30-day new-code period.[/]");
        return 1;
    }
    Ok($"30-day gate baseline seeded at {windowCommit[..7]} ({windowDate})");
    AnsiConsole.MarkupLine("[steelblue1]Baselines ready — run [bold]dotnet ./scripts/sonar.cs[/] to check your code.[/]");
    return 0;
}

async Task<int> PreflightAsync()
{
    await EnsureProjectAsync(deltaKey);
    await EnsureProjectAsync(windowKey);

    var deltaReady = await NewCodePeriodTypeAsync(deltaKey) == "SPECIFIC_ANALYSIS";
    var windowReady = await NewCodePeriodTypeAsync(windowKey) == "NUMBER_OF_DAYS";
    if (!deltaReady || !windowReady)
    {
        AnsiConsole.MarkupLine(
            "[yellow]⚠ Missing baseline(s). The gate judges new code only — run [bold]dotnet ./scripts/sonar.cs --baseline[/] first; unseeded verdicts will be inconclusive.[/]"
        );
    }

    // One build+test cycle produces the coverage/trx reports; the second scan
    // (30-day project) rebuilds against its own begin-step but reuses those reports.
    if (!await RunStepsAsync(repoRoot, ScanSteps(deltaKey, repoRoot, projectDate: null, runTests: true)))
    {
        return 1;
    }
    var deltaAnalysis = await WaitForAnalysisAsync(repoRoot);

    if (!await RunStepsAsync(repoRoot, ScanSteps(windowKey, repoRoot, projectDate: null, runTests: false)))
    {
        return 1;
    }
    var windowAnalysis = await WaitForAnalysisAsync(repoRoot);

    var deltaVerdict = await RenderVerdictAsync(deltaKey, deltaAnalysis, "Your changes (vs main baseline)", advisory: true);
    var windowVerdict = await RenderVerdictAsync(windowKey, windowAnalysis, "Pipeline gate (30-day window)", advisory: false);

    if (windowVerdict == 1)
    {
        await RenderNewCodeIssuesAsync(windowKey);
    }
    if (deltaVerdict == 0 && windowVerdict == 1)
    {
        AnsiConsole.MarkupLine(
            "[yellow]Your changes are clean — the failure is inherited from the 30-day window. Fix those issues here and re-run until the pipeline gate passes.[/]"
        );
    }
    return windowVerdict;
}

// ---------------------------------------------------------------------------
// Scanning
// ---------------------------------------------------------------------------

string[][] ScanSteps(string projectKey, string dir, string? projectDate, bool runTests, bool isHistorical = false)
{
    // Vulnerability audits are time-dependent: a historical baseline commit must not
    // fail restore because an advisory was published after it was written.
    string[] audit = isHistorical ? ["-p:NuGetAudit=false"] : [];
    var props = pipeline.BuildSonarProperties(dir);
    List<string[]> steps =
    [
        ["tool", "restore"],
        [
            "dotnet-sonarscanner",
            "begin",
            $"/k:{projectKey}",
            $"/v:{projectVersion}",
            $"/d:sonar.host.url={sonarUrl}",
            $"/d:sonar.token={sonarToken}",
            .. projectDate is null ? Array.Empty<string>() : [$"/d:sonar.projectDate={projectDate}"],
            .. props.Select(p => $"/d:{p.Key}={p.Value}"),
        ],
        [
            "restore",
            pipeline.RestoreProj,
            .. File.Exists(Path.Combine(dir, "nuget.config")) ? new[] { "--configfile", "nuget.config" } : [],
            .. audit,
        ],
        ["build", pipeline.BuildProj, "-c", "Release", "--no-restore", .. audit],
    ];
    if (runTests)
    {
        steps.Add([
            "test",
            pipeline.BuildProj,
            "-c",
            "Release",
            "--logger",
            "trx",
            "--results-directory",
            "./TestResults",
            "--logger:Console;noprogress=true",
            "/p:CollectCoverage=true",
            "/p:CoverletOutputFormat=opencover",
            "--no-build",
            "--no-restore",
        ]);
    }
    steps.Add(["dotnet-sonarscanner", "end", $"/d:sonar.token={sonarToken}"]);
    return [.. steps];
}

async Task<bool> RunStepsAsync(string dir, string[][] steps)
{
    foreach (var step in steps)
    {
        var result = await CmdAsync("dotnet", step, dir);
        if (!result.IsSuccess && step is not [_, "end", ..])
        {
            // The end step exits non-zero on gate failure (sonar.qualitygate.wait);
            // that is a verdict, not an error — everything else is fatal.
            AnsiConsole.MarkupLine("[red]Preflight aborted: build step failed before analysis completed.[/]");
            return false;
        }
    }
    return true;
}

// Analyzes a historical commit in a disposable worktree. Tests are skipped:
// baseline coverage never influences new-code conditions.
async Task<string?> ScanWorktreeAsync(string projectKey, string commit, string? projectDate)
{
    var worktree = Path.Combine(Path.GetTempPath(), $"sonar-preflight-{commit[..7]}");
    if (Directory.Exists(worktree))
    {
        await GitAsync("worktree", "remove", "--force", worktree);
    }
    await GitAsync("worktree", "add", "--force", worktree, commit);
    try
    {
        // Old commits may predate the scanner tool manifest or nuget.config.
        foreach (var f in new[] { "dotnet-tools.json", "nuget.config" })
        {
            if (File.Exists(Path.Combine(repoRoot, f)))
            {
                File.Copy(Path.Combine(repoRoot, f), Path.Combine(worktree, f), overwrite: true);
            }
        }
        // The build shells out to openapi-ts, which needs node_modules.
        if (File.Exists(Path.Combine(worktree, "package.json")))
        {
            await CmdAsync("pnpm", ["install", "--frozen-lockfile", "--prefer-offline"], worktree);
        }
        if (!await RunStepsAsync(worktree, ScanSteps(projectKey, worktree, projectDate, runTests: false, isHistorical: true)))
        {
            return null;
        }
        return await WaitForAnalysisAsync(worktree);
    }
    finally
    {
        await GitAsync("worktree", "remove", "--force", worktree);
    }
}

async Task<string?> WaitForAnalysisAsync(string dir)
{
    var reportTask = Path.Combine(dir, ".sonarqube", "out", ".sonar", "report-task.txt");
    if (!File.Exists(reportTask))
    {
        AnsiConsole.MarkupLine("[red]No report-task.txt found; the scanner did not submit an analysis.[/]");
        return null;
    }
    var report = (await File.ReadAllLinesAsync(reportTask, ct))
        .Select(l => l.Split('=', 2))
        .Where(p => p.Length == 2)
        .ToDictionary(p => p[0], p => p[1]);

    return await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync(
            "[steelblue1]Waiting for server-side analysis…[/]",
            async _ =>
            {
                while (true)
                {
                    var task = (await GetJsonAsync($"api/ce/task?id={report["ceTaskId"]}")).GetProperty("task");
                    switch (task.GetProperty("status").GetString())
                    {
                        case "SUCCESS":
                            return task.GetProperty("analysisId").GetString();
                        case "FAILED" or "CANCELED":
                            AnsiConsole.MarkupLine(
                                "[red]Server-side analysis failed; check the background task on the server.[/]"
                            );
                            return null;
                        default:
                            await Task.Delay(TimeSpan.FromSeconds(2), ct);
                            continue;
                    }
                }
            }
        );
}

// ---------------------------------------------------------------------------
// Verdicts
// ---------------------------------------------------------------------------

async Task<int> RenderVerdictAsync(string projectKey, string? analysisId, string title, bool advisory)
{
    if (analysisId is null)
    {
        return 1;
    }
    var status = (await GetJsonAsync($"api/qualitygates/project_status?analysisId={analysisId}")).GetProperty("projectStatus");
    var dashboard = $"{sonarUrl.TrimEnd('/')}/dashboard?id={Esc(projectKey)}";

    // Zero evaluated conditions means the new-code gate had nothing to judge — not a real pass.
    if (status.GetProperty("conditions").GetArrayLength() == 0)
    {
        AnsiConsole.Write(
            new Panel(
                Markup.FromInterpolated(
                    $"[yellow bold]⚠ INCONCLUSIVE[/] — no baseline, so the gate evaluated nothing.\nRun [bold]dotnet ./scripts/sonar.cs --baseline[/] first.\n[grey]{dashboard}[/]"
                )
            )
                .Header($"[yellow] {title} [/]")
                .RoundedBorder()
                .BorderColor(Color.Yellow)
        );
        return 3;
    }

    var passed = status.GetProperty("status").GetString() == "OK";
    var table = new Table()
        .RoundedBorder()
        .BorderColor(Color.Grey37)
        .AddColumn("Condition")
        .AddColumn("Status")
        .AddColumn("Actual")
        .AddColumn("Required");
    foreach (var c in status.GetProperty("conditions").EnumerateArray())
    {
        var metric = c.GetProperty("metricKey").GetString()!;
        var ok = c.GetProperty("status").GetString() == "OK";
        table.AddRow(
            Markup.FromInterpolated($"{PrettyMetric(metric)}"),
            new Markup(ok ? "[green]OK[/]" : "[red bold]ERROR[/]"),
            Markup.FromInterpolated($"{FormatValue(metric, c.TryGetProperty("actualValue", out var av) ? av.GetString() : "—")}"),
            Markup.FromInterpolated(
                $"{PrettyComparator(c.GetProperty("comparator").GetString())} {FormatValue(metric, c.GetProperty("errorThreshold").GetString())}"
            )
        );
    }

    var summary = (passed, advisory) switch
    {
        (true, _) => (FormattableString)$"[green bold]✔ PASSED[/]\n[grey]{dashboard}[/]",
        (false, true) => (FormattableString)
            $"[red bold]✖ FAILED[/] — your changes alone would trip the gate.\n[grey]{dashboard}[/]",
        (false, false) => (FormattableString)
            $"[red bold]✖ FAILED[/] — the pipeline would reject a deployment right now.\n[grey]{dashboard}[/]",
    };
    AnsiConsole.Write(
        new Panel(new Rows(table, Markup.FromInterpolated(summary)))
            .Header(passed ? $"[green] {title} [/]" : $"[red] {title} [/]")
            .RoundedBorder()
            .BorderColor(passed ? Color.Green : Color.Red)
    );
    return passed ? 0 : 1;
}

// The fix-list: where the 30-day window's problems live.
async Task RenderNewCodeIssuesAsync(string projectKey)
{
    var issues = await GetJsonAsync(
        $"api/issues/search?components={Esc(projectKey)}&inNewCodePeriod=true&resolved=false&ps=1&facets=files,severities"
    );
    var total = issues.GetProperty("total").GetInt32();
    if (total == 0)
    {
        return;
    }
    var table = new Table()
        .RoundedBorder()
        .BorderColor(Color.Grey37)
        .AddColumn("File (new-code issues)")
        .AddColumn(new TableColumn("Count").RightAligned());
    var files = issues
        .GetProperty("facets")
        .EnumerateArray()
        .First(f => f.GetProperty("property").GetString() == "files")
        .GetProperty("values")
        .EnumerateArray()
        .Take(10);
    foreach (var f in files)
    {
        table.AddRow(
            Markup.FromInterpolated($"{f.GetProperty("val").GetString()?.Split(':').Last()}"),
            Markup.FromInterpolated($"{f.GetProperty("count").GetInt32()}")
        );
    }
    AnsiConsole.Write(
        new Panel(table)
            .Header($"[yellow] {total} open issue(s) in the 30-day window [/]")
            .RoundedBorder()
            .BorderColor(Color.Grey37)
    );
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

string? Arg(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string Esc(string value) => Uri.EscapeDataString(value);

void Ok(string message) => AnsiConsole.MarkupLineInterpolated($"[green]✔ {message}[/]");

void Note(string message) => AnsiConsole.MarkupLineInterpolated($"[grey]{message}[/]");

async Task<string> GitAsync(params string[] gitArgs) =>
    (
        await Cli.Wrap("git")
            .WithArguments(gitArgs)
            .WithWorkingDirectory(repoRoot)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync(ct)
    ).StandardOutput;

async Task<string> FirstExistingRefAsync(params string[] refs)
{
    foreach (var r in refs)
    {
        if ((await GitAsync("rev-parse", "--verify", "--quiet", r)).Trim().Length > 0)
        {
            return r;
        }
    }
    return "HEAD";
}

async Task<CommandResult> CmdAsync(string cmd, string[] cmdArgs, string dir)
{
    var display = $"{cmd} {string.Join(' ', cmdArgs.Select(a => a.Contains("sonar.token") ? "/d:sonar.token=***" : a))}";
    return await AnsiConsole
        .Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync(
            $"[steelblue1]{Markup.Escape(display)}[/]",
            async _ =>
            {
                AnsiConsole.MarkupLineInterpolated($"[grey85]$ {display}[/]");
                var result = await Cli.Wrap(cmd)
                    .WithArguments(cmdArgs)
                    .WithWorkingDirectory(dir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(o => AnsiConsole.MarkupLineInterpolated($"[grey]{o}[/]")))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(o => AnsiConsole.MarkupLineInterpolated($"[red3]{o}[/]")))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync(ct);
                AnsiConsole.MarkupLineInterpolated(
                    result.IsSuccess
                        ? (FormattableString)$"[green]✔ {display} ({result.RunTime:mm\\:ss})[/]"
                        : (FormattableString)$"[darkred_1]✖ {display} exited with code {result.ExitCode}[/]"
                );
                return result;
            }
        );
}

// A project must exist with the right gate before its first analysis,
// otherwise that analysis is judged by the server default gate.
async Task EnsureProjectAsync(string projectKey)
{
    var search = await GetJsonAsync($"api/projects/search?projects={Esc(projectKey)}");
    if (search.GetProperty("components").GetArrayLength() == 0)
    {
        if (await PostOkAsync($"api/projects/create?project={Esc(projectKey)}&name={Esc(projectKey)}"))
        {
            Ok($"Provisioned shadow project {projectKey}");
        }
        else
        {
            AnsiConsole.MarkupLineInterpolated($"[red3]✖ Could not provision {projectKey}[/]");
        }
    }
    if (!await PostOkAsync($"api/qualitygates/select?gateName={Esc(gateName)}&projectKey={Esc(projectKey)}"))
    {
        // Non-admins may not assign gates; the instance default then applies.
        AnsiConsole.MarkupLineInterpolated(
            $"[yellow]⚠ Could not assign gate \"{gateName}\" to {projectKey}; the server's default gate will judge it.[/]"
        );
    }
}

async Task<string?> NewCodePeriodTypeAsync(string projectKey) =>
    (await GetJsonAsync($"api/new_code_periods/show?project={Esc(projectKey)}")).GetProperty("type").GetString();

async Task<bool> PostOkAsync(string path)
{
    using var response = await http.PostAsync(path, null, ct);
    return response.IsSuccessStatusCode;
}

async Task<JsonElement> GetJsonAsync(string path)
{
    using var response = await http.GetAsync(path, ct);
    response.EnsureSuccessStatusCode();
    return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
}

static string PrettyMetric(string key) =>
    key switch
    {
        "new_reliability_rating" or "new_software_quality_reliability_rating" => "Reliability Rating (new code)",
        "new_security_rating" or "new_software_quality_security_rating" => "Security Rating (new code)",
        "new_maintainability_rating" or "new_software_quality_maintainability_rating" => "Maintainability Rating (new code)",
        "new_coverage" => "Coverage (new code)",
        "new_duplicated_lines_density" => "Duplicated Lines (new code)",
        "new_security_hotspots_reviewed" => "Security Hotspots Reviewed (new code)",
        "new_violations" => "Issues (new code)",
        _ => key.Replace('_', ' '),
    };

static string FormatValue(string metric, string? value) =>
    metric.EndsWith("_rating") && value is { } v && double.TryParse(v, out var d) && d is >= 1 and <= 5
        ? ((char)('A' + (int)double.Round(d) - 1)).ToString()
        : value ?? "—";

static string PrettyComparator(string? comparator) =>
    comparator switch
    {
        "GT" => "≤",
        "LT" => "≥",
        _ => "=",
    };

// Reads azure-pipelines.yml so exclusions/properties never drift from what the pipeline sends.
// The pipeline folds extra sonar.* properties into sonar_custom_exclusions (first line is the
// exclusion list, remaining lines are key=value pairs) — mirrored here.
sealed record PipelineVariables(string ProjectKey, string Version, string RestoreProj, string BuildProj, string CustomExclusions)
{
    public static PipelineVariables Load(string yamlPath)
    {
        var yaml = new YamlStream();
        using var reader = new StreamReader(yamlPath);
        yaml.Load(reader);

        var vars = ((YamlMappingNode)yaml.Documents[0].RootNode).Children[new YamlScalarNode("variables")];
        var map = ((YamlSequenceNode)vars)
            .OfType<YamlMappingNode>()
            .Where(n => n.Children.ContainsKey(new YamlScalarNode("name")))
            .ToDictionary(
                n => ((YamlScalarNode)n.Children[new YamlScalarNode("name")]).Value!,
                n => ((YamlScalarNode)n.Children[new YamlScalarNode("value")]).Value!
            );

        return new PipelineVariables(
            ProjectKey: $"{map["mnemonic"].ToLowerInvariant()}.{map["app_name"].ToLowerInvariant()}",
            Version: map["version"],
            RestoreProj: map["restore_proj"],
            BuildProj: map["build_proj"],
            CustomExclusions: map["sonar_custom_exclusions"]
        );
    }

    // Mirrors extraProperties of the enterprise template's SonarQubePrepare task.
    // Paths anchor to the directory being scanned (repo root or a baseline worktree).
    public IReadOnlyList<KeyValuePair<string, string>> BuildSonarProperties(string dir)
    {
        var lines = CustomExclusions
            .Replace("$(System.DefaultWorkingDirectory)", dir)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<KeyValuePair<string, string>> props =
        [
            new("sonar.scanner.skipJreProvisioning", "true"),
            new("sonar.scm.disabled", "true"),
            new("sonar.coverage.dtdVerification", "true"),
            new("sonar.exclusions", lines[0]),
            new("sonar.coverage.exclusions", "*Tests*.cs, *testresult*.xml, *opencover*.xml"),
            new("sonar.test.exclusions", "*Tests*.cs, *testresult*.xml, *opencover*.xml"),
            new("sonar.cs.opencover.reportsPaths", $"{dir}/**/coverage.opencover.xml"),
            new("sonar.cs.vstest.reportsPaths", $"{dir}/TestResults/*.trx"),
        ];
        props.AddRange(
            lines
                .Skip(1)
                .Select(l => l.Split('=', 2))
                .Where(p => p.Length == 2)
                .Select(p => new KeyValuePair<string, string>(p[0], p[1]))
        );
        // The generic test execution sensor hard-fails on a missing report file
        // (unlike coverage sensors, which merely warn) — pass only what exists.
        return [.. props.Where(p => p.Key != "sonar.testExecutionReportPaths" || p.Value.Split(',').All(File.Exists))];
    }
}
