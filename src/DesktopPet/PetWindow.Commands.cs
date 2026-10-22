using System;
using System.Windows.Controls;

namespace DesktopPet;

// "Do now" menu: the user asks for an action or (in manual control) an emotion right away.
// Cooldowns, bond, chance and Quiet are skipped; sleep lock, waking, drag and flight are not.
public sealed partial class PetWindow
{
    // A request made while the pet cannot act yet (waking after the right-click, a real
    // reaction) waits this long, then is dropped.
    public const double CommandQueueSeconds = 3;
    private PetAction? queuedCommand;
    private double queuedCommandUntil;

    private static readonly (PetAction Action, string Vi, string En)[] CommandItems =
    {
        (PetAction.Sitting, "Ngồi", "Sit"), (PetAction.Looking, "Nhìn quanh", "Look around"),
        (PetAction.PreparingJump, "Nhảy", "Jump"), (PetAction.Sleeping, "Ngủ", "Sleep"),
        (PetAction.SeekingAttention, "Vẫy chân", "Wave a paw"),
        (PetAction.Grooming, "Liếm lông", "Groom"), (PetAction.Stretching, "Duỗi người", "Stretch"),
        (PetAction.Kneading, "Nhồi bột", "Knead"), (PetAction.RollingOver, "Lăn ngửa", "Roll over"),
        (PetAction.ChasingTail, "Đuổi đuôi", "Chase tail"), (PetAction.Scratching, "Cào móng", "Scratch"),
        (PetAction.Sneezing, "Hắt xì", "Sneeze"),
    };

    private static readonly (PetEmotion Emotion, string Vi, string En)[] EmotionItems =
    {
        (PetEmotion.Neutral, "Bình thường", "Normal"), (PetEmotion.Happy, "Vui", "Happy"),
        (PetEmotion.Angry, "Giận", "Angry"), (PetEmotion.Sad, "Buồn", "Sad"), (PetEmotion.Surprised, "Giật mình", "Surprised"),
    };

    private void InitializeCommands(ContextMenu menu)
    {
        var root = PetUi.MenuItem(PetUi.Play);
        var actions = new MenuItem[CommandItems.Length];
        var cushionSleep = new MenuItem();
        cushionSleep.Click += (_, _) => SleepOnCushionNow(); // PetWindow.TrophyPlay.cs
        for (int i = 0; i < CommandItems.Length; i++)
        {
            var action = CommandItems[i].Action;
            actions[i] = new MenuItem();
            actions[i].Click += (_, _) => DoNow(action);
            root.Items.Add(actions[i]);
            // Groups: basic poses, self-care, gestures.
            if (action is PetAction.SeekingAttention or PetAction.Stretching) root.Items.Add(new Separator());
            // Right under "Sleep": sleep on the slime cushion, when it is on display and close by.
            if (action == PetAction.Sleeping) root.Items.Add(cushionSleep);
        }
        // Emotions have their own submenu: the manual-control switch, then the emotions it unlocks.
        var feelings = PetUi.MenuItem(PetUi.Emotions);
        var manual = new MenuItem { IsCheckable = true };
        manual.Click += (_, _) => SetManualEmotions(manual.IsChecked);
        feelings.Items.Add(manual);
        feelings.Items.Add(new Separator());
        var emotions = new MenuItem[EmotionItems.Length];
        for (int i = 0; i < EmotionItems.Length; i++)
        {
            var emotion = EmotionItems[i].Emotion;
            emotions[i] = new MenuItem();
            emotions[i].Click += (_, _) => PerformEmotion(emotion);
            feelings.Items.Add(emotions[i]);
        }
        menu.Items.Add(root);
        menu.Items.Add(feelings);
        menu.Opened += (_, _) =>
        {
            bool en = preferences.Language == "en";
            root.Header = en ? "Do now" : "Làm ngay";
            root.IsEnabled = !paused && !BattleOwnsPet;
            feelings.Header = en ? "Emotions" : "Cảm xúc";
            feelings.IsEnabled = !paused && !BattleOwnsPet;
            for (int i = 0; i < actions.Length; i++) actions[i].Header = en ? CommandItems[i].En : CommandItems[i].Vi;
            // Shown only with the cushion on display; greyed out (with the reason) when the cat is far from it.
            bool cushion = TrophiesVisible && DisplayedKeepsake?.Kind == KeepsakeSpec.Cushion;
            cushionSleep.Visibility = cushion ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            cushionSleep.Header = en ? "Sleep on the cushion" : "Ngủ trên đệm";
            cushionSleep.IsEnabled = CushionNear;
            cushionSleep.ToolTip = CushionNear ? null : en ? "The cat is too far from the cushion" : "Mèo ở xa đệm quá";
            ToolTipService.SetShowOnDisabled(cushionSleep, true);
            manual.Header = en ? "Control emotions myself" : "Tự điều khiển cảm xúc";
            manual.IsChecked = pet.Emotions.ManualMode;
            for (int i = 0; i < emotions.Length; i++)
            {
                emotions[i].Header = en ? EmotionItems[i].En : EmotionItems[i].Vi;
                emotions[i].IsEnabled = pet.Emotions.ManualMode;
                emotions[i].IsChecked = pet.Emotions.ManualMode && pet.Emotions.ManualEmotion == EmotionItems[i].Emotion;
            }
        };
    }

    public bool DoNow(PetAction action)
    {
        if (paused || closed || dragging || BattleOwnsPet || Array.IndexOf(PetBehavior.ManualActions, action) < 0) return false;
        revision++; // Drop AI replies that were chosen before this request; not an interaction for needs or learning.
        if (TryStartCommand(action)) { queuedCommand = null; return true; }
        queuedCommand = action; // Newest request wins.
        queuedCommandUntil = clock.Elapsed.TotalSeconds + CommandQueueSeconds;
        return false;
    }

    // Every unpaused frame: start a queued request once the pet is free (awake, landed, calm).
    private void TickCommands()
    {
        if (queuedCommand is not PetAction action) return;
        if (clock.Elapsed.TotalSeconds > queuedCommandUntil || dragging || BattleOwnsPet) { queuedCommand = null; return; }
        if (pressPending) return;
        if (TryStartCommand(action)) { queuedCommand = null; revision++; }
    }

    // Cancels ball/treat/rest-spot only when the request really starts: cancelling resets
    // the behavior clock, which would stretch an ongoing waking.
    private bool TryStartCommand(PetAction action)
    {
        if (!PetBehavior.CanStartManual(pet, action)) return false;
        CancelFeatureActivity();
        return behavior.StartManual(pet, action);
    }

    public void SetManualEmotions(bool on)
    {
        pet.Emotions.SetManualMode(on);
        revision++;
    }

    public bool PerformEmotion(PetEmotion emotion)
    {
        if (paused || !pet.Emotions.Perform(emotion)) return false;
        revision++;
        return true;
    }
}
