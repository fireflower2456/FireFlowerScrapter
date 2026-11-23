using System;

namespace FireFlower
{
    // The SO only provides defaults. Each call creates an independent runtime sheet.
    public interface IStatSource
    {
        void WriteStats(StatSheet sheet);
    }

    public static class StatSourceExtensions
    {
        public static StatSheet CreateSheet(this IStatSource source)
        {
            if (source == null || (source is UnityEngine.Object asset && asset == null))
                throw new ArgumentNullException(nameof(source));
            var sheet = new StatSheet();
            source.WriteStats(sheet);
            return sheet;
        }
    }
}
