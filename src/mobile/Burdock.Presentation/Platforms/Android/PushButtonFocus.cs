using Android.Views;

namespace Burdock.Presentation.Controls;

public sealed partial class PushButton
{
    // Keyboard, D-pad and Switch Access reach the button like a native one (spec 02 section 3.11 «focused»):
    // the platform view becomes focusable, focus shows the ring, Enter / Space / DPAD_CENTER activate.
    partial void PlatformConfigure()
    {
        if (Handler?.PlatformView is not Android.Views.View view)
        {
            return;
        }

        view.Focusable = true;
        view.FocusableInTouchMode = false;
        view.FocusChange -= OnPlatformFocusChange;
        view.FocusChange += OnPlatformFocusChange;
        view.KeyPress -= OnPlatformKeyPress;
        view.KeyPress += OnPlatformKeyPress;
    }

    private void OnPlatformFocusChange(object? sender, Android.Views.View.FocusChangeEventArgs e) => SetFocusRing(e.HasFocus);

    private void OnPlatformKeyPress(object? sender, Android.Views.View.KeyEventArgs e)
    {
        var activates = e.KeyCode is Keycode.Enter or Keycode.NumpadEnter or Keycode.Space or Keycode.DpadCenter;
        if (!activates)
        {
            e.Handled = false;
            return;
        }

        // Swallow the press so the key does not scroll; activate on release like a native button.
        e.Handled = true;
        if (e.Event?.Action == KeyEventActions.Up)
        {
            _ = HandleTapAsync();
        }
    }
}
