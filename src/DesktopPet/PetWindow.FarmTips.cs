using System.Windows;
using System.Windows.Controls;

namespace DesktopPet;

// First-time hints on the land (docs/FARM-GAME.md): a label over the first tile that follows the
// first crop's steps, then a short one over the hearth and the pond. Words only: they take no
// clicks and no focus, and never touch the cat.
public sealed partial class PetWindow
{
    private const double FarmTipRise = 54, FarmTipSeconds = 3, FarmTipLastSeconds = 5;

    private Border? farmTip;
    private TextBlock? farmTipText;
    private string? farmTipShown;

    private bool FarmTipsAllowed => Blocking(AutoBlock.Paused | AutoBlock.AnyQuiet | AutoBlock.Away | AutoBlock.AnyBattle) == AutoBlock.None;

    // Every frame the field is shown.
    private void UpdateFarmTips()
    {
        if (farm == null) return;
        if (farm.TipPending(PetFarm.TipField) && farmFieldFold.Showing) UpdateFarmFieldTip();
        else if (farm.TipPending(PetFarm.TipField)) ShowFarmTipWords(null);
        else if (FarmTipsAllowed) ShowFarmPatchTips();
    }

    private void UpdateFarmFieldTip()
    {
        string? words = farm!.Next(0) switch
        {
            FarmStep.Till => L("Bấm ô này để cày đất", "Click this tile to till it"),
            FarmStep.Plant => L("Bấm lần nữa để chọn hạt", "Click again to pick a seed"),
            FarmStep.Water => L("Bấm để tưới nước", "Click to water it"),
            _ => null
        };
        if (words == null)
        {
            farm.MarkTip(PetFarm.TipField);
            SaveFarm();
            ShowFarmTipWords(null);
            ShowFarmLabel(0, L("Mèo sẽ phụ bạn chăm ruộng!", "The cat will help you tend it!"), FarmTipLastSeconds);
            return;
        }
        ShowFarmTipWords(FarmTipsAllowed && farmLift.Phase == FarmLiftPhase.Down ? words : null);
    }

    private void ShowFarmTipWords(string? words)
    {
        if (words == farmTipShown || farmTip == null || farmTipText == null || farmTip.Parent is not Grid row) return;
        farmTipShown = words;
        farmTip.Opacity = words == null ? 0 : 1;
        if (words == null) return;
        farmTipText.Text = words;
        PlaceFarmLabel(row, 0, FarmTipRise);
    }

    private void ShowFarmPatchTips()
    {
        foreach (var patch in farmPatches)
        {
            string tip = patch.Kind == FarmPatchKind.Hearth ? PetFarm.TipHearth : patch.Kind == FarmPatchKind.Dock ? PetFarm.TipDock : PetFarm.TipPond;
            if (!farm!.TipPending(tip) || !patch.OnGround || patch.Window?.IsVisible != true || !patch.Fold.Showing) continue;
            farm.MarkTip(tip);
            SaveFarm();
            ShowFarmPatchLabel(patch, patch.Kind == FarmPatchKind.Hearth ? L("Chuột phải để nấu ăn", "Right-click to cook") : patch.Kind == FarmPatchKind.Dock ? L("Bấm ô để gập hoặc mở vùng đất", "Click a board to fold or open land") : L("Chuột phải để câu cá", "Right-click to fish"), FarmTipSeconds);
        }
    }

    // The cart's first visit: its own label already says to click it.
    private void ShowFarmCartTip()
    {
        if (farm == null || !farm.MarkTip(PetFarm.TipCart)) return;
        SaveFarm();
        ShowFarmLabel(PetFarm.TileCount / 2, L("Xe hàng đến mua nông sản!", "The stall buys your crops!"), FarmTipSeconds);
    }
}
