namespace Clipalka.App.Models;

public sealed record DeviceOption(string Id, string Name, bool IsDefault = false)
{
    public string DisplayName
    {
        get
        {
            const string prefix = "SteelSeries Sonar";
            if (!Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return Name;
            var channel = Name[prefix.Length..].TrimStart(' ', '-', '—', '–');
            var details = channel.IndexOf(" (", StringComparison.Ordinal);
            return "Sonar " + (details >= 0 ? channel[..details] : channel);
        }
    }

    public override string ToString() => DisplayName;
}

