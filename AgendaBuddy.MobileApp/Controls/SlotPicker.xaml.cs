#if MOBILE
using AgendaBuddy.MobileApp.ViewModels;

namespace AgendaBuddy.MobileApp.Controls;

/// <summary>
/// The shared free-slot picker. Bind its <c>BindingContext</c> to a <see cref="SlotPickerViewModel"/>.
/// </summary>
/// <remarks>
/// One control for booking, provider reschedule and customer reschedule request. Three copies of a
/// timezone-sensitive picker is three places for the same off-by-one-day bug, and the rules that matter here —
/// grouping by local date while holding UTC instants, landing on the soonest date with room, dropping a slot
/// when the date changes — live in the view model, which is covered on the <c>net10.0</c> test slice.
/// </remarks>
public partial class SlotPicker : ContentView
{
    public SlotPicker() => InitializeComponent();

    /// <summary>
    /// Surfaced as a property so the "nothing free" copy is not a second string in XAML that can drift from the
    /// view model's own.
    /// </summary>
    public static string FullyBookedMessage => SlotPickerViewModel.FullyBookedMessage;
}
#endif
