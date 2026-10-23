using System.Reflection;
using DesktopPet;

// The one place the UI checks reach PetWindow's private members by name.
// A renamed member fails with its name instead of a NullReferenceException.
internal static class PetPrivate
{
    public const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

    public static FieldInfo FieldOf(string name) =>
        typeof(PetWindow).GetField(name, Flags)
        ?? throw new MissingFieldException($"PetWindow has no private field '{name}' (renamed?)");

    public static MethodInfo MethodOf(string name, object?[]? args = null)
    {
        var methods = typeof(PetWindow).GetMethods(Flags).Where(m => m.Name == name).ToArray();
        if (methods.Length == 1)
            return methods[0];
        if (methods.Length == 0)
            throw new MissingMethodException($"PetWindow has no private method '{name}' (renamed?)");
        // Overloads: pick by the runtime types of the arguments.
        var types = (args ?? Array.Empty<object?>()).Select(a => a?.GetType() ?? typeof(object)).ToArray();
        return typeof(PetWindow).GetMethod(name, Flags, null, types, null)
            ?? throw new MissingMethodException($"PetWindow has no private overload {name}({string.Join(", ", types.Select(t => t.Name))})");
    }

    public static T Field<T>(PetWindow window, string name) => (T)FieldOf(name).GetValue(window)!;
    public static void SetField(PetWindow window, string name, object? value) => FieldOf(name).SetValue(window, value);
    public static object? Call(PetWindow window, string name, params object?[]? args) => MethodOf(name, args).Invoke(window, args);
}
