# CodexzierDirectSight — Raspberry Pi Setup (Pi 4 B)

Kurzanleitung, um das Projekt auf einem Raspberry Pi 4 Model B mit PWM-Unterstützung zum Laufen zu bringen.

Voraussetzungen auf dem Pi
- Raspberry Pi OS (Bullseye/Bookworm empfohlen) oder eine andere moderne Distribution.
- Aktiviere PWM bzw. Overlay falls nötig (siehe unten).

1) NuGet-Pakete (auf deinem Entwicklungsrechner bzw. Pi):

```powershell
cd 'D:\Source\CSharp\Services\CodexzierDirectSight'
# Installiere System.Device.Gpio / PWM Unterstützung
dotnet add package System.Device.Gpio
dotnet add package System.Device.Pwm
# Optional: zusätzliche Bindings
dotnet add package Iot.Device.Bindings
```

2) Device-Tree / PWM aktivieren (Beispiel für Pi 4):
- Prüfe aktuelle PWM-Geräte:

```bash
# Auf dem Pi
ls /sys/class/pwm || echo "no pwm sysfs"
```

- Falls nötig, aktiviere overlay in `/boot/config.txt` (Beispiel):

```
# in /boot/config.txt
dtoverlay=pwm-2chan
# optional: dtoverlay=gpio-fan,gpiopin=18 (nur Beispiel)
```

- Reboot nach Änderung: `sudo reboot`.

3) PWM-Pin / Channel Mapping prüfen
- Nach dem Aktivieren findest du pwmchips unter `/sys/class/pwm/pwmchip0`, `pwmchip1`, ...
- Welche GPIOs auf welche pwmchip/channel gemappt sind, hängt vom Overlay ab.
- Nutze `raspi-gpio` oder `dtoverlay` Doku um das Mapping zu ermitteln.

4) Build / Publish (auf dem Pi oder Cross-compile):

```powershell
cd 'D:\Source\CSharp\Services\CodexzierDirectSight'
# Mac/Windows: debug build
dotnet build

# Publish für Raspberry Pi (linux-arm64 oder linux-arm depending on runtime)
dotnet publish -c Release -r linux-arm64 --self-contained false -o ./publish
```

5) Run

```bash
cd publish
# ggf. setze Ausführungsrechte
./CodexzierDirectSight
```

6) Logging / Debug
- Das Programm nutzt ILogger; prüfe systemd-Logs oder Konsole.

Hinweis
- Die Implementierung verwendet Reflection, um beim Entwickeln auf Nicht-Pi-Systemen zu kompilieren (No-op-Fallback). Auf dem Pi installiere die oben genannten Pakete, damit echte PWM-Kanäle verwendet werden.

Wenn du möchtest, überprüfe ich für dich das genaue PWM-Mapping auf deinem Pi 4 B — nenne mir das verwendete OS-Image (z. B. "Raspberry Pi OS Bullseye") und ich helfe beim Ermitteln der richtigen `chip`/`channel` Werte und dtoverlay-Einträgen.

