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

        // Gibt an, ob echte PWM-Hardware (kein No-op-Fallback) verwendet wird
        public bool IsHardwareAvailable { get; }

        public RaspberryPwmController(int chip, int channel1, int channel2)
        {
            _channel1 = PwmChannelFactory.Create(chip, channel1);
            _channel2 = PwmChannelFactory.Create(chip, channel2);

            // Wenn beide Wrapper echte Hardware verwenden, gilt Hardware als vorhanden
            IsHardwareAvailable = _channel1.IsReal && _channel2.IsReal;

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

        // Neue Try-Methoden liefern Erfolg/Fehler-Meldung
        public bool TrySetPulseMsChannel1(double pulseMs, out string? error)
        {
            error = null;
            if (!IsHardwareAvailable)
            {
                error = "PWM hardware not available";
                return false;
            }

            try
            {
                SetPulseMsChannel1(pulseMs);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TrySetPulseMsChannel2(double pulseMs, out string? error)
        {
            error = null;
            if (!IsHardwareAvailable)
            {
                error = "PWM hardware not available";
                return false;
            }

            try
            {
                SetPulseMsChannel2(pulseMs);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
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
            try { _channel1.Stop(); _channel1.Dispose(); } catch { }
            try { _channel2.Stop(); _channel2.Dispose(); } catch { }
        }

        private interface IPwmChannelWrapper : IDisposable
        {
            void Start();
            void Stop();
            void SetDutyCycle(double duty);
            bool IsReal { get; }
        }

        private static class PwmChannelFactory
        {
            public static IPwmChannelWrapper Create(int chip, int channel)
            {
                try
                {
                    // Versuche, den echten System.Device.Pwm.PwmChannel Typ per Reflection zu laden
                    var pwmType = Type.GetType("System.Device.Pwm.PwmChannel, System.Device.Pwm");
                    if (pwmType == null)
                    {
                        // Fallback: Paket nicht installiert. Verwende No-op Implementierung.
                        Console.WriteLine("[RaspberryPwmController] System.Device.Pwm.PwmChannel type not found. Ensure 'System.Device.Pwm' (or appropriate IoT packages) is installed.");
                        return new NoOpPwmChannelWrapper();
                    }

                    // Finde die statische Create-Methode: Create(int chip, int channel, double frequency, double dutyCycle)
                    var createMethod = pwmType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null,
                        new Type[] { typeof(int), typeof(int), typeof(double), typeof(double) }, null);

                    if (createMethod == null)
                    {
                        Console.WriteLine("[RaspberryPwmController] PwmChannel.Create method not found on type 'System.Device.Pwm.PwmChannel'. Using no-op fallback.");
                        return new NoOpPwmChannelWrapper();
                    }

                    // Erzeuge Instanz
                    object? instance = null;
                    try
                    {
                        instance = createMethod.Invoke(null, new object[] { chip, channel, Frequency, 0.0 });
                    }
                    catch (TargetInvocationException tie)
                    {
                        Console.WriteLine($"[RaspberryPwmController] Exception while invoking PwmChannel.Create: {tie.InnerException?.Message ?? tie.Message}");
                        return new NoOpPwmChannelWrapper();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[RaspberryPwmController] Unexpected exception invoking PwmChannel.Create: {ex.Message}");
                        return new NoOpPwmChannelWrapper();
                    }

                    if (instance == null)
                    {
                        Console.WriteLine("[RaspberryPwmController] PwmChannel.Create returned null. Using no-op fallback.");
                        return new NoOpPwmChannelWrapper();
                    }

                    return new ReflectionPwmChannelWrapper(instance, pwmType);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RaspberryPwmController] Unexpected error in PwmChannelFactory.Create: {ex.Message}");
                    return new NoOpPwmChannelWrapper();
                }
            }
        }

        private sealed class NoOpPwmChannelWrapper : IPwmChannelWrapper
        {
            private double _duty;
            public bool IsReal => false;
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

            public bool IsReal => true;

            public void Dispose()
            {
                // Rethrow exceptions to allow caller to detect errors
                _disposeMethod?.Invoke(_instance, null);
            }

            public void Start()
            {
                _startMethod?.Invoke(_instance, null);
            }

            public void Stop()
            {
                _stopMethod?.Invoke(_instance, null);
            }

            public void SetDutyCycle(double duty)
            {
                if (_dutyProp == null) throw new InvalidOperationException("DutyCycle property not found on PwmChannel type");
                _dutyProp.SetValue(_instance, duty);
            }
        }
    }
}
