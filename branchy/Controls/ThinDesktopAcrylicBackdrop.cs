using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Branchy.Controls;

/// <summary>Uses the native Thin Acrylic recipe for transient surfaces.</summary>
public sealed class ThinDesktopAcrylicBackdrop : SystemBackdrop
{
    private readonly Dictionary<ICompositionSupportsSystemBackdrop, DesktopAcrylicController> _controllers = new();

    /// <inheritdoc />
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);
        if (!DesktopAcrylicController.IsSupported() || _controllers.ContainsKey(connectedTarget))
        {
            return;
        }

        DesktopAcrylicController? controller = null;
        try
        {
            controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
            // Keep native theme, accessibility and activation policy instead of fixing tint colors.
            controller.SetSystemBackdropConfiguration(GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot));
            controller.AddSystemBackdropTarget(connectedTarget);
            _controllers.Add(connectedTarget, controller);
        }
        catch
        {
            controller?.Dispose();
            OnTargetDisconnected(connectedTarget);
            throw;
        }
    }

    /// <inheritdoc />
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        try
        {
            if (_controllers.Remove(disconnectedTarget, out DesktopAcrylicController? controller))
            {
                try
                {
                    controller.RemoveSystemBackdropTarget(disconnectedTarget);
                }
                finally
                {
                    controller.Dispose();
                }
            }
        }
        finally
        {
            try
            {
                base.OnTargetDisconnected(disconnectedTarget);
            }
            finally
            {
                // Transient backdrop links must close on the UI thread, not in a WinRT finalizer.
                if (disconnectedTarget is IDisposable backdropLink)
                {
                    backdropLink.Dispose();
                }
            }
        }
    }
}
