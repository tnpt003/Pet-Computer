using System;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DesktopPet;

public partial class App : Application
{
    private Forms.NotifyIcon? tray;
    private PetWindow? pet;
    private System.Drawing.Icon? trayIcon;
    private System.Threading.Mutex? instanceMutex;
    private System.Threading.EventWaitHandle? showControls;
    private System.Threading.RegisteredWaitHandle? controlsWait;
    private bool ownsInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string key = "Local\\DesktopPet-" + Environment.UserName + "-" + System.Diagnostics.Process.GetCurrentProcess().SessionId;
        instanceMutex = new System.Threading.Mutex(false, key);
        try { ownsInstance = instanceMutex.WaitOne(0); }
        catch (System.Threading.AbandonedMutexException) { ownsInstance = true; }
        showControls = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, key + "-controls");
        if (!ownsInstance) { showControls.Set(); Shutdown(); return; }
        PetLog.Install();
        // Log only; existing crash behavior is unchanged.
        DispatcherUnhandledException += (_, args) => System.Diagnostics.Trace.WriteLine($"Unhandled UI exception: {args.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, args) => System.Diagnostics.Trace.WriteLine($"Unhandled exception: {args.ExceptionObject}");
        pet = new PetWindow();
        MainWindow = pet;
        var menu = new Forms.ContextMenuStrip();
        var pause = new Forms.ToolStripMenuItem("Pause") { CheckOnClick = true };
        pause.CheckedChanged += (_, _) => pet.SetPaused(pause.Checked);
        menu.Items.Add(pause);
        var aiStatus = new Forms.ToolStripMenuItem { Enabled = false };
        var enableAi = new Forms.ToolStripMenuItem("Enable Python AI") { CheckOnClick = true, Checked = pet.AiEnabled };
        enableAi.CheckedChanged += (_, _) => pet.SetAiEnabled(enableAi.Checked);
        menu.Opening += (_, _) => aiStatus.Text = pet.AiStatus;
        menu.Items.Add(enableAi);
        menu.Items.Add(aiStatus);
        menu.Items.Add("Reset to primary screen", null, (_, _) => pet.Reset());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        trayIcon = AppIcon.TrayImage();
        tray = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = "Desktop Pet — right-click for controls",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => pet.Reset();
        pet.Show();
        controlsWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(showControls,
            (_, _) => Dispatcher.BeginInvoke(new Action(() => { if (pet.IsVisible) pet.ShowControls(); })),
            null, System.Threading.Timeout.Infinite, false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        pet?.StopAi();
        if (tray != null)
        {
            tray.Visible = false;
            tray.ContextMenuStrip?.Dispose();
            tray.Dispose();
        }
        trayIcon?.Dispose();
        controlsWait?.Unregister(null);
        showControls?.Dispose();
        if (ownsInstance) instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
