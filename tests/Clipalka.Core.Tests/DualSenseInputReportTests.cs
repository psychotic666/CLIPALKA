using Clipalka.Core.Services;

namespace Clipalka.Core.Tests;

public sealed class DualSenseInputReportTests
{
    // CRC generated independently with zlib.crc32 over 0xA1 + the report body.
    private static byte[] BluetoothChord() => Convert.FromHexString(
        "31007F7F7F7F000000280100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000007BEDB8C");

    [Fact]
    public void FullBluetoothReport_ReadsCrossAndL1()
    {
        Assert.True(DualSenseInputReport.TryRead(BluetoothChord(), out var buttons));
        Assert.Equal(new[] { 1, 4 }, buttons);
    }

    [Fact]
    public void CorruptedBluetoothPacket_CannotCreateAButtonPress()
    {
        var report = BluetoothChord();
        report[9] ^= 0x10;
        Assert.False(DualSenseInputReport.TryRead(report, out var buttons));
        Assert.Empty(buttons);
    }

    [Fact]
    public void UsbReport_UsesItsOwnOffsetAndPreservesDpadAndSystemButtons()
    {
        var report = new byte[64];
        report[0] = 1;
        report[8] = 2; // right D-pad
        report[9] = 0x20; // Options
        report[10] = 0x03; // PS + touchpad
        Assert.True(DualSenseInputReport.TryRead(report, out var buttons));
        Assert.Equal(new[] { 9, 12, 13, 0x10002 }, buttons);
        report[8] = 8; report[9] = report[10] = 0;
        Assert.True(DualSenseInputReport.TryRead(report, out buttons));
        Assert.Empty(buttons);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 1)]
    [InlineData(77, 0x31)]
    [InlineData(79, 0x31)]
    [InlineData(64, 0x02)]
    public void UnknownOrTruncatedReport_IsRejected(int length, byte id)
    {
        var report = new byte[length];
        if (length > 0) report[0] = id;
        Assert.False(DualSenseInputReport.TryRead(report, out var buttons));
        Assert.Empty(buttons);
    }
}
