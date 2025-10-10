using AHON_TRACK.Components.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Diagnostics;
using System.IO;

namespace AHON_TRACK.Components.AddEditProduct;

public partial class AddEditProductView : UserControl
{
    public AddEditProductView()
    {
        InitializeComponent();

        // Subscribe to DataContext changes to handle edit mode
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is AddEditProductViewModel vm)
        {
            // Listen for property changes
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(AddEditProductViewModel.ProductImageFilePath))
                {
                    LoadExistingImage(vm.ProductImageFilePath);
                }
            };

            // Load existing image if available
            if (!string.IsNullOrEmpty(vm.ProductImageFilePath))
            {
                LoadExistingImage(vm.ProductImageFilePath);
            }
        }
    }

    private void LoadExistingImage(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return;

        try
        {
            var profileImage = this.FindControl<Image>("ProductImage");
            if (profileImage == null) return;

            // Check if it's a file path
            if (File.Exists(imagePath))
            {
                var bitmap = new Bitmap(imagePath);
                profileImage.Source = bitmap;
                profileImage.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading existing image: {ex.Message}");
        }
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                Title = "Select an image file",
                FileTypeFilter = [FilePickerFileTypes.ImageJpg, FilePickerFileTypes.ImagePng, FilePickerFileTypes.ImageAll]
            });

            if (files.Count <= 0) return;

            var file = files[0];
            await using var stream = await file.OpenReadAsync();

            // Create a Bitmap to preview the image
            var bitmap = new Bitmap(stream);

            // Find the image control
            var profileImage = this.FindControl<Image>("ProductImage");
            if (profileImage == null) return;

            // Display the selected image
            profileImage.Source = bitmap;
            profileImage.IsVisible = true;

            // Save the local file path for database
            if (DataContext is AddEditProductViewModel vm)
            {
                vm.ProductImageFilePath = file.Path.LocalPath;
                Debug.WriteLine($"Image selected: {file.Path.LocalPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error from uploading Picture: {ex.Message}");
        }
    }
}