using domain.Enums;
using domain.Models;

namespace domain.Common;

public class DbcSignalCodec
{

    #region Decoding (Extract)

    /// <summary>
    /// Extracts a signal value from CAN data using a DBC property
    /// </summary>
    public static double ExtractSignal(byte[] data, DbcProperty dbcProp)
    {
        return ExtractSignal(data, dbcProp.StartBit, dbcProp.Length, dbcProp.ByteOrder,  dbcProp.IsSigned, dbcProp.Factor, dbcProp.Offset);
    }
    
    /// <summary>
    /// Extracts a signal value from CAN data using DBC parameters
    /// </summary>
    public static double ExtractSignal(
        byte[] data,
        int startBit,
        int length,
        ByteOrder byteOrder = ByteOrder.LittleEndian,
        bool isSigned = false,
        double factor = 1.0,
        double offset = 0.0)
    {
        if (length > 64 || length < 1)
            throw new ArgumentException("Length must be between 1 and 64 bits");

        ulong rawValue = 0;

        if (byteOrder == ByteOrder.LittleEndian)
        {
            // Intel/Little Endian: start bit is the LSB
            int startByte = startBit / 8;
            int startBitInByte = startBit % 8;
            int bitsRemaining = length;
            int currentBit = 0;

            for (int i = startByte; i < data.Length && bitsRemaining > 0; i++)
            {
                int bitsToRead = Math.Min(8 - (i == startByte ? startBitInByte : 0), bitsRemaining);
                int shift = i == startByte ? startBitInByte : 0;
                
                ulong mask = ((1UL << bitsToRead) - 1) << shift;
                ulong bits = (ulong)((data[i] & mask) >> shift);
                
                rawValue |= bits << currentBit;
                currentBit += bitsToRead;
                bitsRemaining -= bitsToRead;
            }
        }
        else // BigEndian (Motorola)
        {
            // Motorola/Big Endian: start bit is the MSB
            int startByte = startBit / 8;
            int startBitInByte = 7 - (startBit % 8);
            int bitsRemaining = length;
            int currentBit = length - 1;

            for (int byteIdx = startByte; byteIdx < data.Length && bitsRemaining > 0; byteIdx++)
            {
                int bitsInThisByte = byteIdx == startByte ? startBitInByte + 1 : 8;
                int bitsToRead = Math.Min(bitsInThisByte, bitsRemaining);
                int shift = bitsInThisByte - bitsToRead;
                
                ulong mask = ((1UL << bitsToRead) - 1) << shift;
                ulong bits = (ulong)((data[byteIdx] & mask) >> shift);
                
                rawValue |= bits << (currentBit - bitsToRead + 1);
                currentBit -= bitsToRead;
                bitsRemaining -= bitsToRead;
            }
        }

        // Handle signed values using two's complement
        double value;
        if (isSigned)
        {
            long signedValue;
            ulong signBitMask = 1UL << (length - 1);
            
            if ((rawValue & signBitMask) != 0)
            {
                // Negative number - extend sign
                ulong mask = (1UL << length) - 1;
                signedValue = (long)(rawValue | ~mask);
            }
            else
            {
                signedValue = (long)rawValue;
            }
            value = signedValue;
        }
        else
        {
            value = rawValue;
        }

        // Apply scale and offset
        return (value * factor) + offset;
    }

    /// <summary>
    /// Extract signal as integer (no scaling/offset)
    /// </summary>
    public static long ExtractSignalInt(
        byte[] data,
        int startBit,
        int length,
        ByteOrder byteOrder = ByteOrder.LittleEndian,
        bool isSigned = false,
        double factor = 1.0,
        double offset = 0.0)
    {
        return (long)ExtractSignal(data, startBit, length, byteOrder, isSigned, factor, offset);
    }

    #endregion

    #region Encoding (Insert)

    /// <summary>
    /// Inserts a signal value into CAN data using DBC parameters
    /// </summary>
    /// <param name="data">CAN data byte array to modify</param>
    /// <param name="value">Physical value to encode</param>
    /// <param name="startBit">Start bit position (0-63 for 8-byte CAN frame)</param>
    /// <param name="length">Signal length in bits</param>
    /// <param name="byteOrder">Little or Big endian</param>
    /// <param name="isSigned">True if value should be interpreted as signed</param>
    /// <param name="factor">Scaling factor (default 1.0)</param>
    /// <param name="offset">Offset to subtract before scaling (default 0.0)</param>
    public static void InsertSignal(
        byte[] data,
        double value,
        int startBit,
        int length,
        ByteOrder byteOrder = ByteOrder.LittleEndian,
        bool isSigned = false,
        double factor = 1.0,
        double offset = 0.0)
    {
        if (length > 64 || length < 1)
            throw new ArgumentException("Length must be between 1 and 64 bits");

        // Reverse the scaling: raw = (value - offset) / factor
        double scaledValue = (value - offset) / factor;
        long rawValue = (long)Math.Round(scaledValue);

        // Convert to unsigned representation
        ulong unsignedRawValue;
        if (isSigned)
        {
            // For signed values, mask to the specified bit length
            ulong mask = (1UL << length) - 1;
            unsignedRawValue = (ulong)rawValue & mask;
        }
        else
        {
            unsignedRawValue = (ulong)rawValue;
        }

        // Ensure value fits in the specified number of bits
        ulong maxValue = (1UL << length) - 1;
        if (unsignedRawValue > maxValue)
        {
            throw new ArgumentException($"Value {value} exceeds maximum for {length} bits");
        }

        if (byteOrder == ByteOrder.LittleEndian)
        {
            // Intel/Little Endian: start bit is the LSB
            int startByte = startBit / 8;
            int startBitInByte = startBit % 8;
            int bitsRemaining = length;
            int currentBit = 0;

            for (int i = startByte; i < data.Length && bitsRemaining > 0; i++)
            {
                int bitsToWrite = Math.Min(8 - (i == startByte ? startBitInByte : 0), bitsRemaining);
                int shift = i == startByte ? startBitInByte : 0;
                
                // Create mask for bits we're writing
                byte bitMask = (byte)(((1 << bitsToWrite) - 1) << shift);
                
                // Extract the bits to write from rawValue
                byte bitsToInsert = (byte)(((unsignedRawValue >> currentBit) & ((1UL << bitsToWrite) - 1)) << shift);
                
                // Clear the bits we're about to write, then OR in the new bits
                data[i] = (byte)((data[i] & ~bitMask) | bitsToInsert);
                
                currentBit += bitsToWrite;
                bitsRemaining -= bitsToWrite;
            }
        }
        else // BigEndian (Motorola)
        {
            // Motorola/Big Endian: start bit is the MSB
            int startByte = startBit / 8;
            int startBitInByte = 7 - (startBit % 8);
            int bitsRemaining = length;
            int currentBit = length - 1;

            for (int byteIdx = startByte; byteIdx < data.Length && bitsRemaining > 0; byteIdx++)
            {
                int bitsInThisByte = byteIdx == startByte ? startBitInByte + 1 : 8;
                int bitsToWrite = Math.Min(bitsInThisByte, bitsRemaining);
                int shift = bitsInThisByte - bitsToWrite;
                
                // Create mask for bits we're writing
                byte bitMask = (byte)(((1 << bitsToWrite) - 1) << shift);
                
                // Extract the bits to write from rawValue
                byte bitsToInsert = (byte)(((unsignedRawValue >> (currentBit - bitsToWrite + 1)) & ((1UL << bitsToWrite) - 1)) << shift);
                
                // Clear the bits we're about to write, then OR in the new bits
                data[byteIdx] = (byte)((data[byteIdx] & ~bitMask) | bitsToInsert);
                
                currentBit -= bitsToWrite;
                bitsRemaining -= bitsToWrite;
            }
        }
    }

    /// <summary>
    /// Insert signal from integer value (no scaling/offset)
    /// </summary>
    public static void InsertSignalInt(
        byte[] data,
        long value,
        int startBit,
        int length,
        ByteOrder byteOrder = ByteOrder.LittleEndian,
        bool isSigned = false)
    {
        InsertSignal(data, value, startBit, length, byteOrder, isSigned, 1.0, 0.0);
    }

    /// <summary>
    /// Insert boolean signal (1 bit)
    /// </summary>
    public static void InsertBool(
        byte[] data,
        bool value,
        int startBit,
        ByteOrder byteOrder = ByteOrder.LittleEndian)
    {
        InsertSignalInt(data, value ? 1 : 0, startBit, 1, byteOrder, false);
    }

    #endregion
}