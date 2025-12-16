namespace FaeMaze.Visitors
{
    /// <summary>
    /// Interface for visitors that have an archetype configuration.
    /// Used by hazards and powers to query visitor behavioral parameters.
    /// </summary>
    public interface IArchetypedVisitor
    {
        /// <summary>Gets the visitor's archetype.</summary>
        VisitorArchetype Archetype { get; }

        /// <summary>Gets the visitor's full archetype configuration.</summary>
        VisitorArchetypeConfig ArchetypeConfig { get; }
    }

    /// <summary>
    /// Extension methods for easily accessing archetype configs from components.
    /// </summary>
    public static class VisitorArchetypeExtensions
    {
        /// <summary>
        /// Attempts to get the archetype config from a component.
        /// Returns null if the component doesn't implement IArchetypedVisitor.
        /// </summary>
        public static VisitorArchetypeConfig GetArchetypeConfig(this UnityEngine.Component component)
        {
            return component.GetComponent<IArchetypedVisitor>()?.ArchetypeConfig;
        }

        /// <summary>
        /// Attempts to get the archetype from a component.
        /// Returns null if the component doesn't implement IArchetypedVisitor.
        /// </summary>
        public static VisitorArchetype? GetArchetype(this UnityEngine.Component component)
        {
            var archetyped = component.GetComponent<IArchetypedVisitor>();
            return archetyped != null ? archetyped.Archetype : (VisitorArchetype?)null;
        }
    }
}
