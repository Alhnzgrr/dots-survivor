using System;
using UnityEngine;

namespace ECSSurvivor.Enemy
{
    [CreateAssetMenu(fileName = "EnemySpawnData", menuName = "ECS-Survivor/Enemy Spawn Data")]
    public class EnemySpawnData : ScriptableObject
    {
        [SerializeField] private WaveConfig[] waves = Array.Empty<WaveConfig>();

        public WaveConfig[] Waves => waves;
    }

    [Serializable]
    public struct WaveConfig
    {
        [Min(1)] public int Count;
        [Min(0f)] public float Interval;
    }
}
