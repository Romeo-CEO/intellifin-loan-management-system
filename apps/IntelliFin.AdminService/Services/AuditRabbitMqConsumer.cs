using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IntelliFin.AdminService.Services;

public sealed class AuditRabbitMqConsumer : BackgroundService
{
    private const string ActivitySourceName = "IntelliFin.AdminService.AuditQueue";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    private readonly IServiceProvider _serviceProvider;
    private readonly AuditRabbitMqOptions _options;
    private readonly ILogger<AuditRabbitMqConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public AuditRabbitMqConsumer(
        IServiceProvider serviceProvider,
        IOptions<AuditRabbitMqOptions> options,
        ILogger<AuditRabbitMqConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Audit RabbitMQ consumer disabled");
            return Task.CompletedTask;
        }

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(_options.Exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        _channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(_options.QueueName, _options.Exchange, routingKey: string.Empty);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageAsync;

        _channel.BasicConsume(queue: _options.QueueName, autoAck: false, consumer: consumer);
        _logger.LogInformation("Audit RabbitMQ consumer started on exchange {Exchange}", _options.Exchange);

        return Task.CompletedTask;
    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        var propagationContext = Propagator.Extract(default, args.BasicProperties, ReadHeaderValues);
        var previousBaggage = Baggage.Current;
        Baggage.Current = propagationContext.Baggage;

        using var activity = ActivitySource.StartActivity(
            "audit.events.consume",
            ActivityKind.Consumer,
            propagationContext.ActivityContext);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination", _options.QueueName);
        activity?.SetTag("messaging.rabbitmq.delivery_tag", args.DeliveryTag);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();
            var payload = JsonSerializer.Deserialize<AuditEvent>(args.Body.Span);
            if (payload is null)
            {
                throw new InvalidOperationException("Invalid audit payload");
            }

            if (string.IsNullOrWhiteSpace(payload.CorrelationId) && activity is not null)
            {
                payload.CorrelationId = activity.TraceId.ToString();
            }

            await auditService.LogEventAsync(payload, CancellationToken.None);
            await auditService.FlushBufferAsync(CancellationToken.None);
            _channel?.BasicAck(args.DeliveryTag, multiple: false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process audit message {DeliveryTag}", args.DeliveryTag);
            _channel?.BasicNack(args.DeliveryTag, multiple: false, requeue: false);

            if (_channel is not null)
            {
                PublishToDeadLetter(args, activity);
            }

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
        }
        finally
        {
            Baggage.Current = previousBaggage;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        _channel?.Dispose();
        _connection?.Dispose();
    }

    private void PublishToDeadLetter(BasicDeliverEventArgs args, Activity? activity)
    {
        if (_channel is null)
        {
            return;
        }

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.Headers ??= new Dictionary<string, object>();

        var propagationContext = new PropagationContext(activity?.Context ?? default, Baggage.Current);
        Propagator.Inject(propagationContext, properties, InjectHeader);

        if (activity is not null)
        {
            properties.CorrelationId = activity.TraceId.ToString();
        }

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _options.DeadLetterQueue,
            basicProperties: properties,
            body: args.Body);
    }

    private static IEnumerable<string> ReadHeaderValues(IBasicProperties properties, string key)
    {
        var headers = properties?.Headers;
        if (headers is null)
        {
            return Array.Empty<string>();
        }

        if (!headers.TryGetValue(key, out var value) || value is null)
        {
            return Array.Empty<string>();
        }

        return value switch
        {
            byte[] bytes => new[] { Encoding.UTF8.GetString(bytes) },
            string str => new[] { str },
            _ => Array.Empty<string>()
        };
    }

    private static void InjectHeader(IBasicProperties properties, string key, string value)
    {
        properties.Headers ??= new Dictionary<string, object>();
        properties.Headers[key] = Encoding.UTF8.GetBytes(value);
    }
}
