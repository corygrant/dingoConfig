using domain.Interfaces;
using domain.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using web.Components.Dialogs;

namespace web.Components.Devices.dingoPdm;

public abstract class PdmFunctionComponentBase<TDevice> : ComponentBase
    where TDevice : IDeviceConfigurable
{
    [Parameter, EditorRequired] public TDevice Device { get; set; } = default!;
    [Inject] protected IDialogService DialogService { get; set; } = null!;

    protected async Task OpenVariableSelectorAsync(string[] datatypes, Action<int> setter)
    {
        var parameters = new DialogParameters<VarMapSelectionDialog>
        {
            { x => x.Device, Device },
            { x => x.Datatypes, datatypes }
        };

        var dialog = await DialogService.ShowAsync<VarMapSelectionDialog>("Select Variable", parameters);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: (DeviceVariable variable) })
            setter(variable.VariableIndex);
    }
    
    protected string GetSelectedVarText(int index)
    {
        var variable = Device.VarMap.Find(p => p.VariableIndex == index);

        if (variable == null) return "Not found";

        var name = variable.GetName();
        
        return name.Length == 0 ? "Select Variable" : $"{name} - {variable.PropertyName}";
    }

    protected int GetSelectedVarLen(int index)
    {
        var variable = Device.VarMap.Find(p => p.VariableIndex == index);

        if (variable == null) return 0;

        return variable.DataType switch
        {
            "bool" => 1,
            "int" => 16,
            "float" => 32,
            _ => 0
        };
    }
}
