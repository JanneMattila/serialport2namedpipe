using System.IO.Pipes;

var isRunning = true;
var namedPipe = new NamedPipeServerStream("com1", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough);

Console.WriteLine("Waiting for client to connect...");
namedPipe.WaitForConnection();
Console.WriteLine("Client connected");

void NamedPipeReadThread()
{
    var buffer = new byte[1024];
    while (isRunning)
    {
        try
        {
            var bytes = namedPipe.Read(buffer, 0, buffer.Length);
            if (bytes > 0)
            {
                var data = Convert.ToHexString(buffer, 0, bytes);
                Console.WriteLine($"Received {bytes} bytes from named pipe: {data}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Thread.Sleep(5000);
        }
    }
}

var namedPipeReadThread = new Thread(NamedPipeReadThread);
namedPipeReadThread.Start();

var seq = 1;
while (true)
{
    Console.WriteLine("Press enter to send data to client");
    Console.ReadLine();

    // https://deconz.dresden-elektronik.de/raspbian/deCONZ-Serial-Protocol-en_1.21.pdf
    // Read Firmware Version
    var bytes = 13;
    var buffer = new byte[bytes];
    buffer[0] = 0x0D;
    buffer[1] = (byte)seq++;
    buffer[2] = 0x00;
    buffer[3] = 0x00;
    buffer[4] = 0x09;

    buffer[5] = 0x00;
    buffer[6] = 0x00;
    buffer[7] = 0x00;
    buffer[8] = 0x00;

    int crc = 0;
    for (var i = 0; i < 9; i++)
        crc += buffer[i];
    byte crc0 = (byte)((~crc + 1) & 0xFF);
    byte crc1 = (byte)(((~crc + 1) >> 8) & 0xFF);

    buffer[9] = crc0;
    buffer[10] = crc1;
    buffer[12] = 0xC0;

    var data = Convert.ToHexString(buffer, 0, bytes);
    Console.WriteLine($"Sending {bytes} bytes to named pipe: {data}");
    namedPipe.Write(buffer, 0, bytes);

    // Fixed example:
    //var rawData = Convert.FromHexString("0A02000800010022C9FFC0C00A02000800010022C9FFC0");
    //var fixedData = Convert.ToHexString(rawData, 0, rawData.Length);
    //Console.WriteLine($"Sending {rawData.Length} bytes to named pipe: {fixedData}");
    //namedPipe.Write(rawData, 0, rawData.Length);
}
