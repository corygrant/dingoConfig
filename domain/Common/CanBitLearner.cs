using domain.Models;

namespace domain.Common;

/// <summary>A bit that followed the user's switch while the learner recorded.</summary>
/// <param name="Bit">Little-endian start bit: byte * 8 + bit in byte.</param>
/// <param name="ActiveHigh">The bit reads 1 when the switch is on.</param>
/// <param name="Changes">Edges seen while recording.</param>
/// <param name="PeriodMs">Typical gap between frames on this ID, 0 when it only sends on change.</param>
public record CanBitCandidate(int Id, bool Extended, int Bit, bool ActiveHigh, int Changes, int PeriodMs)
{
    public int Byte => Bit / 8;
    public int BitInByte => Bit % 8;

    /// <summary>When the bit last moved, to tell when the user is done.</summary>
    public DateTime LastChange { get; init; }
}

/// <summary>
/// Finds the CAN bit a switch drives. While the user keeps still it learns which
/// bits change on their own (counters, analog values); then, while the user works
/// the switch, every other bit that changes is a candidate. Bits that flip faster
/// than a hand can work a switch are left out as noise.
///
/// A message that is only sent while something is active (a button module that
/// wakes on a press) is first heard while recording, so the keep-still part has
/// not seen its counter, and its first frame already has the switch on. For those
/// the state the message ends in is taken as the rest state, and counters are
/// told apart by how they move: the lowest bit flips on almost every frame and
/// each bit above it half as often.
/// </summary>
public sealed class CanBitLearner
{
    public enum Phase { Baseline, Recording, Done }

    private const int MinStableMs = 60;
    // A hand works a switch a few times while recording; more than this, on more
    // than every other frame, is a counter or a value
    private const int MaxHandChanges = 6;

    private sealed class IdTrack
    {
        public byte[] Last = [];
        public byte[]? AtRecordStart;
        // Not heard while keeping still: rest is where the message ends, not where it starts
        public bool Newcomer;
        public bool Extended;
        public int Frames;
        public int RecordFrames;
        public DateTime FirstSeen;
        public DateTime LastSeen;
        public readonly HashSet<int> NoisyBits = [];
        public readonly Dictionary<int, BitTrack> Bits = new();
    }

    private sealed class BitTrack
    {
        public int Changes;
        public DateTime LastChange;
        public double MinStableMs = double.MaxValue;
    }

    private readonly object _lock = new();
    private readonly Dictionary<int, IdTrack> _ids = new();
    private readonly Func<int, bool> _accept;

    public CanBitLearner(Func<int, bool> accept) => _accept = accept;

    public Phase Current { get; private set; } = Phase.Baseline;
    public int FramesSeen { get; private set; }
    public int IdsSeen { get { lock (_lock) return _ids.Count; } }

    public void Add(CanFrame frame) => Add(frame, DateTime.UtcNow);

    public void Add(CanFrame frame, DateTime now)
    {
        if (Current == Phase.Done || !_accept(frame.Id))
            return;

        lock (_lock)
        {
            FramesSeen++;
            if (!_ids.TryGetValue(frame.Id, out var track))
            {
                _ids[frame.Id] = track = new IdTrack
                {
                    Last = frame.Payload.ToArray(),
                    Extended = frame.Id > 0x7FF,
                    FirstSeen = now
                };
                if (Current == Phase.Recording)
                {
                    track.AtRecordStart = track.Last.ToArray();
                    track.Newcomer = true;
                }
            }
            else
            {
                var length = Math.Min(track.Last.Length, frame.Payload.Length);
                for (var b = 0; b < length; b++)
                {
                    var diff = track.Last[b] ^ frame.Payload[b];
                    if (diff == 0)
                        continue;

                    for (var i = 0; i < 8; i++)
                        if ((diff & (1 << i)) != 0)
                            Edge(track, b * 8 + i, now);
                }
                track.Last = frame.Payload.ToArray();
            }

            track.Frames++;
            if (Current == Phase.Recording)
                track.RecordFrames++;
            track.LastSeen = now;
        }
    }

    private void Edge(IdTrack track, int bit, DateTime now)
    {
        if (Current == Phase.Baseline)
        {
            track.NoisyBits.Add(bit);
            return;
        }

        if (!track.Bits.TryGetValue(bit, out var b))
            track.Bits[bit] = b = new BitTrack();
        else
            b.MinStableMs = Math.Min(b.MinStableMs, (now - b.LastChange).TotalMilliseconds);

        b.Changes++;
        b.LastChange = now;
    }

    /// <summary>Ends the keep-still part; from here on changes are the user's.</summary>
    public void StartRecording()
    {
        lock (_lock)
        {
            foreach (var track in _ids.Values)
                track.AtRecordStart = track.Last.ToArray();
            Current = Phase.Recording;
        }
    }

    public void Stop() => Current = Phase.Done;

    /// <summary>Best first: back where they started (switched on and off again), then the most edges.</summary>
    public IReadOnlyList<CanBitCandidate> Candidates(int max = 5)
    {
        lock (_lock)
        {
            var list = new List<(CanBitCandidate Candidate, bool Returned)>();
            foreach (var (id, track) in _ids)
            {
                var period = track.Frames > 2
                    ? (int)((track.LastSeen - track.FirstSeen).TotalMilliseconds / (track.Frames - 1))
                    : 0;

                foreach (var (bit, b) in track.Bits)
                {
                    if (track.NoisyBits.Contains(bit) || b.MinStableMs < MinStableMs)
                        continue;

                    // A lower bit of the same byte moving on its own, or more often than
                    // this one, makes the byte a number (a counter, an analog value), and
                    // its upper bits only look quiet because they change slowly
                    var byteStart = bit / 8 * 8;
                    if (Enumerable.Range(byteStart, bit - byteStart).Any(lower =>
                            track.NoisyBits.Contains(lower) ||
                            (track.Bits.TryGetValue(lower, out var l) && l.Changes > b.Changes)))
                        continue;

                    // Flipping on most frames is a counter's lowest bit, not a hand
                    if (b.Changes > Math.Max(MaxHandChanges, track.RecordFrames / 2))
                        continue;

                    var changes = b.Changes;
                    bool rest;
                    if (track.Newcomer)
                    {
                        // Sent only once the switch was worked: it ends at rest, and when it
                        // started with the switch on, the edge to on happened before its
                        // first frame
                        rest = IsSet(track.Last, bit);
                        if (IsSet(track.AtRecordStart, bit) != rest)
                            changes++;
                    }
                    else
                        rest = IsSet(track.AtRecordStart, bit);

                    list.Add((new CanBitCandidate(id, track.Extended, bit, !rest, changes, period) { LastChange = b.LastChange },
                              changes % 2 == 0));
                }
            }

            // Switched on and off again means back where it started. When anything
            // did that, a bit that only moved one way is a coincidence
            if (list.Any(c => c.Returned))
                list = list.Where(c => c.Returned).ToList();

            return list
                .OrderByDescending(c => c.Returned)
                .ThenByDescending(c => c.Candidate.Changes)
                .ThenBy(c => c.Candidate.Id)
                .ThenBy(c => c.Candidate.Bit)
                .Take(max)
                .Select(c => c.Candidate)
                .ToList();
        }
    }

    private static bool IsSet(byte[]? payload, int bit) =>
        payload != null && bit / 8 < payload.Length && (payload[bit / 8] & (1 << (bit % 8))) != 0;

    /// <summary>The candidate's state in a frame, null when the frame is not its message.</summary>
    public static bool? Read(CanBitCandidate candidate, CanFrame frame)
    {
        if (frame.Id != candidate.Id || candidate.Byte >= frame.Payload.Length)
            return null;

        var set = (frame.Payload[candidate.Byte] & (1 << candidate.BitInByte)) != 0;
        return set == candidate.ActiveHigh;
    }

    /// <summary>"640-64F, 6F0" -> a filter; empty accepts everything.</summary>
    public static Func<int, bool> ParseFilter(string? text)
    {
        var ranges = new List<(int From, int To)>();
        foreach (var part in (text ?? "").Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var ends = part.Split('-', 2);
            if (TryHex(ends[0], out var from))
            {
                var to = from;
                if (ends.Length == 2 && !TryHex(ends[1], out to))
                    continue;
                ranges.Add((Math.Min(from, to), Math.Max(from, to)));
            }
        }

        return ranges.Count == 0 ? _ => true : id => ranges.Any(r => id >= r.From && id <= r.To);
    }

    private static bool TryHex(string text, out int value)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            text = text[2..];
        return int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out value);
    }
}
