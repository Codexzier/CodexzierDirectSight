using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace CodexzierDirectSight
{
    // ControlData ist in ControlData.cs definiert; hier verwenden wir die gemeinsame Klasse.

    /// <summary>
    /// TCP-Server für die Steuer-Einheit. Kommuniziert UTF-8-codierte JSON-Nachrichten.
    /// Pro Nachricht wird eine JSON-Zeile (mit Newline) erwartet. Eingehende Daten werden
    /// deserialisiert in <see cref="ControlData"/> und können direkt weiterverarbeitet werden.
    /// Als Antwort wird ein Acknowledge-Objekt (ebenfalls JSON) zurückgesendet.
    /// </summary>
    public static class TcpControlUnitServer
    {
        private const int Port = 5001; // anderer Port als der Test-Server

        public static async Task StartAsync(CancellationToken stoppingToken, ILogger logger, RaspberryPwmController? pwmController = null)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            var listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            logger.LogInformation("Control Unit TCP Server listening on port {Port}", Port);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (listener.Pending())
                    {
                        var client = await listener.AcceptTcpClientAsync(stoppingToken);
                        _ = HandleClientAsync(client, logger, pwmController);
                    }
                    else
                    {
                        await Task.Delay(100, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normaler Stop
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task HandleClientAsync(TcpClient client, ILogger logger, RaspberryPwmController? pwmController)
        {
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.AutoFlush = true;

            logger.LogInformation("Client connected");

            try
            {
                while (client.Connected)
                {
                    string? line;
                    try
                    {
                        line = await reader.ReadLineAsync();
                    }
                    catch (IOException) // Verbindung unterbrochen
                    {
                        break;
                    }

                    if (line == null)
                        break; // Client hat Verbindung beendet

                    ControlData? incoming;
                    try
                    {
                        incoming = ControlData.Deserialize(line);
                    }
                    catch (JsonException ex)
                    {
                        logger.LogWarning(ex, "Fehler beim Deserialisieren der eingehenden Nachricht");
                        // Bei fehlerhaften Daten senden wir eine Fehler-Antwort
                        var err = new ControlData { Servo1 = 0, Servo2 = 0, Text = "ERROR: invalid payload" };
                        await writer.WriteLineAsync(err.Serialize());
                        continue;
                    }

                    if (incoming == null)
                    {
                        var err = new ControlData { Servo1 = 0, Servo2 = 0, Text = "ERROR: empty payload" };
                        await writer.WriteLineAsync(err.Serialize());
                        continue;
                    }

                    logger.LogInformation("Received ControlData: Servo1={Servo1}, Servo2={Servo2}, Text={Text}", incoming.Servo1, incoming.Servo2, incoming.Text);

                    // Wenn ein PWM-Controller vorhanden ist, setze die Servos anhand des gemappten Pulses
                    if (pwmController != null)
                    {
                        // Mappe eingehende Werte intelligent auf Millisekunden
                        double pulseMs1 = MapServoValueToMs(incoming.Servo1);
                        double pulseMs2 = MapServoValueToMs(incoming.Servo2);

                        try
                        {
                            pwmController.SetPulseMsChannel1(pulseMs1);
                            pwmController.SetPulseMsChannel2(pulseMs2);
                            logger.LogDebug("Set PWM pulses: ch1={Pulse1}ms, ch2={Pulse2}ms", pulseMs1, pulseMs2);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Fehler beim Setzen der PWM-Signale");
                        }
                    }

                    // Als Antwort senden wir ein Ack zurück.
                    var response = new ControlData
                    {
                        Servo1 = incoming.Servo1,
                        Servo2 = incoming.Servo2,
                        Text = $"ACK {DateTime.UtcNow:O}"
                    };

                    await writer.WriteLineAsync(response.Serialize());
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unerwarteter Fehler im Client-Handler");
            }
            finally
            {
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Fehler beim Schließen des Clients");
                }

                logger.LogInformation("Client disconnected");
            }
        }

        // Mapping-Regeln (angenommen):
        // - Wenn value in [0,180] => interpretiere als Winkel (Grad) und mappe linear auf [1ms,2ms]
        // - Wenn value in [1000,2000] => interpretiere als Mikrosekunden und wandle in ms um
        // - Wenn value in [1,2] => interpretiere direkt als ms
        // - Sonst: clamp in den Bereich [1ms,2ms]
        private static double MapServoValueToMs(int value)
        {
            if (value >= 0 && value <= 180)
            {
                // Winkel -> ms
                return 1.0 + (value / 180.0) * 1.0; // 0deg ->1ms, 180deg ->2ms
            }

            if (value >= 1000 && value <= 2000)
            {
                // Mikrosekunden -> ms
                return value / 1000.0;
            }

            if (value >= 1 && value <= 2)
            {
                // Millisekunden als integer
                return (double)value;
            }

            // Fallback: clamp
            return Math.Clamp(value, 1.0, 2.0);
        }
    }
}
