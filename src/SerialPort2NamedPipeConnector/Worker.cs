using System.IO.Pipes;
using System.IO.Ports;
using System.Text;

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

    private ulong _serialPortBytesRead = 0;
    private ulong _namedPipeBytesRead = 0;

    public Worker(ILogger<Worker> logger, IConfiguration configuration)
    {
        _logger = logger;
        _serialPortName = configuration.GetValue<string>("SerialPort") ?? "COM1";
        _baudRate = configuration.GetValue("BaudRate", 9600);
        _namedPipeName = configuration.GetValue<string>("NamedPipe") ?? "com1";
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Serial port: {SerialPort}", _serialPortName);
        _logger.LogDebug("Baud rate: {BaudRate}", _baudRate);
        _logger.LogDebug("Named pipe: {NamedPipe}", _namedPipeName);

        foreach (var serialPortName in SerialPort.GetPortNames())
        {
            _logger.LogDebug("Found serial port: {SerialPortName}", serialPortName);
        }

        _serialPort = new SerialPort
        {
            PortName = _serialPortName,
            BaudRate = _baudRate,
            Handshake = Handshake.None,
            StopBits = StopBits.One,
            Parity = Parity.None,
            ReadTimeout = SerialPort.InfiniteTimeout,
            WriteTimeout = SerialPort.InfiniteTimeout
        };

        _namedPipe = new NamedPipeClientStream(".", _namedPipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        VerifySerialPortConnection();
        VerifyNamedPipeConnection();

        _serialPortReadThread = new Thread(SerialPortReadThread);
        _serialPortReadThread.Start();

        _namedPipeReadThread = new Thread(NamedPipeReadThread);
        _namedPipeReadThread.Start();

        return base.StartAsync(cancellationToken);
    }

    private void VerifySerialPortConnection()
    {
        if (!_serialPort.IsOpen)
        {
            _logger.LogInformation("Opening serial port");
            _serialPort.Open();
            _logger.LogInformation("Serial port opened");
        }
    }

    private void VerifyNamedPipeConnection()
    {
        if (!_namedPipe.IsConnected)
        {
            _logger.LogInformation("Connecting with named pipe");
            _namedPipe.Connect();
            _logger.LogInformation("Connected with named pipe");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("SerialPort2NamedPipeConnector statistics: {Time} - Serial port bytes read: {SerialPortBytesRead} and named pipe bytes read: {NamedPipeBytesRead}",
                DateTimeOffset.Now, _serialPortBytesRead, _namedPipeBytesRead);
            _serialPortBytesRead = 0;
            _namedPipeBytesRead = 0;
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    public void SerialPortReadThread()
    {
        var buffer = new byte[1024];
        while (_isRunning)
        {
            try
            {
                VerifySerialPortConnection();
                var bytes = _serialPort.Read(buffer, 0, buffer.Length);
                if (bytes > 0)
                {
                    _serialPortBytesRead += (ulong)bytes;
                    var data = Convert.ToHexString(buffer, 0, bytes);
                    var text = Encoding.UTF8.GetString(buffer, 0, bytes);
                    _logger.LogTrace("Received {Bytes} bytes from serial port: {Data}: {Text}", bytes, data, text);

                    VerifyNamedPipeConnection();
                    _namedPipe.Write(buffer, 0, bytes);
                    _namedPipe.Flush();
                    _namedPipe.WaitForPipeDrain();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error reading from serial port");
                Thread.Sleep(5000);
            }
        }
    }

    public void NamedPipeReadThread()
    {
        var buffer = new byte[1024];
        while (_isRunning)
        {
            try
            {
                VerifyNamedPipeConnection();
                var bytes = _namedPipe.Read(buffer, 0, buffer.Length);
                if (bytes > 0)
                {
                    _namedPipeBytesRead += (ulong)bytes;
                    var data = Convert.ToHexString(buffer, 0, bytes);
                    var text = Encoding.UTF8.GetString(buffer, 0, bytes);
                    _logger.LogTrace("Received {Bytes} bytes from named pipe: {Data}: {Text}", bytes, data, text);

                    VerifySerialPortConnection();
                    _serialPort.Write(buffer, 0, bytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading from named pipe");
                Thread.Sleep(5000);
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
