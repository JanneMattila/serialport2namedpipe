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

    private NamedPipeClientStream _namedPipe;
    private Thread _namedPipeReadThread;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _serialPortName = configuration.GetValue<string>("SerialPort") ?? "COM1";
        _baudRate = configuration.GetValue("BaudRate", 9600);
        _namedPipeName = configuration.GetValue<string>("NamedPipe") ?? "com1";
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
            StopBits = StopBits.One,
            Parity = Parity.None,
            ReadTimeout = 10000,
            WriteTimeout = 10000
        };

        _logger.LogInformation("Opening serial port");
        _serialPort.Open();
        _logger.LogInformation("Serial port opened");

        _namedPipe = new NamedPipeClientStream(".", _namedPipeName, PipeDirection.InOut, PipeOptions.WriteThrough);

        _logger.LogInformation("Connecting with named pipe");
        _namedPipe.Connect();
        _logger.LogInformation("Connected with named pipe");

        _serialPortReadThread = new Thread(SerialPortReadThread);
        _serialPortReadThread.Start();

        _namedPipeReadThread = new Thread(NamedPipeReadThread);
        _namedPipeReadThread.Start();

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    public void SerialPortReadThread()
    {
        var buffer = new byte[32];
        while (_isRunning)
        {
            try
            {
                var bytes = _serialPort.Read(buffer, 0, buffer.Length);
                if (bytes > 0)
                {
                    _logger.LogInformation("Received {Bytes} bytes from serial port", bytes);

                    _namedPipe.Write(buffer, 0, bytes);
                    _namedPipe.Flush();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error reading from serial port");
            }
        }
    }

    public void NamedPipeReadThread()
    {
        var buffer = new byte[32];
        while (_isRunning)
        {
            try
            {
                var bytes = _namedPipe.Read(buffer, 0, buffer.Length);
                if (bytes > 0)
                {
                    _logger.LogInformation("Received {Bytes} bytes from named pipe", bytes);
                    _serialPort.Write(buffer, 0, bytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from named pipe");
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _isRunning = false;

        _serialPort.Close();
        _namedPipe.Close();

        return base.StopAsync(cancellationToken);
    }
}
