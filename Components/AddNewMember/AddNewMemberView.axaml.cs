using AHON_TRACK.Components.ViewModels;
using Avalonia.Controls;

namespace AHON_TRACK.Components.AddNewMember;

public partial class AddNewMemberView : UserControl
{
    public AddNewMemberView()
    {
        InitializeComponent();

        Loaded += (s, e) =>
        {
            if (DataContext is AddNewMemberViewModel vm)
            {
                vm.MemberProfileImageControl = this.FindControl<Image>("MemberProfileImage");
                vm.MemberProfileImageControl2 = this.FindControl<Image>("MemberProfileImage2");
            }
        };
    }
}