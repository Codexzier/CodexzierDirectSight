using System.Runtime.InteropServices;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodexzierDirectSight;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RaspberryPwmController? pwmController = null;

        // Wenn wir auf Linux laufen (Raspberry Pi), versuche einen PWM-Controller zu erstellen.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                // Default: chip 0, channels 0 and 1. Je nach Board/Overlay müssen diese IDs angepasst werden.
                pwmController = new RaspberryPwmController(chip: 0, channel1: 0, channel2: 1);
                logger.LogInformation("RaspberryPwmController initialisiert (chip=0, ch1=0, ch2=1)");

                if (!pwmController.IsHardwareAvailable)
                {
                    var msg = "PWM hardware wrapper initialized, but real hardware is not available (using No-op fallback).";
                    logger.LogWarning(msg);
                    Console.WriteLine(msg);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Konnte RaspberryPwmController nicht initialisieren; PWM wird deaktiviert");
                Console.WriteLine("Konnte RaspberryPwmController nicht initialisieren; PWM wird deaktiviert: " + ex.Message);
                pwmController = null;
            }
        }
        else
        {
            // Nicht-Linux: Hinweis in der Konsole
            Console.WriteLine("Running on non-Linux platform: PWM will be disabled (No-op). To enable real PWM, run on Raspberry Pi Linux and install the required packages.");
        }

        // Starte den TCP-Server und übergebe ggf. den PWM-Controller
        _ = TcpControlUnitServer.StartAsync(stoppingToken, logger, pwmController);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            await Task.Delay(1000, stoppingToken);
        }

        // Aufräumen
        pwmController?.Dispose();
    }
}