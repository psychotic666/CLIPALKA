namespace Clipalka.App.Models;

public sealed record DeviceOption(string Id, string Name, bool IsDefault = false)
{
    public override string ToString() => IsDefault ? $"{Name} (по умолчанию)" : Name;
}

