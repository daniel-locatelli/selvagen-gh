using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Selvagen.GH
{
    public class SelvagenInfo : GH_AssemblyInfo
    {
        public override string Name => "Selvagen";
        public override string Description => "Push geometry from Rhino/Grasshopper to the Selvagen platform.";
        public override Guid Id => new Guid("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        public override string AuthorName => "Selvagen";
        public override string AuthorContact => "https://selvagen.com";
        public override Bitmap Icon => null;
    }
}
