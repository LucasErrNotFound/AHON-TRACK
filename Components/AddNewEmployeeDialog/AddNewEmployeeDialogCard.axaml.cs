using AHON_TRACK.Components.ViewModels;
using Avalonia.Controls;

namespace AHON_TRACK.Components.AddNewEmployeeDialog;

public partial class AddNewEmployeeDialogCard : UserControl
{
    public AddNewEmployeeDialogCard()
    {
        InitializeComponent();
        
        Loaded += (s, e) =>
        {
            if (DataContext is AddNewEmployeeDialogCardViewModel vm)
            {
                vm.EmployeeProfileImageControl = this.FindControl<Image>("EmployeeProfileImage");
            }
        };
    }
}