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
    private PetUpdates? updates;
    private bool firstRun, restarted, newUser;
    // Where clicking the tray's last balloon goes (null: nowhere).
    private string? balloonLink;

    // Velopack first: the installer's hooks run and exit here, and an update downloaded earlier is
    // applied before anything starts. A copied (not installed) folder goes on as before.
    // First run after installing: the welcome (PetWelcome). Restarted by an update: "what's new".
    // Uninstalling: drop this copy's Windows startup entry so Windows does not try to start a deleted file.
    public App()
    {
        try
        {
            Velopack.VelopackApp.Build()
                .OnFirstRun(_ => firstRun = true)
                .OnRestarted(_ => restarted = true)
                .OnBeforeUninstallFastCallback(_ => PetUpdates.OnUninstall())
                .Run();
        }
        catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"Velopack startup skipped: {ex.Message}"); }
        newUser = firstRun && !System.IO.File.Exists(System.IO.Path.Combine(PetPreferences.DataDirectory, "preferences.json"));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string key = "Local\\DesktopPet-" + Environment.UserName + "-" + System.Diagnostics.Process.GetCurrentProcess().SessionId;
        instanceMutex = new System.Threading.Mutex(false, key);
        try { ownsInstance = instanceMutex.WaitOne(0); }
        catch (System.Threading.AbandonedMutexException) { ownsInstance = true; }
        // Just installed while another copy runs: close that one so the installed pet (and its welcome) opens.
        if (!ownsInstance && firstRun && PetHandover.CloseOtherCopies(TimeSpan.FromSeconds(10)))
        {
            try { ownsInstance = instanceMutex.WaitOne(TimeSpan.FromSeconds(5)); }
            catch (System.Threading.AbandonedMutexException) { ownsInstance = true; }
        }
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
        var customize = new Forms.ToolStripMenuItem(null, null, (_, _) => pet.ShowCustomize());
        menu.Items.Add(customize);
        var settings = new Forms.ToolStripMenuItem(null, null, (_, _) => pet.ShowPetSettings());
        menu.Items.Add(settings);
        var reset = new Forms.ToolStripMenuItem(null, null, (_, _) => pet.Reset());
        menu.Items.Add(reset);
        // Updates (installed app only): "Update to x" once one is downloaded, else "Check for updates".
        updates = new PetUpdates();
        updates.ForgetUninstallChoice();
        pet.Updates = updates;
        var checkUpdates = new Forms.ToolStripMenuItem(null, null, (_, _) => _ = updates.Check(true)) { Visible = updates.Available };
        var applyUpdate = new Forms.ToolStripMenuItem(null, null, (_, _) => { if (updates.ApplyAndRestart()) Shutdown(); }) { Visible = false };
        menu.Items.Add(checkUpdates);
        menu.Items.Add(applyUpdate);
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
            customize.Text = pet.L("Tùy chỉnh…", "Customize…");
            settings.Text = pet.L("Cài đặt…", "Settings…");
            reset.Text = pet.L("Gọi mèo về màn hình chính", "Bring the pet to the main screen");
            exit.Text = pet.L("Thoát", "Exit");
            checkUpdates.Text = pet.L("Kiểm tra cập nhật", "Check for updates");
            applyUpdate.Text = pet.LF($"Cập nhật lên bản {updates.ReadyVersion} (mở lại mèo)", $"Update to {updates.ReadyVersion} (restarts the pet)");
            checkUpdates.Visible = updates.Available && updates.ReadyVersion == null;
            applyUpdate.Visible = updates.ReadyVersion != null;
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
        tray.BalloonTipClicked += (_, _) =>
        {
            if (balloonLink == null) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(balloonLink) { UseShellExecute = true }); }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine($"Could not open {balloonLink}: {ex.Message}"); }
        };
        pet.LanguageChanged += Localize;
        updates.Changed += version =>
        {
            Localize();
            if (tray == null) return;
            balloonLink = null;
            tray.ShowBalloonTip(8000, "Desktop Pet", version != null
                ? pet.LF($"Bản {version} đã tải xong. Chuột phải biểu tượng khay → Cập nhật, hoặc để lần mở mèo sau tự cập nhật.",
                        $"Version {version} is downloaded. Right-click the tray icon → Update, or it updates the next time the pet starts.")
                : pet.L("Bạn đang dùng bản mới nhất.", "You have the latest version."), Forms.ToolTipIcon.Info);
        };
        pet.Show();
        if (firstRun) Dispatcher.BeginInvoke(new Action(() => PetWelcome.Show(pet, newUser, new StartupRegistration())));
        // What's new: the installed version changed since the last start (or Velopack restarted it after updating).
        if (updates.CurrentVersion is string current)
        {
            string? previous = pet.RecordVersion(current);
            if (!firstRun && (restarted || (previous != null && previous != current)))
            {
                balloonLink = PetUpdates.ReleaseUrl(current);
                tray.ShowBalloonTip(8000, "Desktop Pet", pet.LF($"Đã cập nhật lên bản {current}. Bấm vào đây để xem có gì mới.",
                    $"Updated to version {current}. Click here to see what's new."), Forms.ToolTipIcon.Info);
            }
        }
        updates.Start(pet.AutoUpdate);
        controlsWait = System.Threading.ThreadPool.RegisterWaitForSingleObject(showControls,
            (_, _) => Dispatcher.BeginInvoke(new Action(() => { if (pet.IsVisible) pet.ShowControls(); })),
            null, System.Threading.Timeout.Infinite, false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        pet?.StopAi();
        updates?.Stop();
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
        updates?.RunPendingUninstall(); // Last: everything above is closed.
        base.OnExit(e);
    }
}
