using System;

namespace PKHaX.Hacks;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PkhaxModuleAttribute(string id) : Attribute
{
    public string Id { get; } = id;
}
