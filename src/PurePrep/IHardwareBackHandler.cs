namespace PurePrep;

/// <summary>
/// Implemented by pages that show an in-page overlay (a sheet or prompt that is not a separate
/// navigation page) which the hardware back button should dismiss first. The Android back policy
/// lives in <c>MainActivity</c> — because at targetSdk 35+ Android's predictive-back callback
/// bypasses MAUI's per-page <c>OnBackButtonPressed</c> — and it asks the current page through this
/// hook before popping or exiting.
/// </summary>
public interface IHardwareBackHandler
{
    /// <summary>Returns <c>true</c> if the page consumed the back press (e.g. closed an overlay).</summary>
    bool OnHardwareBack();
}
