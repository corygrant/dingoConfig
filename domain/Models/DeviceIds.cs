namespace domain.Models;

public class DeviceIds(int baseId, int paramTx, int paramRx)
{
    public int Base { get; set; } = baseId;
    public int ParamTx { get; set; } = paramTx;
    public int ParamRx { get; set; } = paramRx;
}