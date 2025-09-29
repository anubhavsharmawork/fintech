using MassTransit;
using Serilog;
using NotificationService.Consumers;
using Microsoft.AspNetCore.HttpOverrides;

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
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // MassTransit configuration
    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<TransactionCreatedConsumer>();

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

    builder.Services.AddHealthChecks();
    builder.Services.AddControllers();

    var app = builder.Build();

    app.UseForwardedHeaders();

    app.MapHealthChecks("/health");
    app.MapControllers();

    Log.Information("NotificationService starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NotificationService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
