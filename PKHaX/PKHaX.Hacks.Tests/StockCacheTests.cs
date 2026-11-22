using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class StockCacheTests
{
    private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Fact]
    public void CachesFirstBuiltUnderAnOverrideStayStock()
    {
        var context = new AssemblyLoadContext("pkhax-fresh-core", isCollectible: true);
        try
        {
            var core = context.LoadFromAssemblyPath(typeof(PersonalTable).Assembly.Location);
            Assert.NotSame(typeof(PersonalTable).Assembly, core);
            Type T(string name) => core.GetType($"PKHeX.Core.{name}", throwOnError: true)!;

            var personalTable = T("PersonalTable");
            var personalOverride = T("PersonalOverride");
            var tableInterface = T("IPersonalTable");
            var evolutionTree = T("EvolutionTree");

            var bytes = CopyGen3(PersonalTable.StockE);
            bytes[(1 * PersonalInfo3.SIZE) + 0] = 201;
            var hack = Activator.CreateInstance(T("PersonalTable3"), new Memory<byte>(bytes))!;
            var use = personalOverride.GetMethod("Use", [T("EntityContext"), tableInterface])!;

            var holders = core.GetTypes()
                .Where(t => t != personalTable && !t.ContainsGenericParameters)
                .Where(t => t.GetFields(StaticFields).Any(f => tableInterface.IsAssignableFrom(f.FieldType) || f.FieldType == evolutionTree))
                .ToArray();
            Assert.Contains(holders, t => t.Name == "LearnSource3E");
            Assert.Contains(holders, t => t.Name == "EvolutionTree");

            var scope = (IDisposable)use.Invoke(null, [Enum.Parse(T("EntityContext"), "Gen3"), hack])!;
            Assert.Same(hack, personalTable.GetProperty("E")!.GetValue(null));
            Assert.Same(hack, personalTable.GetProperty("RS")!.GetValue(null));
            foreach (var holder in holders)
                RuntimeHelpers.RunClassConstructor(holder.TypeHandle);
            scope.Dispose();
            Assert.False((bool)personalOverride.GetProperty("IsActive")!.GetValue(null)!);

            var stock = personalTable.GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(f => f.Name.StartsWith("Stock", StringComparison.Ordinal))
                .Select(f => f.GetValue(null)!)
                .ToHashSet(ReferenceEqualityComparer.Instance);
            Assert.Equal(23, stock.Count);

            var leaks = new List<string>();
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var holder in holders)
            {
                foreach (var field in holder.GetFields(StaticFields))
                    Walk(field.GetValue(null), $"{holder.Name}.{field.Name}", 0);
            }
            Assert.Empty(leaks);

            var learn3E = T("LearnSource3E").GetField("Personal", StaticFields)!.GetValue(null)!;
            var bulbasaur = learn3E.GetType().GetProperty("Item", [typeof(int)])!.GetValue(learn3E, [1])!;
            var tmhm = (bool[])bulbasaur.GetType().GetProperty("TMHM")!.GetValue(bulbasaur)!;
            Assert.Contains(true, tmhm);
            Assert.Equal(PersonalTable.StockE[1].HP, (int)bulbasaur.GetType().GetProperty("HP")!.GetValue(bulbasaur)!);

            void Walk(object? value, string where, int depth)
            {
                if (value is null || value is string || value is Array || value.GetType().IsPrimitive || value.GetType().IsEnum || !seen.Add(value))
                    return;
                if (tableInterface.IsInstanceOfType(value))
                {
                    if (!stock.Contains(value))
                        leaks.Add(where);
                    return;
                }
                if (depth >= 3 || value.GetType().Assembly != core)
                    return;
                for (var type = value.GetType(); type is not null && type.Assembly == core; type = type.BaseType)
                {
                    foreach (var field in type.GetFields(InstanceFields))
                        Walk(field.GetValue(value), $"{where}.{field.Name}", depth + 1);
                }
            }
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void NoStaticFieldInPKHeXCoreCopiesAnOverriddenTable()
    {
        var core = Path.Combine(SourcePaths.PKHaXRoot, "PKHeX.Core");
        var declaration = new Regex(@"static\s+(?:readonly\s+)?[^;{}()=]*?\s\w+\s*(?:\{\s*get;\s*\}\s*)?=(?!>)[^;]*;", RegexOptions.Singleline);
        var overridden = new Regex(@"PersonalTable\.(?!Stock)[A-Z]\w*");
        var offenders = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.EndsWith($"PersonalInfo{Path.DirectorySeparatorChar}PersonalTable.cs", StringComparison.Ordinal))
            .SelectMany(path => declaration.Matches(File.ReadAllText(path))
                .Where(m => overridden.IsMatch(m.Value))
                .Select(m => $"{Path.GetRelativePath(core, path)}: {m.Value.Split('\n')[0].Trim()}"))
            .ToArray();
        Assert.Empty(offenders);
    }

    private static byte[] CopyGen3(PersonalTable3 table)
    {
        var data = new byte[table.Count * PersonalInfo3.SIZE];
        for (int i = 0; i < table.Count; i++)
            table[i].Write().CopyTo(data.AsSpan(i * PersonalInfo3.SIZE));
        return data;
    }
}
