using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only registry.
/// Maps ActionData subtypes and MechanicTypes to the Unity component types
/// that must exist on the relevant GameObject in the level prefab.
/// </summary>
[InitializeOnLoad]
public static class ComponentRequirementRegistry
{
    // Clear type cache after every script recompile so renamed/moved types resolve correctly
    static ComponentRequirementRegistry() => AssemblyReloadEvents.afterAssemblyReload += ClearCache;
    // ── Data ──────────────────────────────────────────────────────────────────

    public struct ComponentInfo
    {
        /// <summary>Fully-qualified type name. Used to resolve the real Type at edit time.</summary>
        public string TypeName;

        /// <summary>Short name shown in the Level Editor warning panel.</summary>
        public string DisplayName;

        /// <summary>Plain-language explanation for the GD.</summary>
        public string Reason;

        /// <summary>
        /// Optional: if the component lives in a package that may not be installed,
        /// show this message instead of "click Apply".
        /// </summary>
        public string MissingPackageHint;
    }

    public struct RequirementEntry
    {
        /// <summary>Human-readable label shown in the warning list.</summary>
        public string Label;

        /// <summary>Components that must exist on the object with ID = SourceObjectID.</summary>
        public ComponentInfo[] Components;
    }

    // ── Action requirements ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the components that the ACTION's target object must have.
    /// </summary>
    public static IEnumerable<ComponentInfo> GetForAction(ActionData action)
    {
        if (action == null) yield break;

        switch (action)
        {
            case PlayAnimationActionData anim:
                foreach (var c in GetForAnimationBackend(anim.Backend))
                    yield return c;
                break;

            case SetScaleActionData _:
            case SetPositionActionData _:
            case SetActiveActionData _:
                // Transform is always present — no extra component needed
                break;

            default:
                switch (action.Type)
                {
                    case ActionType.PlaySound:
                        yield return new ComponentInfo
                        {
                            TypeName    = "UnityEngine.AudioSource",
                            DisplayName = "AudioSource",
                            Reason      = "PlaySound action requires an AudioSource to play the clip."
                        };
                        break;

                    case ActionType.Wait:
                        // Pure timer — no component on target
                        break;
                }
                break;
        }
    }

    private static IEnumerable<ComponentInfo> GetForAnimationBackend(AnimationBackend backend)
    {
        switch (backend)
        {
            case AnimationBackend.Spine:
                yield return new ComponentInfo
                {
                    TypeName         = "Spine.Unity.SkeletonAnimation",
                    DisplayName      = "SkeletonAnimation",
                    Reason           = "Spine animation requires SkeletonAnimation (or SkeletonMecanim) on the target.",
                    MissingPackageHint = "Spine Unity Runtime not found. Import the Spine package first."
                };
                break;

            case AnimationBackend.Animator:
                yield return new ComponentInfo
                {
                    TypeName    = "UnityEngine.Animator",
                    DisplayName = "Animator",
                    Reason      = "Animator backend requires a Unity Animator component."
                };
                break;

            case AnimationBackend.DOTween:
                // DOTween animates via code — no component required on the target
                break;
        }
    }

    // ── Mechanic requirements ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the components that the MECHANIC's source object must have.
    /// </summary>
    public static IEnumerable<ComponentInfo> GetForMechanic(MechanicType mechanic)
    {
        switch (mechanic)
        {
            case MechanicType.Draggable:
                // BoxCollider2D must come first — Draggable has [RequireComponent(Collider2D)]
                yield return new ComponentInfo
                {
                    TypeName    = "UnityEngine.BoxCollider2D",
                    DisplayName = "BoxCollider2D",
                    Reason      = "Drag detection requires a 2D collider."
                };
                yield return new ComponentInfo
                {
                    TypeName    = "ROOT.Scripts.Draggable",
                    DisplayName = "Draggable",
                    Reason      = "Object must be draggable by the player."
                };
                break;

            case MechanicType.DropTarget:
                // BoxCollider2D must come first — DropZone has [RequireComponent(Collider2D)]
                yield return new ComponentInfo
                {
                    TypeName    = "UnityEngine.BoxCollider2D",
                    DisplayName = "BoxCollider2D",
                    Reason      = "Drop zone detection requires a trigger BoxCollider2D."
                };
                yield return new ComponentInfo
                {
                    TypeName    = "ROOT.Scripts.DropZone",
                    DisplayName = "DropZone",
                    Reason      = "Object acts as a drop zone that validates incoming draggables."
                };
                break;

            case MechanicType.Click:
                // BoxCollider2D must come first — Clickable has [RequireComponent(Collider2D)]
                yield return new ComponentInfo
                {
                    TypeName    = "UnityEngine.BoxCollider2D",
                    DisplayName = "BoxCollider2D",
                    Reason      = "Click detection requires a 2D collider for raycasting."
                };
                yield return new ComponentInfo
                {
                    TypeName    = "ROOT.Scripts.Clickable",
                    DisplayName = "Clickable",
                    Reason      = "Object must respond to player tap / click."
                };
                break;

            case MechanicType.Timer:
                yield return new ComponentInfo
                {
                    TypeName    = "TimerTrigger",
                    DisplayName = "TimerTrigger",
                    Reason      = "A countdown timer that fires the requirement when it reaches zero."
                };
                break;

            case MechanicType.None:
            default:
                break;
        }
    }

    // ── Type resolution helpers ───────────────────────────────────────────────

    // Cache resolved types to avoid iterating assemblies every frame
    private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();

    /// <summary>
    /// Resolves System.Type from TypeName.
    /// Tries full name first, then short name fallback across all assemblies.
    /// </summary>
    public static Type ResolveType(ComponentInfo info)
    {
        if (_typeCache.TryGetValue(info.TypeName, out var cached))
            return cached;

        Type found = null;

        // Pass 1: full qualified name per assembly
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            found = asm.GetType(info.TypeName, throwOnError: false);
            if (found != null) break;
        }

        // Pass 2: short name fallback (last segment after '.')
        if (found == null)
        {
            var shortName = info.TypeName.Contains('.')
                ? info.TypeName.Substring(info.TypeName.LastIndexOf('.') + 1)
                : info.TypeName;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Skip system / Unity editor assemblies for speed
                var asmName = asm.GetName().Name;
                if (asmName.StartsWith("System") || asmName.StartsWith("mscorlib") ||
                    asmName.StartsWith("Microsoft") || asmName.StartsWith("Mono")) continue;

                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == shortName && typeof(Component).IsAssignableFrom(t))
                        { found = t; break; }
                    }
                }
                catch { /* skip assemblies that throw on GetTypes */ }

                if (found != null) break;
            }
        }

        if (found != null)
            _typeCache[info.TypeName] = found; // only cache successful resolves — null means retry next time

        return found;
    }

    /// <summary>Clears the type cache — call after a script recompile.</summary>
    public static void ClearCache() => _typeCache.Clear();

    /// <summary>
    /// Returns true if the component is already present on the GameObject.
    /// </summary>
    public static bool HasComponent(GameObject go, ComponentInfo info)
    {
        var type = ResolveType(info);
        if (type == null) return false;
        return go.GetComponent(type) != null;
    }

    /// <summary>
    /// Adds the component to the GameObject if its type can be resolved and it's missing.
    /// Returns true when a component was actually added.
    /// </summary>
    public static bool EnsureComponent(GameObject go, ComponentInfo info)
    {
        var type = ResolveType(info);
        if (type == null) return false;
        if (go.GetComponent(type) != null) return false;

        Undo.AddComponent(go, type);
        Debug.Log($"[LevelEditor] Added <b>{info.DisplayName}</b> to '{go.name}'.");
        return true;
    }
}
