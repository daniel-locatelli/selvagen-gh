using System;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public abstract class SelvagenDownloadComponentBase : GH_Component
    {
        protected SelvagenDownloadComponentBase(string name, string nickname, string description)
            : base(name, nickname, description, "Selvagen", "08 Assets") { }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => null;
    }
}
