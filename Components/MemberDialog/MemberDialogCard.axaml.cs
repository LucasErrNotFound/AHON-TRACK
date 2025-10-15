using AHON_TRACK.Components.ViewModels;
using Avalonia.Controls;

namespace AHON_TRACK.Components.MemberDialog;

public partial class MemberDialogCard : UserControl
{
    public MemberDialogCard()
    {
        InitializeComponent();
        
        Loaded += (s, e) =>
        {
            if (DataContext is MemberDialogCardViewModel vm)
            {
                vm.MemberProfileImageControl = this.FindControl<Image>("MemberProfileImage");
            }
        };
    }
}