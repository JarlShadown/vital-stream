using VitalStream.Shared.Enums;

namespace VitalStream.Shared.Domain;

public static class ClinicalThresholds
{
    public static VitalSeverity Evaluate(VitalType type, double value) => type switch
    {
        VitalType.HeartRate => value switch
        {
            < 40 or > 150 => VitalSeverity.Critical,
            < 60 or > 100 => VitalSeverity.Warning,
            _              => VitalSeverity.Normal
        },
        VitalType.SpO2 => value switch
        {
            < 90 => VitalSeverity.Critical,
            < 95 => VitalSeverity.Warning,
            _    => VitalSeverity.Normal
        },
        VitalType.BloodPressureSystolic => value switch
        {
            > 180 or < 70 => VitalSeverity.Critical,
            > 140 or < 90 => VitalSeverity.Warning,
            _              => VitalSeverity.Normal
        },
        VitalType.Temperature => value switch
        {
            > 40.0 or < 35.0 => VitalSeverity.Critical,
            > 38.0 or < 36.0 => VitalSeverity.Warning,
            _                 => VitalSeverity.Normal
        },
        _ => VitalSeverity.Normal
    }; 
}