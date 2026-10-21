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
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) => System.Diagnostics.Trace.WriteLine($"Unobserved task exception: {args.Exception}");
        // With "exited" below, a "started" line without one marks a crash or a killed process.
        SessionEnding += (_, args) => System.Diagnostics.Trace.WriteLine($"Windows session ending: {args.ReasonSessionEnding}");
        pet = new PetWindow();
        MainWindow = pet;
        var menu = new Forms.ContextMenuStrip();
        var pause = new Forms.ToolStripMenuItem { CheckOnClick = true };
        pause.CheckedChanged += (_, _) => pet.SetPaused(pause.Checked);
        menu.Items.Add(pause);
        var aiStatus = new Forms.ToolStripMenuItem { Enabled = false };
        var enableAi = new Forms.ToolStripMenuItem { CheckOnClick = true, Checked = pet.AiEnabled };
        enableAi.CheckedChanged += (_, _) => pet.SetAiEnabled(enableAi.Checked);
        menu.Items.Add(enableAi);
        menu.Items.Add(aiStatus);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var settings = new Forms.ToolStripMenuItem(null, null, (_, _) => pet.ShowPetSettings());
        menu.Items.Add(settings);
        var reset = new Forms.ToolStripMenuItem(null, null, (_, _) => pet.Reset());
        menu.Items.Add(reset);
        menu.Items.Add(new Forms.ToolStripSeparator());
        var exit = new Forms.ToolStripMenuItem(null, null, (_, _) => Shutdown());
        menu.Items.Add(exit);
        PetTrayMenuStyle.Apply(menu);
        // Tray labels follow the pet's single UI language.
        void Localize()
        {
            pause.Text = pet.L("Tạm dừng", "Pause");
            enableAi.Text = pet.L("Mèo thông minh (AI)", "Smart pet (AI)");
            aiStatus.Text = pet.AiStatusText;
            settings.Text = pet.L("Cài đặt…", "Settings…");
            reset.Text = pet.L("Gọi mèo về màn hình chính", "Bring the pet to the main screen");
            exit.Text = pet.L("Thoát", "Exit");
            if (tray != null) tray.Text = pet.L("Desktop Pet — chuột phải để điều khiển", "Desktop Pet — right-click for controls");
        }
        Localize();
        menu.Opening += (_, _) => Localize();
        trayIcon = AppIcon.TrayImage();
        tray = new Forms.NotifyIcon
        {
            Icon = trayIcon,
            Text = pet.L("Desktop Pet — chuột phải để điều khiển", "Desktop Pet — right-click for controls"),
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => pet.Reset();
        pet.LanguageChanged += Localize;
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
        if (ownsInstance) System.Diagnostics.Trace.WriteLine($"Desktop Pet exited (code {e.ApplicationExitCode})");
        base.OnExit(e);
    }
}
