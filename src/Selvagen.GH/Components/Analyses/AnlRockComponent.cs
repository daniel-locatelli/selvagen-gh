using System;
using System.Collections.Generic;
using Grasshopper.Kernel;

namespace Selvagen.GH.Components
{
    public class AnlRockComponent : SelvagenModuleComponentBase
    {
        public AnlRockComponent()
            : base("Analyses Rock", "AnlRk",
                   "Upload analysis rock data. [Rocha]", "05 Analysis") { }

        protected override string ModuleTable => "analyses";
        public override Guid ComponentGuid => new Guid("A1000003-0001-4000-8000-000000000003");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID [ID do Projeto]", GH_ParamAccess.item);
            pManager.AddTextParameter("Mesh ID", "M", "Mesh asset ID [Malha]", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Height Labels ID", "LH", "Height labels asset ID [Rótulos de Altura]", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddTextParameter("Volume Labels ID", "LV", "Volume labels asset ID [Rótulos de Volume]", GH_ParamAccess.item);
            pManager[3].Optional = true;
            pManager.AddNumberParameter("Height Minimum", "HMin", "Minimum height (m) [Altura Mínima]", GH_ParamAccess.item);
            pManager[4].Optional = true;
            pManager.AddNumberParameter("Height Maximum", "HMax", "Maximum height (m) [Altura Máxima]", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("Total Volume Cut", "TVC", "Total volume cut [Volume Total de Corte]", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddTextParameter("Legend ID", "LgdID",
                "Color legend asset ID [ID da Legenda]", GH_ParamAccess.item);
            pManager[7].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload [Enviar]", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetText(DA, 1, out var rockMesh)) values["rock_mesh_id"] = rockMesh;
            if (TryGetText(DA, 2, out var rockLabelsH)) values["rock_label_set_height_id"] = rockLabelsH;
            if (TryGetText(DA, 3, out var rockLabelsV)) values["rock_label_set_vol_id"] = rockLabelsV;
            if (TryGetNumber(DA, 4, out var rockHMin)) values["rock_height_min"] = rockHMin;
            if (TryGetNumber(DA, 5, out var rockHMax)) values["rock_height_max"] = rockHMax;
            if (TryGetNumber(DA, 6, out var rockTotalVol)) values["rock_total_vol_cut"] = rockTotalVol;
            if (TryGetText(DA, Params.Input.Count - 2, out var legendId)) values["rock_legend_id"] = legendId;
            return values;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("AnlRock");
    }
}
