using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PKHaX.Hacks.Overlay;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class PersonalOverrideTests
{
    private static byte[] Gen4WithBulbasaurAttack(byte attack)
    {
        var stock = PersonalTable.Pt;
        var data = new byte[stock.Count * PersonalInfo4.SIZE];
        for (int i = 0; i < stock.Count; i++)
            stock[i].Write().CopyTo(data.AsSpan(i * PersonalInfo4.SIZE));
        data[(1 * PersonalInfo4.SIZE) + 1] = attack;
        return data;
    }

    [Fact]
    public void Gen4OverrideReachesEveryGen4TableAndPk4()
    {
        var stockAttack = PersonalTable.HGSS[1].ATK;
        Assert.False(PersonalOverride.IsActive);
        using (PersonalOverlay.Use(GameVersion.Pt, Gen4WithBulbasaurAttack(201)))
        {
            Assert.True(PersonalOverride.IsActive);
            Assert.Equal(201, PersonalTable.Pt[1].ATK);
            Assert.Equal(201, PersonalTable.DP[1].ATK);
            Assert.Equal(201, PersonalTable.HGSS[1].ATK);
            var pk = new PK4 { Species = 1 };
            Assert.Equal(201, pk.PersonalInfo.ATK);
            Assert.Equal(stockAttack, PersonalTable.E[1].ATK);
        }
        Assert.False(PersonalOverride.IsActive);
        Assert.Equal(stockAttack, PersonalTable.HGSS[1].ATK);
        Assert.Equal(stockAttack, new PK4 { Species = 1 }.PersonalInfo.ATK);
    }

    [Fact]
    public void SaveFilePersonalFollowsTheOverride()
    {
        var sav = new SAV4Pt();
        using (PersonalOverlay.Use(GameVersion.Pt, Gen4WithBulbasaurAttack(77)))
            Assert.Equal(77, sav.Personal[1].ATK);
        Assert.NotEqual(77, sav.Personal[1].ATK);
    }

    [Fact]
    public void NestedScopesRestoreInReverseOrder()
    {
        var gen4 = PersonalOverlay.Create(GameVersion.Pt, Gen4WithBulbasaurAttack(150));
        var outer = PersonalOverride.Use(gen4.Context, gen4.Table);
        var gen3 = new PersonalTable3(CopyGen3(PersonalTable.E));
        var inner = PersonalOverride.Use(EntityContext.Gen3, gen3);
        Assert.Same(gen3, PersonalTable.FR);
        Assert.Equal(150, PersonalTable.Pt[1].ATK);
        inner.Dispose();
        Assert.NotSame(gen3, PersonalTable.FR);
        Assert.Equal(150, PersonalTable.Pt[1].ATK);
        outer.Dispose();
        Assert.False(PersonalOverride.IsActive);
    }

    [Fact]
    public void OutOfOrderDisposeRemovesOnlyItsOwnScope()
    {
        var a = PersonalOverride.Use(EntityContext.Gen4, PersonalOverlay.Create(GameVersion.Pt, Gen4WithBulbasaurAttack(10)).Table);
        var b = PersonalOverride.Use(EntityContext.Gen4, PersonalOverlay.Create(GameVersion.Pt, Gen4WithBulbasaurAttack(20)).Table);
        Assert.Equal(2, PersonalOverride.ScopeCount);
        a.Dispose();
        Assert.Equal(1, PersonalOverride.ScopeCount);
        Assert.Equal(20, PersonalTable.Pt[1].ATK);
        a.Dispose();
        Assert.Equal(20, PersonalTable.Pt[1].ATK);
        b.Dispose();
        Assert.False(PersonalOverride.IsActive);
        Assert.Equal(PersonalTable.StockPt[1].ATK, PersonalTable.Pt[1].ATK);
    }

    [Fact]
    public void TwoHoldersClosingInOpeningOrderLeaveNothingBehind()
    {
        var stockHp = PersonalTable.StockE[1].HP;
        var gen3 = CopyGen3(PersonalTable.StockE);
        gen3[(1 * PersonalInfo3.SIZE) + 0] = 222;
        var editor = PersonalOverlay.Use(GameVersion.E, gen3);
        var second = PersonalOverlay.Use(GameVersion.Pt, Gen4WithBulbasaurAttack(233));
        Assert.Equal(222, PersonalTable.E[1].HP);
        Assert.Equal(233, PersonalTable.Pt[1].ATK);
        editor.Dispose();
        Assert.Equal(stockHp, PersonalTable.E[1].HP);
        Assert.Equal(stockHp, new PK3 { Species = 1 }.PersonalInfo.HP);
        Assert.Equal(233, PersonalTable.Pt[1].ATK);
        second.Dispose();
        Assert.False(PersonalOverride.IsActive);
        Assert.Equal(stockHp, new PK3 { Species = 1 }.PersonalInfo.HP);
        Assert.Equal(PersonalTable.StockPt[1].ATK, PersonalTable.Pt[1].ATK);
    }

    [Fact]
    public void AFullSetHidesTheScopesBelowItUntilItCloses()
    {
        var below = PersonalOverride.Use(EntityContext.Gen4, PersonalOverlay.Create(GameVersion.Pt, Gen4WithBulbasaurAttack(40)).Table);
        var gen3 = new PersonalTable3(CopyGen3(PersonalTable.StockE));
        var full = PersonalOverride.Use(PersonalOverrideSet.Empty.With(EntityContext.Gen3, gen3));
        Assert.Same(gen3, PersonalTable.E);
        Assert.Same(PersonalTable.StockPt, PersonalTable.Pt);
        below.Dispose();
        Assert.Same(gen3, PersonalTable.E);
        full.Dispose();
        Assert.False(PersonalOverride.IsActive);
        Assert.Same(PersonalTable.StockE, PersonalTable.E);
    }

    [Fact]
    public void ConcurrentHoldersAlwaysEndStock()
    {
        var tables = new[] { 11, 22, 33, 44 }
            .Select(atk => PersonalOverlay.Create(GameVersion.Pt, Gen4WithBulbasaurAttack((byte)atk)).Table)
            .ToArray();
        Parallel.For(0, 8, worker =>
        {
            var random = new Random(worker);
            var open = new List<IDisposable>();
            for (int i = 0; i < 2000; i++)
            {
                if (open.Count == 0 || random.Next(3) != 0)
                    open.Add(PersonalOverride.Use(EntityContext.Gen4, tables[random.Next(tables.Length)]));
                else
                {
                    var index = random.Next(open.Count);
                    open[index].Dispose();
                    open.RemoveAt(index);
                }
            }
            foreach (var scope in open)
                scope.Dispose();
        });
        Assert.False(PersonalOverride.IsActive);
        Assert.Equal(0, PersonalOverride.ScopeCount);
        Assert.Same(PersonalTable.StockPt, PersonalTable.Pt);
    }

    [Fact]
    public void WrongTableTypeForContextIsRefused()
    {
        var gen4 = PersonalOverlay.Create(GameVersion.Pt, Gen4WithBulbasaurAttack(1)).Table;
        Assert.Throws<ArgumentException>(() => PersonalOverride.Use(EntityContext.Gen3, gen4));
        Assert.False(PersonalOverride.IsActive);
    }

    [Fact]
    public void ShortTableIsRefused()
    {
        Assert.Throws<ArgumentException>(() => PersonalOverlay.Create(GameVersion.Pt, new byte[PersonalInfo4.SIZE * 10]));
        Assert.Throws<ArgumentException>(() => PersonalOverlay.Create(GameVersion.Pt, new byte[(PersonalInfo4.SIZE * PersonalTable.Pt.Count) + 1]));
    }

    [Fact]
    public void StockTablesKeepTutorDataWhenFirstTouchedUnderAnOverride()
    {
        Assert.Equal(4, PersonalTable.E[(int)Species.Deoxys].FormCount);
        Assert.Equal(4, PersonalTable.FR[(int)Species.Deoxys].FormCount);
    }

    private static byte[] CopyGen3(PersonalTable3 table)
    {
        var data = new byte[table.Count * PersonalInfo3.SIZE];
        for (int i = 0; i < table.Count; i++)
            table[i].Write().CopyTo(data.AsSpan(i * PersonalInfo3.SIZE));
        return data;
    }
}
