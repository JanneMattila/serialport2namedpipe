namespace SerialPort2NamedPipeConnector;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string _serialPort;
    private readonly string _namedPipe;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _serialPort = configuration.GetValue<string>("SerialPort") ?? "COM1";
        _namedPipe = configuration.GetValue<string>("NamedPipe") ?? "\\\\.\\pipe\\com1";
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        return base.StopAsync(cancellationToken);
    }
}
