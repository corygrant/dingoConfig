using domain.Models;

namespace domain.Models;

public class CanFrameEventArgs(CanFrame frame) : EventArgs
{
    public CanFrame Frame { get; private set; } = frame;
}