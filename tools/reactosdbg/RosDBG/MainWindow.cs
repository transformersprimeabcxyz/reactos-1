﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using AbstractPipe;
using DebugProtocol;
using KDBGProtocol;
using DbgHelpAPI;

namespace RosDBG
{
    public delegate void NoParamsDelegate();

    public partial class MainWindow : Form, IShell
    {
        Point mMousePosition;
        bool mRunning;
        DebugConnection.Mode mConnectionMode;
        ulong mCurrentEip;
        string mSourceRoot = Settings.SourceDirectory, mCurrentFile;
        int mCurrentLine;
        DebugConnection mConnection = new DebugConnection();
        SymbolContext mSymbolContext;
        Dictionary<uint, Module> mModules = new Dictionary<uint, Module>();
        Dictionary<string, SourceView> mSourceFiles = new Dictionary<string, SourceView>();
        public event CopyEventHandler CopyEvent;
        
        public DebugConnection DebugConnection
        {
            get { return mConnection; }
        }

        public MainWindow()
        {
            InitializeComponent();
            mSymbolContext = new SymbolContext();
            RegisterSubwindows(Assembly.GetExecutingAssembly());
        }

        void Rehighlight(SourceView vw)
        {
            vw.ClearHighlight();
            vw.AddHighlight(mCurrentLine, Color.SteelBlue, Color.White);
            vw.ScrollTo(mCurrentLine);
        }

        void TryToDisplaySource()
        {
            if (mCurrentFile == null || mCurrentFile == "unknown") return;
            OpenSourceFile(Path.Combine(mSourceRoot, mCurrentFile));
        }

        private void OpenSourceFile(string FileName)
        {
            SourceView theSourceView;
            if (File.Exists(FileName))
            {
                if (mSourceFiles.TryGetValue(FileName, out theSourceView))
                    Rehighlight(theSourceView);
                else
                {
                    theSourceView = new SourceView(Path.GetFileName(FileName));
                    mSourceFiles[FileName] = theSourceView;
                    AddTab(theSourceView);
                    theSourceView.SourceFile = FileName;
                    Rehighlight(theSourceView);
                }
            }
        }

        public void FocusAddress(ulong eipToFocus)
        {
            KeyValuePair<string, int> fileline = mSymbolContext.GetFileAndLine(eipToFocus);
            mCurrentFile = fileline.Key;
            mCurrentLine = fileline.Value;
            TryToDisplaySource();
        }

        void ComposeTitleString()
        {
            FocusAddress(mCurrentEip);
            RunStatus.Text = "ConnectionMode: " + mConnectionMode + " - Running: " + mRunning + " - Source Location: " + mCurrentFile + ":" + mCurrentLine;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            mSymbolContext.Initialize();
            mConnection.DebugConnectionModeChangedEvent += DebugConnectionModeChangedEvent;
            mConnection.DebugRunningChangeEvent += DebugRunningChangeEvent;
            mConnection.DebugRegisterChangeEvent += DebugRegisterChangeEvent;
            mConnection.DebugModuleChangedEvent += DebugModuleChangedEvent;
            ComposeTitleString();
            mSymbolContext.ReactosOutputPath = Settings.OutputDirectory;
        }

        void CanCopyChanged(object sender, CanCopyChangedEventArgs args)
        {
            copyToolStripMenuItem.Enabled = args.Enabled;   
        }

        void DebugModuleChangedEvent(object sender, DebugModuleChangedEventArgs args)
        {
            Module themod;
            if (!mModules.TryGetValue(args.ModuleAddr, out themod) ||
                themod.ShortName != args.ModuleName.ToLower())
            {
                mModules[args.ModuleAddr] = new Module(args.ModuleAddr, args.ModuleName);
                mSymbolContext.LoadModule(args.ModuleName, args.ModuleAddr);
            }
            Invoke(Delegate.CreateDelegate(typeof(NoParamsDelegate), this, "ComposeTitleString"));
        }

        void DebugRunningChangeEvent(object sender, DebugRunningChangeEventArgs args)
        {
            mRunning = args.Running;
            Invoke(Delegate.CreateDelegate(typeof(NoParamsDelegate), this, "ComposeTitleString"));
        }

        void DebugConnectionModeChangedEvent(object sender, DebugConnectionModeChangedEventArgs args)
        {
            mConnectionMode = args.Mode;
            Invoke(Delegate.CreateDelegate(typeof(NoParamsDelegate), this, "ComposeTitleString"));
        }

        void DebugRegisterChangeEvent(object sender, DebugRegisterChangeEventArgs args)
        {
            mCurrentEip = args.Registers.Eip;
            Invoke(Delegate.CreateDelegate(typeof(NoParamsDelegate), this, "ComposeTitleString"));
        }

        public void RegisterSubwindows(Assembly a)
        {
            foreach (Type t in a.GetTypes())
            {
                object[] debugAttribute = t.GetCustomAttributes(typeof(DebugControlAttribute), false);
                if (debugAttribute.Length > 0)
                {
                    Type x = t;
                    EventHandler create =
                        delegate(object sender, EventArgs args)
                        {
                            Control ctrl = (Control)x.GetConstructor(Type.EmptyTypes).Invoke(new object[] { });
                            IUseDebugConnection usedbg = ctrl as IUseDebugConnection;
                            if (usedbg != null)
                                usedbg.SetDebugConnection(mConnection);
                            IUseSymbols usesym = ctrl as IUseSymbols;
                            if (usesym != null)
                                usesym.SetSymbolProvider(mSymbolContext);
                            IUseShell useshell = ctrl as IUseShell;
                            if (useshell != null)
                                useshell.SetShell(this);
                            AddTab(ctrl);
                        };
                    Control c = (Control)x.GetConstructor(Type.EmptyTypes).Invoke(new object[] { });
                    NewWindowItem.DropDownItems.Add( c.Tag != null ? c.Tag.ToString() : t.Name, null, create);

                    object[] buildNow = t.GetCustomAttributes(typeof(BuildAtStartupAttribute), false);
                    if (buildNow.Length > 0)
                        create(this, new EventArgs());
                }
            }
        }

        public void AddTab(Control ctrl)
        {
            SuspendLayout();
            TabPage tp = new TabPage(ctrl.Text);
            tp.Controls.Add(ctrl);
            tp.Text = ctrl.Tag != null ? ctrl.Tag.ToString() : ctrl.GetType().Name;
            ctrl.Dock = DockStyle.Fill;
            WorkTabs.Controls.Add(tp);

            if (ctrl.GetType() == typeof(SourceView))
                ((SourceView)ctrl).CanCopyChangedEvent += CanCopyChanged;
            else if (ctrl.GetType() == typeof(RawTraffic))
                ((RawTraffic)ctrl).CanCopyChangedEvent += CanCopyChanged;

            ResumeLayout();
        }

        public Control RemoveCurrentTab()
        {
            TabPage tp = (TabPage)WorkTabs.SelectedTab;
            if (tp == null || tp.Controls.Count < 1) return null;
            Control current = tp.Controls[0];
            WorkTabs.Controls.Remove(tp);
            return current;
        }

        private void closeCurrentTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Control ctrl = RemoveCurrentTab();
            if (ctrl != null) ctrl.Dispose();
        }

        private void detachCurrentTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Control ctrl = RemoveCurrentTab();
            if (ctrl != null)
            {
                HostWindow hw = new HostWindow();
                hw.Content = ctrl;
                hw.RedockControlEvent += RedockControlEventFired;
                hw.Show();
            }
        }

        void RedockControlEventFired(object sender, RedockControlEventArgs args)
        {
            AddTab(args.Control);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        // Fixup various editing tools ...
        private void Interaction_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                TextBox tb = (TextBox)sender;
                e.Handled = true;
                string command = tb.Text;
                tb.Text = "";
                if (InteractiveInputEvent != null)
                    InteractiveInputEvent(sender, new InteractiveInputEventArgs(this, command));
            }
        }

        public delegate void InteractiveInputEventHandler(object sender, InteractiveInputEventArgs args);
        public event InteractiveInputEventHandler InteractiveInputEvent;

        private void connectTCPIPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TCPTargetSelect targetSelect = new TCPTargetSelect();
            if (targetSelect.ShowDialog() == DialogResult.OK)
            {
                mConnection.Close();
                mConnection.StartTCP(targetSelect.Host, targetSelect.Port);
            }
        }

        private void breakToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mConnection.Break();
        }

        private void stepToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mConnection.Step();
        }

        private void nextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mConnection.Next();
        }

        private void continueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mConnection.Go();
        }

        private void symbolDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.ShowSettings();
            mSourceRoot = Settings.SourceDirectory;
            mSymbolContext.ReactosOutputPath = Settings.OutputDirectory;
        }

        private void connectSerialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SerialTargetSelect targetSelect = new SerialTargetSelect();
            if (targetSelect.ShowDialog() == DialogResult.OK)
            {
                mConnection.Close();
                mConnection.StartSerial(targetSelect.Port, targetSelect.Baudrate);
            }
        }

        private void openSourceFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "Sourcefiles (*.c;*.cpp)|*.c;*.cpp";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                OpenSourceFile(fileDialog.FileName);
                WorkTabs.SelectTab(WorkTabs.TabCount - 1);   
            }
        }

        private void WorkTabs_MouseClick(object sender, MouseEventArgs e)
        {
            switch (e.Button) 
            {
                case MouseButtons.Right:
                    contextMenuTabStrip.Show(WorkTabs.PointToScreen(e.Location));
                    mMousePosition = e.Location;
                    break;
                case MouseButtons.Middle:
                    SelectTabFromPosition(e.Location);
                    closeCurrentTabToolStripMenuItem_Click(this, null);
                    break;
                default:
                    break;
            }
        }

        private void SelectTabFromPosition(Point Position)
        {
            for (int i = 0; i < WorkTabs.TabCount; i++)
            {
                if (WorkTabs.GetTabRect(i).Contains(Position))
                {
                    if (WorkTabs.SelectedIndex != i)
                        WorkTabs.SelectTab(i);
                    break;
                }
            }
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectTabFromPosition(mMousePosition);
            closeCurrentTabToolStripMenuItem_Click(this, null);
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReactOSWeb Help = new ReactOSWeb("Help", "http://www.reactos.org/wiki/index.php/ReactOSDbg");
            AddTab(Help);
            WorkTabs.SelectTab(WorkTabs.TabCount - 1);   
        }

        private void connectPipeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PipeTargetSelect targetSelect = new PipeTargetSelect();
            if (targetSelect.ShowDialog() == DialogResult.OK)
            {
                mConnection.Close();
                mConnection.StartPipe(targetSelect.PipeName, targetSelect.PipeMode);
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CopyEvent != null)
                CopyEvent(this, new CopyEventArgs(WorkTabs.SelectedTab.Controls[0]));
        }

        private void MainWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            mConnection.Close(true);
        }

    }

    public class InteractiveInputEventArgs : EventArgs
    {
        string mCommand;
        public string Command { get { return mCommand; } }
        public InteractiveInputEventArgs(object sender, string cmd) { mCommand = cmd; }
    }

    public class CanCopyChangedEventArgs : EventArgs
    {
        public readonly bool Enabled;
        public CanCopyChangedEventArgs(bool enabled)
        {
            Enabled = enabled;
        }
    }

    public class CopyEventArgs : EventArgs
    {
        public readonly object Obj;
        public CopyEventArgs(object obj)
        {
            Obj = obj;
        }
    }

    public delegate void CanCopyChangedEventHandler(object sender, CanCopyChangedEventArgs args);
    public delegate void CopyEventHandler(object sender, CopyEventArgs args);
}