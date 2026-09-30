#if MOBILE
namespace AgendaBuddy.MobileApp.Controls;

/// <summary>How much of a showcase is filled in: a sentence naming the count, over a bar showing it.</summary>
public class CompletenessMeter : ContentView
{
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(CompletenessMeter), 0d,
            propertyChanged: (b, _, n) => ((CompletenessMeter)b)._bar.Progress = Math.Clamp((double)n, 0, 1));

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(CompletenessMeter), string.Empty,
            propertyChanged: (b, _, n) =>
            {
                var meter = (CompletenessMeter)b;
                meter._label.Text = (string)n;
                SemanticProperties.SetDescription(meter, (string)n);
            });

    private readonly Label _label = new() { FontSize = 14, FontAttributes = FontAttributes.Bold };
    private readonly ProgressBar _bar = new() { HeightRequest = 8 };

    public CompletenessMeter()
    {
        if (Application.Current?.Resources.TryGetValue("Primary", out var primary) == true && primary is Color color)
            _bar.ProgressColor = color;
        Content = new VerticalStackLayout { Spacing = 6, Children = { _label, _bar } };
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}
#endif
