using api.Models;

namespace api.Models;

public class CanDataEventArgs(CanData data) : EventArgs
{
    public CanData Data { get; private set; } = data;
}