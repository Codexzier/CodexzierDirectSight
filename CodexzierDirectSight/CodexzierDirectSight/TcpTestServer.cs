using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CodexzierDirectSight
{
    public static class TcpTestServer
    {
        private const int Port = 5000;
        public static async Task StartAsync(CancellationToken stoppingToken, ILogger logger)
        {
            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            logger.LogInformation($"TCP Server listening on port {Port}");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (listener.Pending())
                    {
                        var client = await listener.AcceptTcpClientAsync(stoppingToken);
                        _ = HandleClientAsync(client, logger);
                    }
                    else
                    {
                        await Task.Delay(100, stoppingToken); // kleine Pause
                    }
                }
            }
            catch (OperationCanceledException) { } // Normaler Stop
            finally
            {
                listener.Stop();
            }
        }

        private static async Task HandleClientAsync(TcpClient client, ILogger logger)
        {
            await using var stream = client.GetStream();
            var message = Encoding.UTF8.GetBytes($"Hello World! {DateTime.Now:U}\n");
            await stream.WriteAsync(message, 0, message.Length);
            client.Close();
            logger.LogInformation("Sent Hello World to client");
        }
    }
}