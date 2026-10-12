using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Branchy.Services;

/// <summary>Uses the Windows save picker with the app window as owner.</summary>
public sealed class ArchivePickerService(
    Func<Window?> windowProvider,
    ILocalizationService localizer) : IArchivePickerService
{
    /// <inheritdoc />
    public async Task<string?> PickArchivePathAsync(string suggestedFileName)
    {
        Window? window = windowProvider();
        if (window is null)
        {
            return null;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = suggestedFileName,
        };
        picker.FileTypeChoices.Add(localizer.GetString("ArchiveFileType"), [".zip"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
