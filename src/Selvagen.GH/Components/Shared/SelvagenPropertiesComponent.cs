using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Grasshopper.Kernel;
using GH_IO.Serialization;

namespace Selvagen.GH.Components
{
    public class SelvagenPropertiesComponent : SelvagenModuleComponentBase
    {
        internal static readonly string[] ModuleOptions = { "topography", "geology", "analyses", "optimizations" };
        internal static readonly string[] ModuleDisplayNames = { "Topography", "Geology", "Analyses", "Optimizations" };

        private string _selectedModule = "topography";

        public SelvagenPropertiesComponent()
            : base("Custom Properties", "SvProps",
                   "Upload custom JSON properties to any module. [Propriedades Personalizadas]", "07 Shared") { }

        protected override string ModuleTable => _selectedModule;

        public override Guid ComponentGuid => new Guid("A1000005-0001-4000-8000-000000000001");

        public string SelectedModule
        {
            get => _selectedModule;
            set
            {
                if (_selectedModule != value && ModuleOptions.Contains(value))
                {
                    _selectedModule = value;
                    Message = ModuleDisplayNames[Array.IndexOf(ModuleOptions, value)];
                    ExpireSolution(true);
                }
            }
        }

        public override void CreateAttributes()
        {
            m_attributes = new SelvagenPropertiesAttributes(this);
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project ID", "PrjID", "Project UUID", GH_ParamAccess.item);
            pManager.AddTextParameter("JSON", "J", "Custom properties as JSON string", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddBooleanParameter("Upload", "Go", "Set to true to upload", GH_ParamAccess.item, false);
        }

        protected override Dictionary<string, object> CollectValues(IGH_DataAccess DA)
        {
            var values = new Dictionary<string, object>();
            if (TryGetJson(DA, 1, out var props))
                values["properties"] = props;
            return values;
        }

        public override bool Write(GH_IWriter writer)
        {
            writer.SetString("SelectedModule", _selectedModule);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            if (reader.ItemExists("SelectedModule"))
                _selectedModule = reader.GetString("SelectedModule");
            Message = ModuleDisplayNames[Array.IndexOf(ModuleOptions, _selectedModule)];
            return base.Read(reader);
        }

        protected override void AppendAdditionalComponentMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            Menu_AppendSeparator(menu);
            for (int i = 0; i < ModuleOptions.Length; i++)
            {
                var option = ModuleOptions[i];
                var display = ModuleDisplayNames[i];
                Menu_AppendItem(menu, display, (s, e) => SelectedModule = option, true, _selectedModule == option);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        protected override System.Drawing.Bitmap Icon => IconLoader.Load("Properties");
    }
}
