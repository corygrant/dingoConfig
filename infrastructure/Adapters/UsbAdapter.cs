using domain.Enums;

namespace infrastructure.Adapters;

public class UsbAdapter : SerialAdapter
{
    public override string Name => "USB";
    
}