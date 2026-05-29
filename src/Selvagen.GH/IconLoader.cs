using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;

namespace Selvagen.GH
{
    internal static class IconLoader
    {
        private static readonly ConcurrentDictionary<string, Bitmap> _cache =
            new ConcurrentDictionary<string, Bitmap>();

        /// <summary>
        /// Load a 24x24 icon from embedded resources, cached by name.
        /// Grasshopper queries the Icon getter frequently (every canvas redraw),
        /// so we memoize to avoid re-opening the resource stream and re-allocating
        /// a Bitmap each time. Resource names follow: Selvagen.GH.Icons.{name}.png
        /// </summary>
        internal static Bitmap Load(string name)
        {
            return _cache.GetOrAdd(name, static key =>
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"Selvagen.GH.Icons.{key}.png";
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    return stream != null ? new Bitmap(stream) : null;
                }
            });
        }
    }
}
