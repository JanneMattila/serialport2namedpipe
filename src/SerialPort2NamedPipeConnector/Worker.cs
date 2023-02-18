using System.IO.Pipes;
using System.IO.Ports;

namespace SerialPort2NamedPipeConnector;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly string _serialPortName;
    private readonly int _baudRate;
    private readonly string _namedPipeName;

    private SerialPort _serialPort;
    private Thread _serialPortReadThread;
    private bool _isRunning = true;

    private NamedPipeServerStream _namedPipe;
    private Thread _namedPipeReadThread;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _serialPortName = configuration.GetValue<string>("SerialPort") ?? "COM1";
        _baudRate = configuration.GetValue("BaudRate", 9600);
        _namedPipeName = configuration.GetValue<string>("NamedPipe") ?? "\\\\.\\pipe\\com1";
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Serial port: {SerialPort}", _serialPortName);
        _logger.LogInformation("Baud rate: {BaudRate}", _baudRate);
        _logger.LogInformation("Named pipe: {NamedPipe}", _namedPipeName);

        foreach (var serialPortName in SerialPort.GetPortNames())
        {
            _logger.LogInformation("Found serial port: {SerialPortName}", serialPortName);
        }

        _serialPort = new SerialPort
        {
            PortName = _serialPortName,
            BaudRate = _baudRate,
            Handshake = Handshake.None,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        _serialPort.Open();
        _serialPortReadThread = new Thread(SerialPortReadThread);

        _namedPipe = new NamedPipeServerStream(_namedPipeName, PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.None)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        _namedPipeReadThread = new Thread(NamedPipeReadThread);

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }

    public void SerialPortReadThread()
    {
        var buffer = new byte[1024];
        while (_isRunning)
        {
            try
            {
                var bytes = _serialPort.Read(buffer, 0, buffer.Length);
                if (bytes > 0)
                {
                    _namedPipe.Write(buffer, 0, bytes);
                }
            }
            catch (TimeoutException) { }
        }
    }

    public void NamedPipeReadThread()
    {
        var buffer = new byte[1024];
        while (_isRunning)
        {
            try
            {
                var bytes = _namedPipe.Read(buffer, 0, buffer.Length);
                if (bytes > 0)
                {
                    _serialPort.Write(buffer, 0, bytes);
                }
            }
            catch (TimeoutException) { }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _isRunning = false;

        _serialPortReadThread.Join(5000);
        _namedPipeReadThread.Join(5000);

        _serialPort.Close();
        _namedPipe.Close();

        return base.StopAsync(cancellationToken);
    }
}
