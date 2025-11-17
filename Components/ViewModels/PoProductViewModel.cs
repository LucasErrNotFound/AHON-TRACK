using System.ComponentModel;
using AHON_TRACK.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using HotAvalonia;
using ShadUI;

namespace AHON_TRACK.Components.ViewModels;

[Page("po-product")]
public class PoProductViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;
    
    public PoProductViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public PoProductViewModel()
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

public partial class PurchaseOrderEquipmentItem : ObservableValidator
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
    private decimal? _suppliersPrice;
    
    [ObservableProperty] 
    private decimal? _markupPrice;
    
    [ObservableProperty] 
    private decimal? _sellingPrice;
    
    [ObservableProperty] 
    private int? _quantity;
    
    [ObservableProperty] 
    private int? _quantityReceived;
}