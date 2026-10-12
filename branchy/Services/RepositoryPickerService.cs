using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Branchy.Services;

/// <summary>Uses the Windows folder picker with the app window as owner.</summary>
public sealed class RepositoryPickerService(Func<Window?> windowProvider) : IRepositoryPickerService
{
    /// <inheritdoc />
    public async Task<string?> PickRepositoryAsync()
    {
        Window? window = windowProvider();
        if (window is null)
        {
            return null;
        }

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };
        picker.FileTypeFilter.Add("*");

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
