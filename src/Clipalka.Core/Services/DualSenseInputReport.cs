using System.Buffers.Binary;

namespace Clipalka.Core.Services;

// Sony full input format (USB 0x01 / Bluetooth 0x31), as documented by
// drivers/hid/hid-playstation.c in the Linux kernel. No output/feature writes.
public static class DualSenseInputReport
{
    public static bool TryRead(ReadOnlySpan<byte> report, out int[] inputs)
    {
        inputs = [];
        int offset;
        if (report.Length == 78 && report[0] == 0x31)
        {
            uint crc = UpdateCrc(uint.MaxValue, 0xA1);
            foreach (var value in report[..^4]) crc = UpdateCrc(crc, value);
            if (~crc != BinaryPrimitives.ReadUInt32LittleEndian(report[^4..])) return false;
            offset = 2;
        }
        else if (report.Length == 64 && report[0] == 0x01) offset = 1;
        else return false;

        var buttons = report.Slice(offset + 7, 3);
        var pressed = new List<int>();
        for (var bit = 4; bit < 8; bit++) if ((buttons[0] & (1 << bit)) != 0) pressed.Add(bit - 4);
        for (var bit = 0; bit < 8; bit++) if ((buttons[1] & (1 << bit)) != 0) pressed.Add(bit + 4);
        for (var bit = 0; bit < 3; bit++) if ((buttons[2] & (1 << bit)) != 0) pressed.Add(bit + 12);
        var hat = buttons[0] & 0xF;
        if (hat < 8) pressed.Add(0x10000 + hat);
        inputs = pressed.ToArray();
        return true;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
        return crc;
    }
}
