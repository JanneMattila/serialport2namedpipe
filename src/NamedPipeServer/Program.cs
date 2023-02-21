using System.IO.Pipes;

var buffer = new byte[1024];
var namedPipe = new NamedPipeServerStream("com1", PipeDirection.InOut);

Console.WriteLine("Waiting for client to connect...");
namedPipe.WaitForConnection();
Console.WriteLine("Client connected");

while (true)
{
    var bytes = namedPipe.Read(buffer, 0, buffer.Length);
    var data = Convert.ToHexString(buffer, 0, bytes);
    Console.WriteLine($"Received {bytes} bytes from named pipe: {data}");
}
