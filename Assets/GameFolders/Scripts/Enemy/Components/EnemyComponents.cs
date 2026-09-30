using Unity.Entities;

namespace ECSSurvivor.Enemy
{
    public struct EnemyTag : IComponentData { }

    public struct EnemyMoveData : IComponentData
    {
        public float FollowStopDistance;
        public float FollowStartDistance;
        public float MoveSpeed;
    }

    public struct EnemyContactDamage : IComponentData
    {
        public float Amount;
        public float Radius;
    }

    public struct EnemySpawner : IComponentData
    {
        public Entity EnemyPrefab;
    }

    public struct EnemySpawnSettings : IComponentData
    {
        public float SpawnDistanceMin;
        public float SpawnDistanceMax;
        public float SpawnMinSeparation;
    }

    public struct EnemySpawnState : IComponentData
    {
        public int WaveIndex;
        public int SpawnedInWave;
        public int AliveInWave;
        public float Cooldown;
        public Unity.Mathematics.Random Random;
    }

    /// Which spawner a live enemy belongs to, so a wave can wait for its own
    /// children to die without counting enemies from another spawner.
    public struct WaveOwner : IComponentData
    {
        public Entity Spawner;
    }

    public struct WaveElement : IBufferElementData
    {
        public int Count;
        public float Interval;
    }
}
