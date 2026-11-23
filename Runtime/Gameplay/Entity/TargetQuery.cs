using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // Finds hittable targets in range. Kind (mob / object) is split by layer, friend or foe by Team.
    // Targets with several colliders are added once; dead targets are excluded
    public static class TargetQuery
    {
        static readonly Collider2D[] buffer = new Collider2D[64];
        static readonly HashSet<IHitAble> seen = new();
        static readonly List<IHitAble> scratch = new();

        public static LayerMask Hittable => Layers.Mob | Layers.Object;

        public static void Enemies(Vector2 center, float radius, Team team, List<IHitAble> results) =>
            Enemies(center, radius, team, Hittable, results);

        public static void Enemies(Vector2 center, float radius, Team team, LayerMask kinds, List<IHitAble> results) =>
            Collect(Physics2D.OverlapCircle(center, radius, Filter(kinds), buffer), team, results);

        // A band of the given width from `from` to `to` (a thrust or swept path)
        public static void Line(Vector2 from, Vector2 to, float width, Team team, List<IHitAble> results) =>
            Line(from, to, width, team, Hittable, results);

        public static void Line(Vector2 from, Vector2 to, float width, Team team, LayerMask kinds, List<IHitAble> results)
        {
            Vector2 line = to - from;
            float angle = Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg;
            int n = Physics2D.OverlapBox((from + to) * 0.5f, new Vector2(line.magnitude, width), angle, Filter(kinds), buffer);
            Collect(n, team, results);
        }

        // The nearest target. accept can filter further
        public static IHitAble Nearest(Vector2 center, float radius, Team team, LayerMask kinds, Predicate<IHitAble> accept = null)
        {
            Enemies(center, radius, team, kinds, scratch);
            IHitAble best = null;
            float bestSqr = float.MaxValue;
            foreach (IHitAble h in scratch)
            {
                if (accept != null && !accept(h)) continue;
                float sqr = (Position(h) - center).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = h; }
            }
            return best;
        }


        public static Vector2 Position(IHitAble h) => ((Component)h).transform.position;

        static ContactFilter2D Filter(LayerMask kinds)
        {
            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            filter.SetLayerMask(kinds);
            return filter;
        }

        static void Collect(int n, Team team, List<IHitAble> results)
        {
            results.Clear();
            seen.Clear();
            for (int i = 0; i < n; i++)
            {
                IHitAble h = buffer[i].GetComponentInParent<IHitAble>();
                if (h == null || h.Team == team || h.Body.IsDead || !seen.Add(h)) continue;
                results.Add(h);
            }
        }
    }
}
