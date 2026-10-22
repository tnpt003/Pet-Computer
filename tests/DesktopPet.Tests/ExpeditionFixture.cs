using DesktopPet;

// The built-in battle data with the Expedition's map fixed as it was before the step gacha
// (2026-10-22) and no capsule machine, for checks that need to know which node kinds each step has.
internal static class ExpeditionFixture
{
    public const string OldSteps = "[ [ \"fight\", \"fight\" ], [ \"fight\", \"elite\", \"event\" ], [ \"rest\", \"elite\" ], [ \"fight\", \"elite\", \"event\" ], [ \"rest\", \"fight\" ] ]";
    private static BattleSettings? fixedData;
    public static BattleSettings Fixed => fixedData ??= BattleData.ParseWithOverride(Embedded(), "{ \"schemaVersion\": 1, \"expedition\": { \"steps\": " + OldSteps + ", \"capsules\": null } }");

    public static string Embedded()
    {
        using var stream = typeof(BattleData).Assembly.GetManifestResourceStream("DesktopPet.battle.json")!;
        return new StreamReader(stream).ReadToEnd();
    }
}
