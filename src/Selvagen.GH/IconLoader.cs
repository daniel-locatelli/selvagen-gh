using System.Drawing;
using System.Reflection;

namespace Selvagen.GH
{
    internal static class IconLoader
    {
        /// <summary>
        /// Load a 24x24 icon from embedded resources.
        /// Resource names follow: Selvagen.GH.Icons.{name}.png
        /// </summary>
        internal static Bitmap Load(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"Selvagen.GH.Icons.{name}.png";
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                return stream != null ? new Bitmap(stream) : null;
            }
        }
    }
}
