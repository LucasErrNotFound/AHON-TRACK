using System;
using System.Diagnostics;
using AHON_TRACK.Components.ViewModels;
using AHON_TRACK.Converters;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace AHON_TRACK.Components.AddNewEmployeeDialog;

public partial class AddNewEmployeeDialogCard : UserControl
{
    public AddNewEmployeeDialogCard()
    {
        InitializeComponent();
    }

    private async void Button_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel is null)
            {
                return;
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Select an image file",
                FileTypeFilter = [FilePickerFileTypes.ImageJpg, FilePickerFileTypes.ImagePng, FilePickerFileTypes.ImageAll]
            });

            if (files.Count <= 0) return;
            var file = files[0];
            await using var stream = await file.OpenReadAsync();

            var bitmap = new Bitmap(stream);

            if (DataContext is AddNewEmployeeDialogCardViewModel vm)
            {
                vm.ProfileImageSource = bitmap;
                vm.ProfileImage = ImageHelper.BitmapToBytes(bitmap);
            }

            var profileImage = this.FindControl<Image>("EmployeeProfileImage");
            if (profileImage == null) return;
            profileImage.Source = bitmap;
            profileImage.IsVisible = true; // Ensure the image is visible after loading
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error from uploading Picture: {ex.Message}");
        }
    }
}