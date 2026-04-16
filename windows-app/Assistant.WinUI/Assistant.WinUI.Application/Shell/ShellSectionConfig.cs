using System.Collections.Generic;

namespace Assistant.WinUI.Application.Shell;

public sealed class ShellSectionConfig
{
    public required string Eyebrow { get; init; }

    public required string Badge { get; init; }

    public required string Note { get; init; }

    public required string DefaultSubsection { get; init; }

    public required IReadOnlyList<ShellNavItem> Subsections { get; init; }
}
