using System.ComponentModel;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

[Page("po-equipment")]
public class PoEquipmentViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    public PoEquipmentViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public PoEquipmentViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }
}

public partial class PurchaseOrderProductItem : ObservableValidator
{
    [ObservableProperty] 
    private string? _poNumber;
    
    [ObservableProperty] 
    private string? _itemId;
    
    [ObservableProperty] 
    private string? _itemName;
    
    [ObservableProperty] 
    private string? _unitsOfMeasures;
    
    [ObservableProperty] 
    private string? _category;
    
    [ObservableProperty] 
    private string? _batchCode;
    
    [ObservableProperty] 
    private decimal? _suppliersPrice;
    
    [ObservableProperty] 
    private int? _quantity;
    
    [ObservableProperty] 
    private int? _quantityReceived;
}