using domain.Enums;
using domain.Models;
using Microsoft.Extensions.Logging;

namespace domain.Common;

/// <summary>
/// Parser for DBC (CAN Database) files
/// </summary>
public static class DbcParser
{
    /// <summary>
    /// Parses a DBC file and extracts signal definitions
    /// </summary>
    /// <param name="filePath">Path to the DBC file</param>
    /// <param name="logger">Optional logger for diagnostic messages</param>
    /// <returns>List of parsed DbcProperty objects</returns>
    public static List<DbcProperty> ParseFile(string filePath, ILogger? logger = null)
    {
        var properties = new List<DbcProperty>();

        if (string.IsNullOrEmpty(filePath))
        {
            logger?.LogError("DBC file path is empty");
            return properties;
        }

        if (!File.Exists(filePath))
        {
            logger?.LogError("DBC file not found at {Path}", filePath);
            return properties;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            int currentMessageId = 0;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Parse message definitions: BO_ <MessageID> <MessageName>: <MessageSize> <SendingNode>
                if (trimmedLine.StartsWith("BO_ "))
                {
                    var messageParts = trimmedLine.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (messageParts.Length >= 3 && int.TryParse(messageParts[1], out var messageId))
                    {
                        currentMessageId = messageId;
                    }
                }
                // Parse signal definitions: SG_ <SignalName> : <StartBit>|<Length>@<ByteOrder><IsSigned> (<Factor>,<Offset>) [<Min>|<Max>] "<Unit>" <ReceivingNodes>
                else if (trimmedLine.StartsWith("SG_ "))
                {
                    try
                    {
                        var dbcProp = ParseSignalLine(trimmedLine, currentMessageId);
                        if (dbcProp != null)
                        {
                            properties.Add(dbcProp);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Failed to parse signal line: {Line}", trimmedLine);
                    }
                }
            }

            logger?.LogInformation("Parsed {Count} signals from DBC file", properties.Count);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to parse DBC file at {Path}", filePath);
        }

        return properties;
    }

    /// <summary>
    /// Parses a single signal definition line from a DBC file
    /// </summary>
    /// <param name="line">Signal line to parse</param>
    /// <param name="messageId">ID of the parent message</param>
    /// <returns>DbcProperty if parsing succeeds, null otherwise</returns>
    private static DbcProperty? ParseSignalLine(string line, int messageId)
    {
        // Example: SG_ Speed : 0|16@1+ (0.1,0) [0|655.35] "km/h" ECU2
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 4) return null;

        var signalName = parts[1];
        var signalDef = parts[3]; // Format: <StartBit>|<Length>@<ByteOrder><IsSigned>

        // Parse start bit and length: "0|16@1+"
        var bitParts = signalDef.Split('|');
        if (bitParts.Length != 2 || !int.TryParse(bitParts[0], out var startBit))
            return null;

        var lengthAndFormat = bitParts[1].Split('@');
        if (lengthAndFormat.Length != 2 || !int.TryParse(lengthAndFormat[0], out var length))
            return null;

        // Parse byte order and signed: "1+" or "0-"
        var format = lengthAndFormat[1];
        if (format.Length < 2) return null;

        var byteOrder = format[0] == '1' ? ByteOrder.BigEndian : ByteOrder.LittleEndian;
        var isSigned = format[1] == '-';

        // Parse factor and offset: "(0.1,0)"
        double factor = 1.0;
        double offset = 0.0;

        if (parts.Length > 4)
        {
            var factorOffset = string.Join("", parts.Skip(4).TakeWhile(p => !p.StartsWith("[")));
            factorOffset = factorOffset.Trim('(', ')');
            var factorOffsetParts = factorOffset.Split(',');

            if (factorOffsetParts.Length == 2)
            {
                double.TryParse(factorOffsetParts[0], out factor);
                double.TryParse(factorOffsetParts[1], out offset);
            }
        }

        return new DbcProperty(signalName)
        {
            Id = messageId,
            StartBit = startBit,
            Length = length,
            ByteOrder = byteOrder,
            IsSigned = isSigned,
            Factor = factor,
            Offset = offset
        };
    }
}
