using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class GeoDepthComponent : SelvagenModuleComponentBase
    {
        public GeoDepthComponent()
            : base("Geology Depth", "GeoDp",
                   "Upload geology depth data", "Geology") { }

        protected override string ModuleTable => "geology";
        public override Guid ComponentGuid => new Guid("A1000002-0001-4000-8000-000000000005");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("DepthMeshID", "DepM", "Depth mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("DepthRef", "DepR", "Depth reference value", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("DepthUsRate", "DepUR", "Depth usability rate", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var depthMeshId)) values["depth_mesh_id"] = depthMeshId;
            if (TryGetNumber(DA, 2, out var depthRef)) values["depth_ref"] = depthRef;
            if (TryGetNumber(DA, 3, out var depthUsRate)) values["depth_usability_rate"] = depthUsRate;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("GeoDepth");
    }
}
