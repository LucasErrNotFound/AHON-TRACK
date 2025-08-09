using HotAvalonia;
using ShadUI;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AHON_TRACK.ViewModels;

[Page("item-purchase")]
public sealed partial class ItemPurchaseViewModel : ViewModelBase, INavigable, INotifyPropertyChanged
{
    private readonly DialogManager _dialogManager;
    private readonly ToastManager _toastManager;
    private readonly PageManager _pageManager;

    // Backing fields for toggle states
    private bool _isAllItemsChecked;
    private bool _isDrinksChecked;
    private bool _isSupplementsChecked;
    private bool _isApparelChecked;
    private bool _isProductsChecked;

    public ItemPurchaseViewModel(DialogManager dialogManager, ToastManager toastManager, PageManager pageManager)
    {
        _dialogManager = dialogManager;
        _toastManager = toastManager;
        _pageManager = pageManager;
    }

    public ItemPurchaseViewModel()
    {
        _dialogManager = new DialogManager();
        _toastManager = new ToastManager();
        _pageManager = new PageManager(new ServiceProvider());
    }

    [AvaloniaHotReload]
    public void Initialize()
    {
    }
    
    public bool IsAllItemsChecked 
    {
        get => _isAllItemsChecked;
        set
        {
            if (!SetField(ref _isAllItemsChecked, value)) return;
            if (!value) return;
            IsDrinksChecked = false;
            IsSupplementsChecked = false;
            IsApparelChecked = false;
            IsProductsChecked = false;
        }
    }

    public bool IsDrinksChecked 
    {
        get => _isDrinksChecked;
        set
        {
            if (!SetField(ref _isDrinksChecked, value)) return;
            if (!value) return;
            IsAllItemsChecked = false;
            IsSupplementsChecked = false;
            IsApparelChecked = false;
            IsProductsChecked = false;
        }
    }
    
    public bool IsSupplementsChecked 
    {
        get => _isSupplementsChecked;
        set
        {
            if (!SetField(ref _isSupplementsChecked, value)) return;
            if (!value) return;
            IsAllItemsChecked = false;
            IsDrinksChecked = false;
            IsApparelChecked = false;
            IsProductsChecked = false;
        }
    }

    public bool IsApparelChecked 
    {
        get => _isApparelChecked;
        set
        {
            if (!SetField(ref _isApparelChecked, value)) return;
            if (!value) return;
            IsAllItemsChecked = false;
            IsDrinksChecked = false;
            IsSupplementsChecked = false;
            IsProductsChecked = false;
        }
    }
    
    public bool IsProductsChecked 
    {
        get => _isProductsChecked;
        set
        {
            if (!SetField(ref _isProductsChecked, value)) return;
            if (!value) return;
            IsAllItemsChecked = false;
            IsDrinksChecked = false;
            IsSupplementsChecked = false;
            IsApparelChecked = false;
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}