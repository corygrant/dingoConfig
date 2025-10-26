using domain.Models;

namespace domain.Events;

public class CanDataEventArgs(CanData data) : EventArgs
{
    public CanData Data { get; private set; } = data;
}