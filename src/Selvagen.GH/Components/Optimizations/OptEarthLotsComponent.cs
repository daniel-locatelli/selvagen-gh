using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class OptEarthLotsComponent : SelvagenModuleComponentBase
    {
        public OptEarthLotsComponent()
            : base("Optimizations Earth Lots", "OptEL",
                   "Upload optimizations earth lots data", "Optimizations") { }

        protected override string ModuleTable => "optimizations";
        public override Guid ComponentGuid => new Guid("A1000004-0001-4000-8000-000000000003");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("ProjectID", "PID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("LotsMeshID", "LotsM", "Lots mesh asset ID", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddNumberParameter("LotsVolCompFill", "LotsCF", "Lots volume compact fill", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("LotsVolBulkFill", "LotsBF", "Lots volume bulking fill", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("LotsVolCut", "LotsCt", "Lots volume cut", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("LotsVolImport", "LotsIm", "Lots volume import", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("LotsVolExport", "LotsEx", "Lots volume export", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var lotsMeshId)) values["earth_mesh_lots_id"] = lotsMeshId;
            if (TryGetNumber(DA, 2, out var lotsVolCompFill)) values["earth_lots_vol_compact_fill"] = lotsVolCompFill;
            if (TryGetNumber(DA, 3, out var lotsVolBulkFill)) values["earth_lots_vol_bulking_fill"] = lotsVolBulkFill;
            if (TryGetNumber(DA, 4, out var lotsVolCut)) values["earth_lots_vol_cut"] = lotsVolCut;
            if (TryGetNumber(DA, 5, out var lotsVolImport)) values["earth_lots_vol_import"] = lotsVolImport;
            if (TryGetNumber(DA, 6, out var lotsVolExport)) values["earth_lots_vol_export"] = lotsVolExport;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("OptEarthLots");
    }
}
