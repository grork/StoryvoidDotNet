using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml.Navigation;
using System.Runtime.InteropServices;
using WinRT;

namespace Codevoid.Storyvoid.Utilities;

/// <summary>
/// Apply to a <see cref="Microsoft.UI.Xaml.Controls.Page"/>, so that when the
/// window has a <see cref="SystemBackdropHelper"/> attached, along with the
/// <see cref="Microsoft.UI.Xaml.Controls.Frame" /> the page is part of, the
/// app background will reflect the system backdrop if the attribute is present.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal class UseSystemBackdropAttribute : Attribute
{ }

/// <summary>
/// Enables a system backdrop on a window. It handles clean up when the window
/// closes, and changing the material when the windows active state changes.
/// </summary>
internal class SystemBackdropHelper
{
    /// <summary>
    /// Lifted from the documentation on supporting System Backdrops. See
    /// https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/system-backdrop-controller
    /// </summary>
    private class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static unsafe extern int CreateDispatcherQueueController(DispatcherQueueOptions options, IntPtr* instance);

        IntPtr m_dispatcherQueueController = IntPtr.Zero;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == IntPtr.Zero)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                unsafe
                {
                    IntPtr dispatcherQueueController;
                    CreateDispatcherQueueController(options, &dispatcherQueueController);
                    m_dispatcherQueueController = dispatcherQueueController;
                }
            }
        }
    }

    private Window controlledWindow;
    private Frame navigationSource;
    private WindowsSystemDispatcherQueueHelper queueHelper;
    private MicaController? backdropController;
    private SystemBackdropConfiguration backdropConfiguration;
    private bool IsCurrentlyApplyingBackdrop = false;

    /// <summary>
    /// Create a backdrop helper that will listen to frame navigations, and
    /// based on the presence of <see cref="UseSystemBackdropAttribute" /> apply
    /// the system backdrop to the window with an animation.
    /// </summary>
    /// <param name="controlledWindow">Window to apply the backdrop to</param>
    /// <param name="navigationSource">Frame that will raised Navigated events</param>
    public SystemBackdropHelper(Window controlledWindow, Frame frameSource)
    {
        this.controlledWindow = controlledWindow;
        this.navigationSource = frameSource;

        this.queueHelper = new WindowsSystemDispatcherQueueHelper();
        this.queueHelper.EnsureWindowsSystemDispatcherQueueController();

        this.backdropConfiguration = new SystemBackdropConfiguration()
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default
        };

        this.navigationSource.Navigated += FrameSource_Navigated;
        this.controlledWindow.Activated += ControlledWindow_Activated;
        this.controlledWindow.Closed += ControlledWindow_Closed;
    }

    /// <summary>
    /// Set up -- but do not apply -- the backdrop to the window. Once a
    /// navigation occurs, the backdrop will reflect the page.
    /// 
    /// If you wish to force the backdrop on or off see
    /// <see cref="ShowBackdrop(bool)"/> or <see cref="HideBackdrop"/>
    /// </summary>
    private void SetupBackdrop()
    {
        if (this.backdropController != null)
        {
            // Already created, don't need to do it again
            return;
        }

        this.backdropController = new MicaController();
        this.backdropController.SetSystemBackdropConfiguration(backdropConfiguration);
    }

    /// <summary>
    /// Cleans up the backdrop and frees the system resources
    /// </summary>
    private void CleanupBackdrop()
    {
        this.backdropController?.Dispose();
        this.backdropController = null;
    }

    /// <summary>
    /// Explicitly hides the backdrop with an animation. When the next
    /// navigation occurs, the backdrop will reflect the page choice.
    /// </summary>
    internal async void HideBackdrop()
    {
        // if we are not applying it, theres no need to hide it.
        if (!this.IsCurrentlyApplyingBackdrop)
        {
            return;
        }

        this.IsCurrentlyApplyingBackdrop = false;

        // Rather than have the effect transition instantly to the no-backdrop
        // visual, if we apply some settings the effect will be animated. Note
        // that this works peachy with Mica, but with Acrylic, it doesn't go
        // opaque, and will need updating in that case.
        this.ApplyAppBackgroundColorToBackdrop();

        // We'd like the animation to play before we destroy the effect.
        await Task.Delay(500);
        this.CleanupBackdrop();
    }

    /// <summary>
    /// Explicitly shows the backdrop on the window
    /// </summary>
    /// <param name="withTransition">If false, no animation is applied</param>
    internal async void ShowBackdrop(bool withTransition = false)
    {
        // If we're already showing it, we don't need to show it again (e.g.
        // avoid redundant animations
        if (this.IsCurrentlyApplyingBackdrop)
        {
            return;
        }

        this.IsCurrentlyApplyingBackdrop = true;
        if (!withTransition)
        {
            // Apply an animation from the empty state so it looks nicer when
            // we transition _from_ no-backdrop
            this.ApplyAppBackgroundColorToBackdrop();
        }

        // This is what makes the system actually apply the backdrop
        this.backdropController!.AddSystemBackdropTarget(this.controlledWindow.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());

        if (!withTransition)
        {
            await Task.Delay(1); // Make sure it renders before we transition
        }

        this.backdropController!.ResetProperties();
    }

    private void ApplyAppBackgroundColorToBackdrop()
    {
        var uiSettings = new Windows.UI.ViewManagement.UISettings();
        var backgroundColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
        this.backdropController!.TintColor = backgroundColor;
        this.backdropController!.TintOpacity = 1.0F;
    }

    private void ControlledWindow_Closed(object sender, WindowEventArgs args)
    {
        this.CleanupBackdrop();
        this.controlledWindow.Closed -= this.ControlledWindow_Closed;
        this.controlledWindow.Activated -= this.ControlledWindow_Activated;
    }

    /// <summary>
    /// Listens for window activation so that the backdrop reflects the actual
    /// window active state
    /// </summary>
    private void ControlledWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        this.backdropConfiguration.IsInputActive = (args.WindowActivationState != WindowActivationState.Deactivated);
    }

    private void FrameSource_Navigated(object sender, NavigationEventArgs e)
    {
        var pageAttributes = e.SourcePageType.GetCustomAttributes(typeof(UseSystemBackdropAttribute), false);

        this.SetupBackdrop();
        if (pageAttributes.Length == 0)
        {
            this.HideBackdrop();
        }
        else
        {
            // We only want to apply the backdrop transition if we have other
            // items in the backstack
            var withTransition = (this.navigationSource.BackStack.Count == 0 && this.navigationSource.ForwardStack.Count == 0);
            this.ShowBackdrop(withTransition);
        }
    }
}
