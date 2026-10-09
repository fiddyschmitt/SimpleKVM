using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleKVM.Displays.linux
{
    /// <summary>
    /// Converts a compositor's logical (scaled) layout into physical pixels, so monitor ids don't
    /// change with the desktop scale and match the ids Windows produces for the same geometry.
    /// Sizes are exact (the mode in pixels); positions are rebuilt from edge adjacency in the
    /// logical layout, since scaling a logical offset is wrong once monitors have mixed scales.
    /// </summary>
    public static class PhysicalLayout
    {
        /// <summary>
        /// How far apart two edges may be and still count as touching. At a scale like 175% a
        /// monitor's logical size isn't whole, and the compositor may place its neighbour a
        /// pixel either side of where the rounded size ends.
        /// </summary>
        const int EdgeSlack = 1;

        /// <param name="X">Logical position</param>
        /// <param name="LogicalWidth">Logical size (mode size divided by scale)</param>
        /// <param name="Width">Physical size in pixels, rotation already applied</param>
        public record Output(int X, int Y, int LogicalWidth, int LogicalHeight, int Width, int Height);

        public record Rect(int X, int Y, int Width, int Height);

        public static Dictionary<string, Rect> ToPhysical(IReadOnlyDictionary<string, Output> outputs)
        {
            var xs = PlaceAxis(outputs.ToDictionary(o => o.Key, o => (o.Value.X, o.Value.LogicalWidth, o.Value.Width)));
            var ys = PlaceAxis(outputs.ToDictionary(o => o.Key, o => (o.Value.Y, o.Value.LogicalHeight, o.Value.Height)));

            return outputs.ToDictionary(o => o.Key, o => new Rect(xs[o.Key], ys[o.Key], o.Value.Width, o.Value.Height));
        }

        /// <summary>
        /// Physical start of each output along one axis. Outputs at or after the origin are placed
        /// ascending and butt against a placed output whose logical end meets their logical start
        /// (give or take <see cref="EdgeSlack"/>);
        /// outputs before the origin are placed descending against the one they end at. An output
        /// touching nothing falls back to its logical offset times its own scale.
        /// </summary>
        static Dictionary<string, int> PlaceAxis(Dictionary<string, (int Start, int LogicalSize, int Size)> axis)
        {
            var placed = new Dictionary<string, int>();

            foreach (var (name, o) in axis.Where(a => a.Value.Start >= 0).OrderBy(a => a.Value.Start))
            {
                var neighbour = placed.Keys.FirstOrDefault(p => Math.Abs(axis[p].Start + axis[p].LogicalSize - o.Start) <= EdgeSlack);
                placed[name] = o.Start == 0 ? 0
                             : neighbour != null ? placed[neighbour] + axis[neighbour].Size
                             : Scale(o.Start, o.LogicalSize, o.Size);
            }

            foreach (var (name, o) in axis.Where(a => a.Value.Start < 0).OrderByDescending(a => a.Value.Start))
            {
                var neighbour = placed.Keys.FirstOrDefault(p => Math.Abs(o.Start + o.LogicalSize - axis[p].Start) <= EdgeSlack);
                placed[name] = neighbour != null ? placed[neighbour] - o.Size
                             : Scale(o.Start, o.LogicalSize, o.Size);
            }

            return placed;
        }

        static int Scale(int logical, int logicalSize, int size) =>
            logicalSize > 0 ? (int)Math.Round((double)logical * size / logicalSize) : logical;
    }
}
