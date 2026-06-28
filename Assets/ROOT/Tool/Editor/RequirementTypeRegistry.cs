using System.Collections.Generic;

/// <summary>
/// Maps RequirementType → IRequirementDescriptor.
/// Descriptors self-register via [InitializeOnLoad] static constructors — nothing here needs editing.
/// </summary>
public static class RequirementTypeRegistry
{
    private static readonly Dictionary<RequirementType, IRequirementDescriptor> _map =
        new Dictionary<RequirementType, IRequirementDescriptor>();

    /// <summary>Called by each descriptor's [InitializeOnLoad] static ctor.</summary>
    public static void Register(RequirementType type, IRequirementDescriptor descriptor)
        => _map[type] = descriptor;

    /// <summary>Returns the descriptor for <paramref name="type"/>, or null if not registered.</summary>
    public static IRequirementDescriptor Get(RequirementType type)
        => _map.TryGetValue(type, out var d) ? d : null;
}
