using Unity.Entities;

namespace ECSSurvivor.Core
{
    /// The order below is the whole frame. Every gameplay system declares itself
    /// into one of these groups instead of pairing off with UpdateBefore/UpdateAfter,
    /// so adding a system cannot silently reorder an unrelated pair.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystemGroup))]
    public partial class InputSystemGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SpatialSystemGroup))]
    public partial class MovementSystemGroup : ComponentSystemGroup { }

    /// The acceleration structure is rebuilt after everything has moved and
    /// before anything queries it, so no system ever reads a stale grid.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(CombatSystemGroup))]
    public partial class SpatialSystemGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ResolutionSystemGroup))]
    public partial class CombatSystemGroup : ComponentSystemGroup { }

    /// Damage is applied and deaths are resolved here, on one thread, after every
    /// parallel producer has finished writing events.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class ResolutionSystemGroup : ComponentSystemGroup { }
}
