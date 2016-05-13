﻿using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AldursLab.Essentials.Extensions.DotNet;
using AldursLab.Essentials.Synchronization;
using AldursLab.PersistentObjects;
using AldursLab.WurmApi;
using AldursLab.WurmAssistant3.Areas.Config.Contracts;
using AldursLab.WurmAssistant3.Areas.Config.Modules;
using AldursLab.WurmAssistant3.Areas.Config.Views;
using AldursLab.WurmAssistant3.Areas.Core.Components.Singletons;
using AldursLab.WurmAssistant3.Areas.Core.Contracts;
using AldursLab.WurmAssistant3.Areas.Features.Contracts;
using AldursLab.WurmAssistant3.Areas.Logging.Contracts;
using AldursLab.WurmAssistant3.Areas.Logging.Views;
using AldursLab.WurmAssistant3.Areas.MainMenu.Views;
using AldursLab.WurmAssistant3.Areas.Native.Constants;
using AldursLab.WurmAssistant3.Areas.Native.Contracts;
using AldursLab.WurmAssistant3.Areas.Native.Modules;
using AldursLab.WurmAssistant3.Messages;
using AldursLab.WurmAssistant3.Messages.Requests;
using AldursLab.WurmAssistant3.Properties;
using AldursLab.WurmAssistant3.Utils.WinForms;
using AldursLab.WurmAssistant3.Utils.WinForms.Reusables;
using Caliburn.Micro;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Action = System.Action;

namespace AldursLab.WurmAssistant3.Areas.Core.Views
{
    [PersistentObject("MainForm")]
    public partial class MainForm : PersistentForm, IHandle<MainViewRestoreRequest>, IHandle<ApplicationShutdownRequest>
    {
        private readonly string[] args;
        MouseDragManager mouseDragManager;

        [JsonObject(MemberSerialization.OptIn)]
        class Settings
        {
            public MainForm MainForm { get; set; }

            [JsonProperty]
            int savedWidth;
            [JsonProperty]
            int savedHeight;
            [JsonProperty]
            bool baloonTrayTooltipShown;

            public int SavedWidth
            {
                get { return savedWidth; }
                set { savedWidth = value; MainForm.FlagAsChanged(); }
            }

            public int SavedHeight
            {
                get { return savedHeight; }
                set { savedHeight = value; MainForm.FlagAsChanged(); }
            }

            public bool BaloonTrayTooltipShown
            {
                get { return baloonTrayTooltipShown; }
                set { baloonTrayTooltipShown = value; MainForm.FlagAsChanged(); }
            }
        }

        ILogger logger;

        CoreBootstrapper bootstrapper;
        bool bootstrapped = false;

        IWurmAssistantConfig wurmAssistantConfig;

        MinimizationManager minimizationManager;

        [JsonProperty]
        Settings settings;

        bool persistentStateLoaded;
        IMessageBus messageBus;
        ISystemTrayContextMenu systemTrayContextMenu;

        public MainForm(string[] args)
        {
            if (args == null) throw new ArgumentNullException("args");
            this.args = args;
            InitializeComponent();
        }

        //TODO this is temporary until MainForm becomes a part of resolve
        public IMessageBus MessageBus
        {
            get { return messageBus; }
            set { messageBus = value; messageBus.Subscribe(this); }
        }

        //TODO this is temporary until MainForm becomes a part of resolve
        public ISystemTrayContextMenu SystemTrayContextMenu
        {
            get { return systemTrayContextMenu; }
            set
            {
                systemTrayContextMenu = value;
                systemTrayContextMenu.ShowMainWindowClicked += ShowRestore;
                systemTrayContextMenu.ExitWurmAssistantClicked += (sender, eventArgs) => Shutdown();
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ConsoleArgsManager argsManager = null;
            try
            {
                argsManager = new ConsoleArgsManager();
                if (argsManager.WurmUnlimitedMode)
                {
                    this.Text = "Wurm Assistant Unlimited";
                    this.Icon = Resources.WurmAssistantUnlimitedIcon;
                }
                else
                {
                    this.Text = "Wurm Assistant";
                }

                mouseDragManager = new MouseDragManager(this);
                settings = new Settings();

                minimizationManager = new MinimizationManager(this);

                bootstrapper = new CoreBootstrapper(this);
                bootstrapper.PreBootstrap();
                wurmAssistantConfig = bootstrapper.WurmAssistantConfig;
                logger = bootstrapper.GetCoreLogger();

                InitTimer.Enabled = true;
            }
            catch (LockFailedException)
            {
                try
                {
                    // this will probably fail on non windows even under WINE

                    if (argsManager != null)
                    {
                        INativeCalls rwc = new Win32NativeCalls();
                        if (argsManager.WurmUnlimitedMode)
                        {
                            rwc.AttemptToBringMainWindowToFront("AldursLab.WurmAssistant3", "Unlimited");
                        }
                        else
                        {
                            rwc.AttemptToBringMainWindowToFront("AldursLab.WurmAssistant3", @"^((?!Unlimited).)*$");
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (logger != null) logger.Error(exception, "");
                }

                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception exception)
            {
                var view = new UniversalTextDisplayView();
                view.Text = "Wurm Assistant - ERROR";
                view.ContentText = exception.ToString();
                view.ShowDialog();
                Shutdown();
            }
        }

        protected override void OnPersistentDataLoaded()
        {
            settings.MainForm = this;
        }

        public void AfterPersistentStateLoaded()
        {
            persistentStateLoaded = true;
            RestoreSizeFromSaved();
        }

        void RestoreSizeFromSaved()
        {
            if (settings.SavedWidth != default(int) && settings.SavedHeight != default(int))
            {
                this.Size = new Size()
                {
                    Height = settings.SavedHeight,
                    Width = settings.SavedWidth
                };
            }
        }

        public void SetLogView(LogView logView)
        {
            LogViewPanel.Controls.Clear();
            logView.Dock = DockStyle.Fill;
            LogViewPanel.Controls.Add(logView);
        }

        public void SetFeaturesManager(IFeaturesManager featuresManager)
        {
            featuresFlowPanel.Controls.Clear();

            var features = featuresManager.Features;

            foreach (var f in features)
            {
                var feature = f;
                Button btn = new Button
                {
                    Size = new Size(80, 80),
                    BackgroundImageLayout = ImageLayout.Stretch,
                    BackgroundImage = feature.Icon
                };

                btn.Click += (o, args) => feature.Show();
                toolTips.SetToolTip(btn, feature.Name);
                featuresFlowPanel.Controls.Add(btn);
            }
        }

        public void SetMenuView(MenuView menuView)
        {
            MenuViewPanel.Controls.Clear();
            menuView.Dock = DockStyle.Fill;
            MenuViewPanel.Controls.Add(menuView);
        }

        private void InitTimer_Tick(object sender, EventArgs e)
        {
            if (!bootstrapped)
            {
                InitTimer.Enabled = false;

                try
                {
                    bootstrapper.Bootstrap();
                }
                catch (ConfigCancelledException)
                {
                    Shutdown();
                }
                catch (Exception exception)
                {
                    bool handled = false;

                    // Trying to handle missing irrKlang dependencies.
                    // Checking for specific part of the message, because the rest of it may be localized.
                    if (exception.GetType() == typeof (FileNotFoundException)
                        && exception.Message.Contains("'irrKlang.NET4.dll'"))
                    {
                        handled = HandleMissingIrrklangDependency(exception);
                    }

                    if (!handled)
                    {
                        HandleOtherError(exception);
                    }
                    
                    return;
                }
                finally
                {
                    mouseDragManager.Hook();
                }

                bootstrapped = true;
            }
        }

        void HandleOtherError(Exception exception)
        {
            bool restart = false;
            var btn1 = new Button()
            {
                Text = "Reset Wurm Assistant config",
                Height = 28,
                Width = 220
            };
            btn1.Click += (o, args) =>
            {
                if (bootstrapper.TryResetConfig())
                {
                    MessageBox.Show("Reset complete, please restart.", "Done", MessageBoxButtons.OK);
                }
                else
                {
                    MessageBox.Show("Reset was not possible.", "Sorry", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            var btn2 = new Button()
            {
                Text = "Restart Wurm Assistant",
                Height = 28,
                Width = 220,
                DialogResult = DialogResult.OK
            };
            btn2.Click += (o, args) => restart = true;
            var btn3 = new Button()
            {
                Text = "Close Wurm Assistant",
                Height = 28,
                Width = 220,
                DialogResult = DialogResult.OK
            };
            var view = new UniversalTextDisplayView(btn3, btn2, btn1)
            {
                Text = "OH NO!!",
                ContentText = "Application startup was interrupted by an ugly error! "
                              + Environment.NewLine
                              + Environment.NewLine + exception.ToString()
            };

            view.ShowDialog();
            if (restart) Restart();
            else Shutdown();
        }

        bool HandleMissingIrrklangDependency(Exception exception)
        {
            var visualCppDetector = new VisualCppRedistDetector();
            // trying to detect if Visual C++ Redistributable x86 SP1 is installed
            if (!visualCppDetector.IsInstalled2010X86Sp1())
            {
                var form = new VisualCppMissingHelperView(exception);
                form.ShowDialog();
                Shutdown();
                return true;
            }
            return false;
        }

        private void MainView_FormClosing(object sender, FormClosingEventArgs e)
        {
        }
        
        public void Restart()
        {
            Application.Restart();
            // Restart does not automatically shutdown WPF application
            Shutdown();
        }

        public void Shutdown()
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void MainView_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (bootstrapper != null) bootstrapper.Dispose();
        }

        void ShowRestore(object sender, EventArgs e)
        {
            minimizationManager.Restore();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                if (persistentStateLoaded)
                {
                    settings.SavedWidth = this.Width;
                    settings.SavedHeight = this.Height;
                }
            }
        }

        class MouseDragManager
        {
            [DllImport("user32.dll")]
            private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
            [DllImport("user32.dll")]
            private static extern bool ReleaseCapture();


            readonly MainForm mainForm;

            public MouseDragManager([NotNull] MainForm mainForm)
            {
                if (mainForm == null) throw new ArgumentNullException("mainForm");
                this.mainForm = mainForm;
            }

            public void Hook()
            {
                var logViewButtonsPanel = mainForm.LogViewPanel.Controls.Find("logViewButtonsFlowPanel", true).FirstOrDefault();

                WireControls(mainForm.featuresFlowPanel,
                    mainForm.LogViewPanel,
                    mainForm.MenuViewPanel,
                    logViewButtonsPanel);
            }

            void WireControls(params object[] objects)
            {
                foreach (var obj in objects)
                {
                    var control = obj as Control;
                    if (control != null)
                    {
                        control.MouseDown += OnMouseDownHandler;
                    }
                }
            }

            void OnMouseDownHandler(object sender, MouseEventArgs mouseEventArgs)
            {
                if (mouseEventArgs.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(mainForm.Handle, Win32Hooks.WM_NCLBUTTONDOWN, Win32Hooks.HT_CAPTION, 0);
                }
            }
        }

        void IHandle<MainViewRestoreRequest>.Handle(MainViewRestoreRequest message)
        {
            ShowRestore(new object(), EventArgs.Empty);
        }

        void IHandle<ApplicationShutdownRequest>.Handle(ApplicationShutdownRequest message)
        {
            Shutdown();
        }
    }
}
