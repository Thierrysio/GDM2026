using GDM2026.ViewModels;
using Microsoft.Maui.Controls;

namespace GDM2026;

public partial class ImageUploadPage : ContentPage
{
    private readonly ImageUploadViewModel _viewModel = new();

    public ImageUploadPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }
}
