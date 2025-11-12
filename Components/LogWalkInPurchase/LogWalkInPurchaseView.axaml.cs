using AHON_TRACK.Components.ViewModels;
using Avalonia.Controls;

namespace AHON_TRACK.Components.LogWalkInPurchase;

public partial class LogWalkInPurchaseView : UserControl
{
    public LogWalkInPurchaseView()
    {
        InitializeComponent();
        
        Loaded += (s, e) =>
        {
            if (DataContext is LogWalkInPurchaseViewModel vm)
            {
                vm.LetterConsentTextBoxControl1 = this.FindControl<TextBox>("LetterConsentTextBoxControl1");
                vm.LetterConsentTextBoxControl2 = this.FindControl<TextBox>("LetterConsentTextBoxControl2");
            }
        };
    }
}