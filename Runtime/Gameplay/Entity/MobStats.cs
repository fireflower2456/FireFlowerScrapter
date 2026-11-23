namespace FireFlower
{
    // Stat keys read by the MobBase frame. The game's key list points to these values
    public static class MobStats
    {
        public const string MaxHp = "Mob.MaxHp";
        public const string Defence = "Mob.Defence";
        public const string MoveSpeed = "Mob.MoveSpeed";
        public const string BehaviorSpeed = "Mob.BehaviorSpeed";
        public const string AtkPower = "Mob.AtkPower";
        public const string AtkBehaviorSpeed = "Mob.AtkBehaviorSpeed";

        // Body values (outside the StatSheet). Used when the stat value block picks Hp or shield
        public static class Body
        {
            public const string Hp = "Mob.Hp";
            public const string Shield = "Mob.Shield";
        }

        public static class Flag
        {
            public const string Invincible = "Mob.Invincible";
            public const string IsKnocked = "Mob.IsKnocked";
            public const string Hidden = "Mob.Hidden";           // removed from the field: invisible, physics/collision/UI off
        }
    }
}
