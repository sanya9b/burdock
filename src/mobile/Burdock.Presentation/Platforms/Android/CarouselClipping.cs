using Android.Views;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Controls.Handlers.Items;

namespace Burdock.Presentation.Platforms;

/// <summary>
/// Lets a card's glow spill past its page in a <see cref="CarouselView"/> (the hero pager of S-10): the platform
/// RecyclerView and its item wrappers clip children by default, which cut the <c>ShadowHeroGlow</c> at the item bounds.
/// </summary>
internal static class CarouselClipping
{
    public static void Register()
    {
        CarouselViewHandler.Mapper.AppendToMapping("BurdockUnclippedGlow", static (handler, _) =>
        {
            if (handler.PlatformView is not RecyclerView recycler)
            {
                return;
            }

            recycler.SetClipChildren(false);
            recycler.SetClipToPadding(false);
            recycler.AddOnChildAttachStateChangeListener(new UnclipItems());
        });
    }

    private sealed class UnclipItems : Java.Lang.Object, RecyclerView.IOnChildAttachStateChangeListener
    {
        public void OnChildViewAttachedToWindow(Android.Views.View view)
        {
            if (view is ViewGroup item)
            {
                item.SetClipChildren(false);
                item.SetClipToPadding(false);
            }
        }

        public void OnChildViewDetachedFromWindow(Android.Views.View view)
        {
        }
    }
}
