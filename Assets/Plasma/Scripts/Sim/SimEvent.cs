namespace Plasma.Sim
{
    public enum SimEventType
    {
        Volley, EnemyKilled, EnemyHit, EnemyReachedSquad,
        GateHit, GateBroken, GateSpawned, ConveyorUpgraded, TileCaught, TileMissed,
        SoldiersGained, SoldiersLost,
        BossRevealed, BossHit, BossKilled, BossBite, WaveStarted, Won, Lost
    }

    /// <summary>Something the view/audio layer may want to react to. Cleared by the caller each frame.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public float X, Z;
        public int Value;
        public int Index;
        public SimEvent(SimEventType t, float x, float z, int value = 0, int index = -1) { Type = t; X = x; Z = z; Value = value; Index = index; }
    }
}
