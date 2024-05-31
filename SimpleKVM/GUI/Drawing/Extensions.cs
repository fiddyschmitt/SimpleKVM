using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SimpleKVM.GUI.Drawing
{
    public static class Extensions
    {
        public static List<Point> AnchorPoints(this Rectangle rectangle, bool justLeftAndRight)
        {
            List<Point> result;

            if (justLeftAndRight)
            {
                result = [
                    new Point(rectangle.Left, (int)(rectangle.Top + rectangle.Height / 2f)),
                    new Point(rectangle.Right, (int)(rectangle.Top + rectangle.Height / 2f)),
                ];
            }
            else
            {
                var xList = new[] { rectangle.Left, (int)(rectangle.Left + rectangle.Width / 2f), rectangle.Right };
                var yList = new[] { rectangle.Top, (int)(rectangle.Top + rectangle.Height / 2f), rectangle.Bottom };

                result = xList
                                .SelectMany(x => yList.Select(y => new Point(x, y)))
                                .ToList();
            }

            return result;
        }

        public static double DistanceTo(this Point from, Point to)
        {
            var result = Math.Sqrt(Math.Pow((from.X - to.X), 2) + Math.Pow((from.Y - to.Y), 2));
            return result;
        }

        public static IEnumerable<T> Recurse<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> childSelector, bool depthFirst = false)
        {
            var queue = new List<T>(source); ;

            while (queue.Count > 0)
            {
                var item = queue[0];
                queue.RemoveAt(0);

                var children = childSelector(item);

                if (depthFirst)
                {
                    queue.InsertRange(0, children);
                }
                else
                {
                    queue.AddRange(children);
                }

                yield return item;
            }
        }

        public static IEnumerable<(T? Previous, T Current, T? Next)> Sandwich<T>(this IEnumerable<T> source, T? beforeFirst = default, T? afterLast = default)
        {
            var sourceList = source.ToList();

            T? previous = beforeFirst;
            T? current = sourceList.First();

            foreach (var next in sourceList.Skip(1))
            {
                yield return (previous, current, next);

                previous = current;
                current = next;
            }

            yield return (previous, current, afterLast);
        }

        public static IEnumerable<T> Recurse<T>(this T source, Func<T, T> childSelector, bool depthFirst = false)
        {
            var list = new List<T>() { source };
            var childListSelector = new Func<T, IEnumerable<T>>(item =>
            {
                var child = childSelector(item);
                if (child == null)
                {
                    return new List<T>();
                }
                else
                {
                    return new List<T>() { child };
                }
            });

            foreach (var result in Recurse(list, childListSelector, depthFirst))
            {
                yield return result;
            }
        }

        public static SizeF MeasureText(this Font font, string text)
        {
            using Graphics g = Graphics.FromHwnd(IntPtr.Zero);
            var labelSize = g.MeasureString(text, font);
            g.Dispose();

            return labelSize;
        }

        public static Point Add(this Point point0, Point point1)
        {
            var result = new Point(point0.X + point1.X, point0.Y + point1.Y);
            return result;
        }

        public static Point Minus(this Point point0, Point point1)
        {
            var result = new Point(point0.X - point1.X, point0.Y - point1.Y);
            return result;
        }

        public static PointF Midpoint(this PointF a, PointF b)
        {
            var result = new PointF(
                (a.X + b.X) / 2,
                (a.Y + b.Y) / 2);
            return result;
        }
    }
}