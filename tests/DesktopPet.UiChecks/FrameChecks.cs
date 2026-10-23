using System.Diagnostics;
using System.Reflection;
using DesktopPet;
using static PetPrivate;

// The debug view's frame statistics report real gaps between frames, while the simulation
// keeps its 0.1 s cap (no catch-up after a stall). Time is faked by moving the last frame back.
internal static class FrameChecks
{

    public static void Run(PetWindow window, PetModel pet, Action<bool, string> check)
    {
        var clock = Field<Stopwatch>(window, "clock");
        void FrameAfter(double gap)
        {
            SetField(window, "previous", clock.Elapsed.TotalSeconds - gap);
            MethodOf("Tick").Invoke(window, new object?[] { null, EventArgs.Empty });
        }
        window.Reset();
        SetField(window, "frameStatsStart", clock.Elapsed.TotalSeconds);
        SetField(window, "frameMaxGap", 0.0);
        SetField(window, "measuredMaxGap", 0.0);
        double animation = pet.AnimationTime;
        FrameAfter(0.3);
        double gap = Field<double>(window, "frameMaxGap");
        check(gap >= 0.299 && gap < 0.5, $"A 300 ms stall is recorded as ~300 ms, not the simulation's 100 ms cap ({gap * 1000:F0} ms)");
        double advanced = pet.AnimationTime - animation;
        check(advanced > 0 && advanced <= 0.1 + 1e-9, $"The simulation still advances at most 0.1 s for it (no catch-up; {advanced:F3} s)");
        SetField(window, "frameStatsStart", clock.Elapsed.TotalSeconds - 1.5); // Close the 1 s window.
        FrameAfter(1.0 / 60);
        double reported = Field<double>(window, "measuredMaxGap");
        check(reported >= 0.299 && Field<double>(window, "frameMaxGap") == 0, $"The closed window reports that longest gap and the next one starts fresh ({reported * 1000:F0} ms)");
        window.Reset();
    }
}
