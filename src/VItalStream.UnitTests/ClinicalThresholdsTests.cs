using VitalStream.Shared.Domain;
using VitalStream.Shared.Enums;

namespace VItalStream.UnitTests;

public class ClinicalThresholdsTests
{
    // HeartRate
    [TestCase(39,  VitalSeverity.Critical)]
    [TestCase(151, VitalSeverity.Critical)]
    [TestCase(59,  VitalSeverity.Warning)]
    [TestCase(101, VitalSeverity.Warning)]
    [TestCase(70,  VitalSeverity.Normal)]
    public void HeartRate_ReturnsExpectedSeverity(double value, VitalSeverity expected)
    {
        Assert.That(ClinicalThresholds.Evaluate(VitalType.HeartRate, value), Is.EqualTo(expected));
    }

    // SpO2
    [TestCase(89,  VitalSeverity.Critical)]
    [TestCase(94,  VitalSeverity.Warning)]
    [TestCase(98,  VitalSeverity.Normal)]
    public void SpO2_ReturnsExpectedSeverity(double value, VitalSeverity expected)
    {
        Assert.That(ClinicalThresholds.Evaluate(VitalType.SpO2, value), Is.EqualTo(expected));
    }

    // BloodPressureSystolic
    [TestCase(181, VitalSeverity.Critical)]
    [TestCase(69,  VitalSeverity.Critical)]
    [TestCase(141, VitalSeverity.Warning)]
    [TestCase(89,  VitalSeverity.Warning)]
    [TestCase(110, VitalSeverity.Normal)]
    public void BloodPressureSystolic_ReturnsExpectedSeverity(double value, VitalSeverity expected)
    {
        Assert.That(ClinicalThresholds.Evaluate(VitalType.BloodPressureSystolic, value), Is.EqualTo(expected));
    }

    // Temperature
    [TestCase(40.1, VitalSeverity.Critical)]
    [TestCase(34.9, VitalSeverity.Critical)]
    [TestCase(38.1, VitalSeverity.Warning)]
    [TestCase(35.9, VitalSeverity.Warning)]
    [TestCase(37.0, VitalSeverity.Normal)]
    public void Temperature_ReturnsExpectedSeverity(double value, VitalSeverity expected)
    {
        Assert.That(ClinicalThresholds.Evaluate(VitalType.Temperature, value), Is.EqualTo(expected));
    }

    [Test]
    public void UnknownVitalType_ReturnsNormal()
    {
        Assert.That(ClinicalThresholds.Evaluate(VitalType.BloodPressureDiastolic, 80), Is.EqualTo(VitalSeverity.Normal));
    }
}