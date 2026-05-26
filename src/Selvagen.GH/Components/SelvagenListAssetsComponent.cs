using System;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Selvagen.Core.Api;
using Selvagen.Core.Models;

namespace Selvagen.GH.Components
{
    public class SelvagenListAssetsComponent : SelvagenSelectableComponentBase<AssetInfo>, IFilterDropdownComponent
    {
        private static readonly string[] _filterOptions =
            { "meshes", "curve_sets", "label_sets", "animation_sequences" };

        private static readonly string[] _filterDisplayNames =
            { "Meshes", "Curve Sets", "Label Sets", "Animation Sequences" };

        private string _assetType = "meshes";

        public SelvagenListAssetsComponent()
            : base("List Assets", "SvAssets",
                "List meshes, curve sets, label sets, or animation sequences for a project. Pick one from the inline dropdown. [Listar Assets]",
                "08 Assets")
        { }

        protected override string SelectedIdLabel => "Asset ID";
        protected override string SelectedIdNick => "AstID";
        protected override string SelectedNameLabel => "Asset Name";
        protected override string SelectedNameNick => "AstName";

        public override Guid ComponentGuid => new Guid("A17B2C3D-E4F5-6789-0ABC-DEF123456789");

        // ── IFilterDropdownComponent ─────────────────────────────────────────

        public string[] FilterOptions => _filterOptions;
        public string[] FilterDisplayNames => _filterDisplayNames;

        public string SelectedFilter
        {
            get => _assetType;
            set
            {
                if (value == _assetType) return;
                _assetType = value;
                _cachedItems = null;
                _cachedKey = null;
                ExpireSolution(true);
            }
        }

        // ── Inputs / fetch ───────────────────────────────────────────────────

        protected override void RegisterFilterInputs(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project ID to list assets for", GH_ParamAccess.item, "");
            pManager[0].Optional = true;
        }

        protected override object[] CaptureInputs(IGH_DataAccess da)
        {
            string projectId = "";
            da.GetData(0, ref projectId);
            return new object[] { projectId ?? "", _assetType };
        }

        protected override async Task<AssetInfo[]> FetchAsync(SelvagenClient client, object[] inputs)
        {
            string projectId = (string)inputs[0];
            string assetType = (string)inputs[1];

            if (string.IsNullOrEmpty(projectId)) return new AssetInfo[0];

            switch (assetType.ToLowerInvariant())
            {
                case "meshes":
                case "mesh":
                    return await client.ListMeshesAsync(projectId).ConfigureAwait(false);
                case "curve_sets":
                case "curves":
                    return await client.ListCurveSetsAsync(projectId).ConfigureAwait(false);
                case "label_sets":
                case "labels":
                case "text":
                    return await client.ListLabelSetsAsync(projectId).ConfigureAwait(false);
                case "animation_sequences":
                case "animations":
                    return await client.ListAnimationSequencesAsync(projectId).ConfigureAwait(false);
                default:
                    throw new ArgumentException($"Unknown asset type: {assetType}. Use meshes, curve_sets, label_sets, or animation_sequences.");
            }
        }

        protected override string GetId(AssetInfo item) => item.Id;
        protected override string GetDisplayName(AssetInfo item) => item.Name;

        // ── Persistence ──────────────────────────────────────────────────────

        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;
            writer.SetString("AssetType", _assetType);
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader)) return false;
            string type = null;
            reader.TryGetString("AssetType", ref type);
            if (!string.IsNullOrEmpty(type))
                _assetType = type;
            return true;
        }

        protected override System.Drawing.Bitmap Icon => IconLoader.Load("ListAssets");
    }
}
