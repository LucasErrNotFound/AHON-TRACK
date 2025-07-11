using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace AHON_TRACK.Components.EmployeeDetails;

public partial class EmployeeDetailsDialogCard : UserControl
{
    public EmployeeDetailsDialogCard()
    {
        InitializeComponent();
    }

    private async void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select an image file",
            FileTypeFilter = [ FilePickerFileTypes.ImageJpg, FilePickerFileTypes.ImagePng, FilePickerFileTypes.ImageAll ]
        });

        if (files.Count > 0)
        {
            var file = files[0];
            using var stream = await file.OpenReadAsync();
            var bitmap = new Bitmap(stream);

            var profileImage = this.FindControl<Image>("EmployeeProfileImage");
            if (profileImage != null)
            {
                profileImage.Source = bitmap;
                profileImage.IsVisible = true; // Ensure the image is visible after loading
            }
        }
    }
}