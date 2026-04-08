using MassTransit;
using Serilog;
using NotificationService.Consumers;
using NotificationService.Data;
using NotificationService.Services;
using NotificationService.Stores;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Net.Mail;
using System.Reflection;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Forwarded headers (not strictly needed for worker, but harmless if hosting health endpoint)
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // EF Core InMemory — notification preferences
    builder.Services.AddDbContext<NotificationDbContext>(opt =>
        opt.UseInMemoryDatabase("NotificationInMemory"));

    // Notification services
    builder.Services.AddScoped<NotificationPreferenceService>();
    builder.Services.AddSingleton<RecentNotificationStore>();
    builder.Services.AddSingleton<ISmsService, ConsoleSmsService>();

    // FluentEmail with SMTP sender
    var smtpHost = builder.Configuration["Email:Smtp:Host"] ?? "localhost";
    var smtpPort = builder.Configuration.GetValue<int>("Email:Smtp:Port", 25);
    var fromEmail = builder.Configuration["Email:Smtp:FromEmail"] ?? "noreply@notifications.local";
    var fromName = builder.Configuration["Email:Smtp:FromName"] ?? "Dynofin Notifications";

    builder.Services
        .AddFluentEmail(fromEmail, fromName)
        .AddSmtpSender(new SmtpClient(smtpHost, smtpPort)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = builder.Configuration.GetValue<bool>("Email:Smtp:EnableSsl", false)
        });

    // MassTransit configuration
    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<TransactionCreatedConsumer>();
        x.AddConsumer<KycStatusChangedConsumer>();
        x.AddConsumer<SuspiciousActivityFlaggedConsumer>();
        x.AddConsumer<PaymentApprovedConsumer>();
        x.AddConsumer<PaymentBatchSubmittedForApprovalConsumer>();
        x.AddConsumer<RepaymentCompletedConsumer>();

        x.UsingRabbitMq((context, cfg) =>
        {
            var amqpUrl = builder.Configuration["CLOUDAMQP_URL"]
                         ?? builder.Configuration["RABBITMQ_URL"]
                         ?? builder.Configuration["AMQP_URL"];

            if (!string.IsNullOrWhiteSpace(amqpUrl) && Uri.TryCreate(amqpUrl, UriKind.Absolute, out var uri))
            {
                Log.Information("RabbitMQ broker: {Scheme}://{Host}:{Port}{Vhost}", uri.Scheme, uri.Host, uri.Port, uri.AbsolutePath);
                cfg.Host(uri);
            }
            else
            {
                throw new InvalidOperationException("Missing CLOUDAMQP_URL (or RABBITMQ_URL/AMQP_URL). Configure it in Heroku config vars.");
            }

            cfg.ConfigureEndpoints(context);
        });
    });

    // OpenTelemetry Distributed Tracing
    var otelEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    if (!string.IsNullOrWhiteSpace(otelEndpoint))
    {
        var serviceName = Assembly.GetExecutingAssembly().GetName().Name ?? "NotificationService";
        var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName: serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(otelEndpoint.TrimEnd('/') + "/v1/traces");
                    otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    var otlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
                    if (!string.IsNullOrWhiteSpace(otlpHeaders))
                        otlp.Headers = otlpHeaders;
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(otelEndpoint.TrimEnd('/') + "/v1/metrics");
                    otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                    var otlpHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
                    if (!string.IsNullOrWhiteSpace(otlpHeaders))
                        otlp.Headers = otlpHeaders;
                }));
        Log.Information("[Telemetry] OpenTelemetry tracing and metrics configured for {Service} -> {Endpoint}", serviceName, otelEndpoint);
    }
    else
    {
        Log.Information("[Tracing] OTEL_EXPORTER_OTLP_ENDPOINT not set. Distributed tracing disabled.");
    }

    builder.Services.AddHealthChecks();
    builder.Services.AddControllers();

    var app = builder.Build();

    app.UseForwardedHeaders();

    app.MapHealthChecks("/health");
    app.MapControllers();

    Log.Information("NotificationService starting up");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NotificationService terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
