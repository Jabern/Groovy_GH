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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Groovy_GH
{
    public class Groovy_JSONComponent : GH_Component
    {
        private static JSONEditorWindow? _editor;

        public Groovy_JSONComponent()
          : base("Groovy JSON", "GJSON",
            "Manual JSON config editor. Double-click to edit raw JSON.",
            "Groovy", "Panel")
        {
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;
        public override Guid ComponentGuid
            => new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

        protected override void RegisterInputParams(
            GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Import", "I",
                "Optional JSON string to load", GH_ParamAccess.item);
            pManager[0].Optional = true;
        }

        protected override void RegisterOutputParams(
            GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Settings", "S",
                "JSON config string. Wire to main Groovy component.",
                GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string importedJson = "";
            DA.GetData(0, ref importedJson);

            if (!string.IsNullOrEmpty(importedJson))
            {
                try
                {
                    var cfg = MotionMappingConfig.FromJson(importedJson);
                    GroovyState.SetConfig(cfg);
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark,
                        "JSON imported and applied.");
                }
                catch (Exception ex)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error,
                        $"Invalid JSON: {ex.Message}");
                }
            }

            DA.SetData(0, GroovyState.ActiveConfig.ToJson());
        }

        public static void OpenEditor()
        {
            if (_editor == null || !_editor.IsLoaded)
            {
                _editor = new JSONEditorWindow();
                _editor.Closed += (s, e) => _editor = null;
                _editor.Show();
            }
            else _editor.Activate();
        }

        protected override void AppendAdditionalComponentMenuItems(
            System.Windows.Forms.ToolStripDropDown menu)
        {
            base.AppendAdditionalComponentMenuItems(menu);
            var item = new System.Windows.Forms.ToolStripMenuItem(
                "Open JSON Editor");
            item.Click += (s, e) => OpenEditor();
            menu.Items.Insert(0, item);
        }

        protected override System.Drawing.Bitmap Icon => null;
    }

    public class JSONEditorWindow : Window
    {
        private readonly TextBox _textBox;
        private readonly TextBlock _statusLabel;

        public JSONEditorWindow()
        {
            Title = "Groovy JSON Editor";
            Width = 580; Height = 440;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new DockPanel { Margin = new Thickness(8) };

            var topBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };
            var validateBtn = new Button
            {
                Content = "Validate", Width = 60, Height = 24,
                Margin = new Thickness(0, 0, 4, 0)
            };
            var applyBtn = new Button
            {
                Content = "Apply", Width = 60, Height = 24,
                Margin = new Thickness(4, 0, 4, 0)
            };
            var resetBtn = new Button
            {
                Content = "Reset", Width = 60, Height = 24,
                Margin = new Thickness(4, 0, 0, 0)
            };
            _statusLabel = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            topBar.Children.Add(validateBtn);
            topBar.Children.Add(applyBtn);
            topBar.Children.Add(resetBtn);
            topBar.Children.Add(_statusLabel);
            DockPanel.SetDock(topBar, Dock.Top);
            root.Children.Add(topBar);

            _textBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Text = GroovyState.ActiveConfig.ToJson()
            };
            root.Children.Add(_textBox);

            validateBtn.Click += (s, e) =>
            {
                try
                {
                    MotionMappingConfig.FromJson(_textBox.Text);
                    _statusLabel.Text = "✓ Valid JSON";
                    _statusLabel.Foreground = Brushes.Green;
                }
                catch (Exception ex)
                {
                    _statusLabel.Text = $"✗ {ex.Message}";
                    _statusLabel.Foreground = Brushes.Red;
                }
            };

            applyBtn.Click += (s, e) =>
            {
                try
                {
                    var cfg = MotionMappingConfig.FromJson(_textBox.Text);
                    GroovyState.SetConfig(cfg);
                    _statusLabel.Text = "✓ Applied";
                    _statusLabel.Foreground = Brushes.Green;
                }
                catch (Exception ex)
                {
                    _statusLabel.Text = $"✗ {ex.Message}";
                    _statusLabel.Foreground = Brushes.Red;
                }
            };

            resetBtn.Click += (s, e) =>
            {
                _textBox.Text = GroovyState.ActiveConfig.ToJson();
                _statusLabel.Text = "Reset to current config";
                _statusLabel.Foreground = Brushes.Gray;
            };

            Content = root;
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) Close();
            };
        }
    }
}
