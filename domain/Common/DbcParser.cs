using System.Text.RegularExpressions;
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
    public static List<DbcSignal> ParseFile(string filePath, ILogger? logger = null)
    {
        var properties = new List<DbcSignal>();

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
                // Parse value tables: VAL_ <MessageID> <SignalName> <Value> "<Text>" ... ;
                else if (trimmedLine.StartsWith("VAL_ "))
                {
                    var valParts = trimmedLine.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
                    if (valParts.Length < 4 || !int.TryParse(valParts[1], out var valMsgId)) continue;

                    var signal = properties.FirstOrDefault(p => p.Id == valMsgId && p.Name == valParts[2]);
                    if (signal == null) continue;

                    signal.ValueTable = new Dictionary<long, string>();
                    foreach (Match m in Regex.Matches(valParts[3], @"(-?\d+)\s+""([^""]*)"""))
                        signal.ValueTable[long.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
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
    private static DbcSignal? ParseSignalLine(string line, int messageId)
    {
        // Example: SG_ Speed : 0|16@1+ (0.1,0) [0|655.35] "km/h" ECU2
        // Multiplexed: SG_ Frame_ID M : ... (multiplexor) / SG_ RPM m0 : ... (multiplexed)
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        var colonIdx = Array.IndexOf(parts, ":");
        if (colonIdx < 2 || colonIdx + 1 >= parts.Length) return null;

        var signalName = parts[1];
        var signalDef = parts[colonIdx + 1]; // Format: <StartBit>|<Length>@<ByteOrder><IsSigned>

        var isMultiplexor = false;
        int? multiplexValue = null;
        if (colonIdx == 3)
        {
            // "M", "m<n>", or extended "m<n>M"
            var mux = parts[2];
            isMultiplexor = mux.EndsWith('M');
            if (mux.StartsWith('m') && int.TryParse(mux.AsSpan(1, mux.Length - (isMultiplexor ? 2 : 1)), out var muxVal))
                multiplexValue = muxVal;
        }

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

        var byteOrder = format[0] == '1' ? ByteOrder.LittleEndian : ByteOrder.BigEndian;
        var isSigned = format[1] == '-';

        // Parse factor and offset: "(0.1,0)"
        double factor = 1.0;
        double offset = 0.0;
        double min = 0.0;
        double max = 0.0;
        string unit = "";

        // Find and parse factor/offset in parentheses
        var factorStartIdx = line.IndexOf('(');
        var factorEndIdx = line.IndexOf(')');
        if (factorStartIdx >= 0 && factorEndIdx > factorStartIdx)
        {
            var factorOffset = line.Substring(factorStartIdx + 1, factorEndIdx - factorStartIdx - 1);
            var factorOffsetParts = factorOffset.Split(',');

            if (factorOffsetParts.Length == 2)
            {
                double.TryParse(factorOffsetParts[0], out factor);
                double.TryParse(factorOffsetParts[1], out offset);
            }
        }

        // Find and parse min/max in brackets: [<Min>|<Max>]
        var minMaxStartIdx = line.IndexOf('[');
        var minMaxEndIdx = line.IndexOf(']');
        if (minMaxStartIdx >= 0 && minMaxEndIdx > minMaxStartIdx)
        {
            var minMaxStr = line.Substring(minMaxStartIdx + 1, minMaxEndIdx - minMaxStartIdx - 1);
            var minMaxParts = minMaxStr.Split('|');

            if (minMaxParts.Length == 2)
            {
                double.TryParse(minMaxParts[0], out min);
                double.TryParse(minMaxParts[1], out max);
            }
        }

        // Find and parse unit in quotes: "<Unit>"
        var unitStartIdx = line.IndexOf('"');
        var unitEndIdx = line.LastIndexOf('"');
        if (unitStartIdx >= 0 && unitEndIdx > unitStartIdx)
        {
            unit = line.Substring(unitStartIdx + 1, unitEndIdx - unitStartIdx - 1);
        }

        return new DbcSignal()
        {
            Name = signalName,
            Id = messageId,
            StartBit = startBit,
            Length = length,
            ByteOrder = byteOrder,
            IsSigned = isSigned,
            Factor = factor,
            Offset = offset,
            Min = min,
            Max = max,
            Unit = unit,
            IsMultiplexor = isMultiplexor,
            MultiplexValue = multiplexValue
        };
    }
}
