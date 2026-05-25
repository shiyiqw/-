using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

var asmPath = @"D:\steam\steamapps\common\哀鸿：城破十日记\BepInEx\interop\Assembly-CSharp.dll";
var asmDir = Path.GetDirectoryName(asmPath)!;
var coreDir = @"D:\steam\steamapps\common\哀鸿：城破十日记\BepInEx\core";

AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string[] candidates =
    {
        Path.Combine(asmDir, name.Name + ".dll"),
        Path.Combine(coreDir, name.Name + ".dll")
    };

    foreach (var candidate in candidates)
    {
        if (File.Exists(candidate))
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
    }

    return null;
};

var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(asmPath);

var targetNames = new[]
{
    "Utage.SoundManager",
    "Utage.SoundData",
    "Utage.AssetFileBase",
    "Utage.AssetFileUtage"
};

BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

foreach (var name in targetNames)
{
    var t = asm.GetType(name);
    Console.WriteLine($"=== TYPE {name} ===");
    if (t == null)
    {
        Console.WriteLine("TYPE_NOT_FOUND");
        Console.WriteLine();
        continue;
    }

    Console.WriteLine($"BaseType: {t.BaseType?.FullName}");

    Console.WriteLine("-- Fields --");
    foreach (var f in t.GetFields(flags).OrderBy(x => x.Name))
        Console.WriteLine($"{f.Attributes} | {f.FieldType.FullName} {f.Name}");

    Console.WriteLine("-- Properties --");
    foreach (var p in t.GetProperties(flags).OrderBy(x => x.Name))
    {
        var get = p.GetMethod != null ? "get" : "";
        var set = p.SetMethod != null ? "/set" : "";
        Console.WriteLine($"{p.PropertyType.FullName} {p.Name} [{get}{set}]");
    }

    Console.WriteLine("-- Constructors --");
    foreach (var c in t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).OrderBy(x => x.ToString()))
        Console.WriteLine(c.ToString());

    Console.WriteLine("-- Methods --");
    foreach (var m in t.GetMethods(flags).OrderBy(x => x.Name))
        Console.WriteLine(m.ToString());

    Console.WriteLine();
}
