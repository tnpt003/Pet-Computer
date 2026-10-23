using System.Text;
using System.Text.RegularExpressions;
using DesktopPet;

// UI languages and the translation tables keyed by English text (PetText.cs, Assets/i18n).
static class TextTests
{
    public static void Run(Action<bool, string> check)
    {
        check(PetLanguage.Normalize("ja") == "ja" && PetLanguage.Normalize("fr") == "en" && PetLanguage.Normalize(null) == "en",
            "Known languages are kept; anything else becomes English");
        check(PetLanguage.UsesEnglish("ja") && PetLanguage.UsesEnglish("en") && !PetLanguage.UsesEnglish("vi"),
            "Every language but Vietnamese starts from the English names and layouts");
        check(PetLanguage.Offered.SequenceEqual(new[] { "en", "vi", "ja" }) && PetLanguage.Choices("ja").SequenceEqual(PetLanguage.Offered),
            "English, Vietnamese and Japanese are offered");

        check(PetText.Pick("vi", "Làm ngay", "Do now") == "Làm ngay" && PetText.Pick("en", "Làm ngay", "Do now") == "Do now",
            "Vietnamese and English come straight from the code");
        check(PetText.Pick("ja", "Làm ngay", "Do now") == "今すぐ", "Japanese is looked up by the English text");
        PetText.ClearMissing();
        check(PetText.Pick("ja", "Chưa dịch", "Not translated (test)") == "Not translated (test)"
              && PetText.Missing.Contains("ja: Not translated (test)"), "Missing Japanese shows English and is recorded");
        int wave = 3; string heal = " +5";
        check(PetText.PickFormat("ja", $"Xong đợt {wave}!{heal}", $"Wave {wave} cleared!{heal}") == "第3ウェーブ クリア！ +5",
            "Interpolated text is looked up by its format and filled with the same values");
        check(PetText.PickFormat("vi", $"Xong đợt {wave}!", $"Wave {wave} cleared!") == "Xong đợt 3!"
              && PetText.PickFormat("ja", $"Còn {wave}", $"{wave} left (test)") == "3 left (test)",
            "Vietnamese formats as written; untranslated formats fall back to English");

        check(PetText.Holes("{0:F0} of {1}, {0}, {{literal}}").SequenceEqual(new[] { 0, 1 }), "Format holes are read with their numbers");
        bool rejected = false;
        try { PetText.Parse(new MemoryStream(Encoding.UTF8.GetBytes("{\"A\": 1}"))); } catch (InvalidDataException) { rejected = true; }
        check(rejected, "A table entry that is not text is rejected");

        // Data text (battle.json, coats.json, accessories.json) uses the same tables.
        var calico = PetCoats.All.First(c => c.Id == "Calico");
        check(PetText.Data("ja", calico.NameVi, calico.NameEn) == "三毛" && PetText.Data("vi", calico.NameVi, calico.NameEn) == "Tam thể"
              && PetText.Data("ja", "", "") == "", "Data names are looked up by their English text; empty text stays empty");
        var settings = BattleData.Settings;
        string cheer = BattleSkills.Get(BattleSkill.Cheer).Describe(settings, "ja");
        check(cheer.StartsWith("応援 — ", StringComparison.Ordinal) && !cheer.Contains('{') && cheer.Contains(settings.CheerCooldown.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)),
            "A Japanese skill description is filled with the battle's numbers");
        check(BattleSkills.Get(BattleSkill.Cheer).Name("en") == "Cheer" && BattleSkills.Get(BattleSkill.Cheer).Name("vi") == "Cổ vũ",
            "Vietnamese and English data names are unchanged");

        // The shipped tables: holes match their keys, and every key still exists in the code or data
        // (tests/ai/test_i18n.py checks the other way: every code and data text has a translation).
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        string code = string.Concat(Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Concat(new[] { "battle.json", "coats.json", "accessories.json" }.Select(name => Path.Combine(root, "src", "DesktopPet", "Assets", name)))
            .Select(File.ReadAllText));
        foreach (string language in PetLanguage.All.Where(l => l is not ("vi" or "en")))
        {
            var table = PetText.Table(language);
            check(table.Count > 0, $"The {language} table is embedded and loads");
            // A translation may leave a hole out (an English plural "s"), never add one.
            var badHoles = table.Where(e => PetText.Holes(e.Value).Except(PetText.Holes(e.Key)).Any()).Select(e => e.Key).ToArray();
            check(badHoles.Length == 0, $"Every {language} translation uses only its key's {{n}} holes: {string.Join(" | ", badHoles)}");
            var stale = table.Keys.Where(key => !InCode(code, key)).ToArray();
            check(stale.Length == 0, $"Every {language} key is still English text in the code: {string.Join(" | ", stale)}");
        }
    }

    // A key is in the code if each literal part of it (between format holes and line breaks, where
    // long text is joined with +) is, as C# would write it.
    private static bool InCode(string code, string key) =>
        Regex.Split(key, @"\{\d+[^{}]*\}|\n").Where(part => part.Length > 0)
            .All(part => code.Contains(part.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n")));
}
