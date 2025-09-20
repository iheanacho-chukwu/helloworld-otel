// Program.cs
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Allow gRPC over cleartext (h2c) when using 4317 with http://
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

// -------------------- Config via env --------------------
string serviceName    = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "hello-otel";
string serviceVersion = "1.0.0";
string protocol       = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL") ?? "grpc"; // "grpc" or "http/protobuf"

// gRPC single endpoint (4317)
string grpcEndpoint   = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://127.0.0.1:4317";

// HTTP endpoints (4318)
string httpTraces     = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT")  ?? "http://127.0.0.1:4318/v1/traces";
string httpLogs       = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_LOGS_ENDPOINT")    ?? "http://127.0.0.1:4318/v1/logs";
string httpMetrics    = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT") ?? "http://127.0.0.1:4318/v1/metrics";

// Optional headers (e.g., "authorization=Bearer <token>")
string? otlpHeaders   = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");

// -------------------- Shared Resource (for logs) --------------------
var logResource = ResourceBuilder.CreateDefault()
    .AddService(serviceName, serviceVersion: serviceVersion)
    .AddAttributes(new[]
    {
        new KeyValuePair<string, object>("deployment.environment", Environment.GetEnvironmentVariable("OTEL_ENV") ?? "dev"),
        new KeyValuePair<string, object>("host.name", Environment.MachineName),
    });

// -------------------- Logging --------------------
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(o =>
{
    o.IncludeScopes = true;
    o.ParseStateValues = true;
    o.IncludeFormattedMessage = true;

    // LoggerProviderBuilder supports SetResourceBuilder
    o.SetResourceBuilder(logResource);

    o.AddOtlpExporter(exp =>
    {
        exp.Endpoint = new Uri(protocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase) ? httpLogs : grpcEndpoint);
        if (!string.IsNullOrWhiteSpace(otlpHeaders))
            exp.Headers = otlpHeaders;
    });
});

// -------------------- Traces & Metrics --------------------
// Use ConfigureResource here (portable across OTel versions)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(rb => rb
        .AddService(serviceName, serviceVersion: serviceVersion)
        .AddAttributes(new[]
        {
            new KeyValuePair<string, object>("deployment.environment", Environment.GetEnvironmentVariable("OTEL_ENV") ?? "dev"),
            new KeyValuePair<string, object>("host.name", Environment.MachineName),
        }))
    .WithTracing(tp =>
    {
        tp.AddAspNetCoreInstrumentation()     // avoid RecordException assignment to keep broad compatibility
          .AddHttpClientInstrumentation()
          .AddSource("HelloActivitySource")
          .AddOtlpExporter(exp =>
          {
              exp.Endpoint = new Uri(protocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase) ? httpTraces : grpcEndpoint);
              if (!string.IsNullOrWhiteSpace(otlpHeaders))
                  exp.Headers = otlpHeaders;
          });
    })
    .WithMetrics(mp =>
    {
        mp.AddAspNetCoreInstrumentation()
          .AddRuntimeInstrumentation()
          .AddOtlpExporter(exp =>
          {
              exp.Endpoint = new Uri(protocol.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase) ? httpMetrics : grpcEndpoint);
              if (!string.IsNullOrWhiteSpace(otlpHeaders))
                  exp.Headers = otlpHeaders;
          });
    });

// -------------------- Minimal API --------------------
var app = builder.Build();

var activitySource = new ActivitySource("HelloActivitySource");

app.MapGet("/", () => "Hello OTEL! Hit /hello to create spans + logs.");

app.MapGet("/hello", async (ILogger<Program> logger) =>
{
    logger.LogInformation("Handling /hello at {Utc}", DateTimeOffset.UtcNow);

    using var activity = activitySource.StartActivity("say-hello");
    activity?.SetTag("hello.tag", "world");

    // Generate a downstream HTTP span
    using var http = new HttpClient();
    try
    {
        var resp = await http.GetAsync("https://example.com");
        activity?.SetTag("example.status", (int)resp.StatusCode);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Downstream call failed");
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    }

    return Results.Ok(new { message = "hello, otel!" });
});

app.Run();
