namespace Burdock.Presentation.Navigation;

/// <summary>A Shell route. Route strings exist only here and in AppShell.xaml (spec 01 section 3.12, spec 03 section 3.4).</summary>
public readonly record struct Route(string Path)
{
    public override string ToString() => Path;
}

/// <summary>All navigable destinations; deep links map onto these (spec 03 deep-link table).</summary>
public static class Routes
{
    public static class Today
    {
        public static readonly Route Root = new("//today");
        public static readonly Route Word = new("//today/word");
    }

    public static class Learn
    {
        public static readonly Route Root = new("//learn");
        public static readonly Route Session = new("//learn/session");
        public static readonly Route Focus = new("//learn/focus");
    }

    public static class Words
    {
        public static readonly Route Root = new("//words");
    }

    public static class Me
    {
        public static readonly Route Root = new("//me");
        public static readonly Route Settings = new("//me/settings");
        public static readonly Route Widgets = new("//me/widgets");
    }

    public static readonly Route Paywall = new("//paywall");
}
