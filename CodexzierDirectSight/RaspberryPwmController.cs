using System;
using System.Reflection;

namespace CodexzierDirectSight
{
    /// <summary>
    /// Verwaltet zwei PWM-Ausgänge für Servos auf einem Raspberry Pi.
    /// Liefert ein 50Hz-Signal (Periode 20ms). Der High-Impulse kann als Millisekundenwert
    /// zwischen 1.0 ms und 2.0 ms gesetzt werden, um Servo-Positionen zu steuern.
    ///
    /// Die Implementierung versucht zur Laufzeit, den Typ System.Device.Pwm.PwmChannel
    /// per Reflection zu laden und echte PWM-Kanäle zu erstellen. Ist das Paket
    /// nicht installiert (z.B. beim Entwickeln/Builden auf Windows), wird ein No-op-Fallback
    /// verwendet, so dass der Code kompiliert und getestet werden kann.
    ///
    /// Hinweis: Für echten Raspberry Pi Betrieb installiere das NuGet-Paket:
    /// dotnet add package System.Device.Gpio
    /// oder
    /// dotnet add package System.Device.Pwm
    /// ggf. zusammen mit Iot.Device.Bindings je nach Board.
    /// </summary>
    public sealed class RaspberryPwmController : IDisposable
    {
        private const double Frequency = 50.0; // 50Hz -> Period = 20ms
        private const double PeriodMs = 1000.0 / Frequency; // 20ms
        private const double MinPulseMs = 1.0; // 1ms
        private const double MaxPulseMs = 2.0; // 2ms

        private readonly IPwmChannelWrapper _channel1;
        private readonly IPwmChannelWrapper _channel2;
        private bool _disposed;

        public RaspberryPwmController(int chip, int channel1, int channel2)
        {
            _channel1 = PwmChannelFactory.Create(chip, channel1);
            _channel2 = PwmChannelFactory.Create(chip, channel2);

            _channel1.Start();
            _channel2.Start();
        }

        public void SetPulseMsChannel1(double pulseMs)
        {
            ThrowIfDisposed();
            SetPulseMs(_channel1, pulseMs);
        }

        public void SetPulseMsChannel2(double pulseMs)
        {
            ThrowIfDisposed();
            SetPulseMs(_channel2, pulseMs);
        }

        private static void SetPulseMs(IPwmChannelWrapper channel, double pulseMs)
        {
            if (channel == null) throw new ArgumentNullException(nameof(channel));
            var clamped = Math.Clamp(pulseMs, MinPulseMs, MaxPulseMs);
            var dutyCycle = clamped / PeriodMs; // duty cycle as fraction of period
            channel.SetDutyCycle(dutyCycle);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RaspberryPwmController));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _channel1?.Stop(); _channel1?.Dispose(); } catch { }
            try { _channel2?.Stop(); _channel2?.Dispose(); } catch { }
        }

        private interface IPwmChannelWrapper : IDisposable
        {
            void Start();
            void Stop();
            void SetDutyCycle(double duty);
        }

        private static class PwmChannelFactory
        {
            public static IPwmChannelWrapper Create(int chip, int channel)
            {
                // Versuche, den echten System.Device.Pwm.PwmChannel Typ per Reflection zu laden
                var pwmType = Type.GetType("System.Device.Pwm.PwmChannel, System.Device.Pwm");
                if (pwmType == null)
                {
                    // Fallback: Paket nicht installiert. Verwende No-op Implementierung.
                    return new NoOpPwmChannelWrapper();
                }

                try
                {
                    // Finde die statische Create-Methode: Create(int chip, int channel, double frequency, double dutyCycle)
                    var createMethod = pwmType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null,
                        new Type[] { typeof(int), typeof(int), typeof(double), typeof(double) }, null);

                    if (createMethod == null)
                    {
                        // Methode nicht gefunden -> Fallback
                        return new NoOpPwmChannelWrapper();
                    }

                    // Erzeuge Instanz
                    var instance = createMethod.Invoke(null, new object[] { chip, channel, Frequency, 0.0 });
                    if (instance == null) return new NoOpPwmChannelWrapper();

                    return new ReflectionPwmChannelWrapper(instance, pwmType);
                }
                catch
                {
                    // Auf jedem Fehler Fallback
                    return new NoOpPwmChannelWrapper();
                }
            }
        }

        private sealed class NoOpPwmChannelWrapper : IPwmChannelWrapper
        {
            private double _duty;
            public void Dispose() { }
            public void Start() { }
            public void Stop() { }
            public void SetDutyCycle(double duty) { _duty = duty; }
        }

        private sealed class ReflectionPwmChannelWrapper : IPwmChannelWrapper
        {
            private readonly object _instance;
            private readonly MethodInfo? _startMethod;
            private readonly MethodInfo? _stopMethod;
            private readonly MethodInfo? _disposeMethod;
            private readonly PropertyInfo? _dutyProp;

            public ReflectionPwmChannelWrapper(object instance, Type pwmType)
            {
                _instance = instance ?? throw new ArgumentNullException(nameof(instance));
                _startMethod = pwmType.GetMethod("Start");
                _stopMethod = pwmType.GetMethod("Stop");
                _disposeMethod = pwmType.GetMethod("Dispose");
                _dutyProp = pwmType.GetProperty("DutyCycle");
            }

            public void Dispose()
            {
                try { _disposeMethod?.Invoke(_instance, null); } catch { }
            }

            public void Start()
            {
                try { _startMethod?.Invoke(_instance, null); } catch { }
            }

            public void Stop()
            {
                try { _stopMethod?.Invoke(_instance, null); } catch { }
            }

            public void SetDutyCycle(double duty)
            {
                try { _dutyProp?.SetValue(_instance, duty); } catch { }
            }
        }
    }
}
