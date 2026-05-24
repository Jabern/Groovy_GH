// Author: Jaber Mohammed <jaberlama123@gmail.com>
// Disclaimer: This code is only for demonstration, it is not production
// ready nor useful, this is only for fun :)
// AI Usage Disclaimer: Since programming is my hobby and i just like
// making cool stuff, i use AI for debugging or helping me out write
// documentation and good code, the architecture, decisions, and
// structure are all mine.

using Grasshopper.Kernel;
using Groovy_GH.Core;
using System;

namespace Groovy_GH
{
    public class Groovy_SettingsComponent : GH_Component
    {
        private static Groovy_SettingsForm? _openForm;

        public Groovy_SettingsComponent()
          : base("Groovy Settings", "GSettings",
            "Right-click → Open Settings to open the visualizer.",
            "Groovy", "Panel")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override Guid ComponentGuid
            => new("8e3d1a5c-77f2-4b98-9d15-26f0e43e82b1");

        protected override void RegisterInputParams(
            GH_InputParamManager pManager)
        {
        }

        protected override void RegisterOutputParams(
            GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Settings", "S",
                "JSON config. Wire to main Groovy component S input.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string json = GroovyState.ActiveConfig.ToJson();
            DA.SetData(0, json);
        }

        public static void OpenForm()
        {
            // dont recreate the window if its already open, just bring it to front
            if (_openForm == null || !_openForm.IsLoaded)
            {
                _openForm = new Groovy_SettingsForm();
                _openForm.Closed += (s, e) => _openForm = null;
                _openForm.Show();
            }
            else
            {
                _openForm.Activate();
            }
        }

        protected override void AppendAdditionalComponentMenuItems(
            System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            var item = new System.Windows.Forms.ToolStripMenuItem(
                "Open Settings / Visualizer");
            item.Click += (s, e) => OpenForm();
            menu.Items.Insert(0, item);
        }

        protected override System.Drawing.Bitmap Icon => null;
    }
}
