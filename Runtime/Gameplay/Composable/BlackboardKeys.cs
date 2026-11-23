
namespace FireFlower
{
    // String constants like StatKeys. Each key's comment gives its stored type.
    public static class BlackboardKeys
    {
        public static class Monster
        {
            public const string Target = "Monster.Target";                       // Transform
            public const string Ally = "Monster.Ally";                           // Transform, nearest mob on the same team
            public const string LastKnownPosition = "Monster.LastKnownPosition"; // Vector3
            public const string SpawnPosition = "Monster.SpawnPosition";         // Vector3
            public const string HasLineOfSight = "Monster.HasLineOfSight";        // bool
            public const string LastAttackTime = "Monster.LastAttackTime";       // float
        }

        public static class FieldState
        {
            public const string SkillCooldown = "FieldState.SkillCooldown";      // float, remaining time
        }
    }
}
