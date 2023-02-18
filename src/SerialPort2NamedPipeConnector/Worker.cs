using System.IO.Ports;

namespace SerialPort2NamedPipeConnector;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string _serialPortName;
    private readonly int _baudRate;
    private readonly string _namedPipe;

    private SerialPort _serialPort;
    private Thread _serialPortReadThread;
    private bool _isRunning = true;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _serialPortName = configuration.GetValue<string>("SerialPort") ?? "COM1";
        _baudRate = configuration.GetValue("BaudRate", 9600);
        _namedPipe = configuration.GetValue<string>("NamedPipe") ?? "\\\\.\\pipe\\com1";
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Serial port: {SerialPort}", _serialPortName);
        _logger.LogInformation("Baud rate: {BaudRate}", _baudRate);
        _logger.LogInformation("Named pipe: {NamedPipe}", _namedPipe);

        foreach (var serialPortName in SerialPort.GetPortNames())
        {
            _logger.LogInformation("Found serial port: {SerialPortName}", serialPortName);
        }

        _serialPortReadThread = new Thread(SerialPortReadThread);

        _serialPort = new SerialPort
        {
            PortName = _serialPortName,
            BaudRate = _baudRate,
            Handshake = Handshake.None,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        _serialPort.Open();

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

    public void SerialPortReadThread()
    {
        while (_isRunning)
        {
            try
            {
                string message = _serialPort.ReadLine();
                Console.WriteLine(message);
            }
            catch (TimeoutException) { }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _isRunning = false;
        _serialPortReadThread.Join(5000);
        _serialPort.Close();

        return base.StopAsync(cancellationToken);
    }
}
