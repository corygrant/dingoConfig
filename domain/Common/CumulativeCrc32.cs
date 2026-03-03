namespace domain.Common;

public class CumulativeCrc32
{
    private uint _crc = 0xFFFFFFFF;

    public void Update(byte[] data)
    {
        _crc = ComputePartial(data, _crc);
    }

    public uint Final => ~_crc;

    public void Reset() => _crc = 0xFFFFFFFF;

    private static uint ComputePartial(byte[] data, uint current)
    {
        uint crc = current;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int j = 0; j < 8; j++)
                crc = (crc >> 1) ^ (0xEDB88320u * (crc & 1));
        }
        return crc; // no final XOR
    }
}