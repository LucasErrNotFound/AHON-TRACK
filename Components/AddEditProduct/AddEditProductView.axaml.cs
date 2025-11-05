using AHON_TRACK.Components.ViewModels;
using Avalonia.Controls;

namespace AHON_TRACK.Components.AddEditProduct;

public partial class AddEditProductView : UserControl
{
    public AddEditProductView()
    {
        InitializeComponent();

        Loaded += (s, e) =>
        {
            if (DataContext is AddEditProductViewModel vm)
            {
                vm.ProductImageControl = this.FindControl<Image>("ProductImage");
            }
        };
    }
}