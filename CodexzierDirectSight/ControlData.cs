using System.Text.Json;

namespace CodexzierDirectSight;

/// <summary>
/// Datenobjekt, das über das TCP-Protokoll gesendet/empfangen wird.
/// Enthält zwei Servo-Werte und ein Textfeld. Bietet einfache JSON-Serialisierung.
/// </summary>
public class ControlData
{
    public int Servo1 { get; set; }
    public int Servo2 { get; set; }
    public string? Text { get; set; }

    public string Serialize()
    {
        return JsonSerializer.Serialize(this);
    }

    public static ControlData? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ControlData>(json);
    }
}