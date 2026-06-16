using Rollgeon.Attributes;

namespace Rollgeon.PreConditions.Tests
{
    /// <summary>Stat <c>int</c> de fixture — no usar en runtime.</summary>
    public sealed class TestEnergy : BaseAttribute<int>
    {
        public TestEnergy() { }
        public TestEnergy(int initial) : base(initial) { }
        protected override BaseAttribute<int> CreateDuplicate() => new TestEnergy(_rawValue);
    }

    /// <summary>Stat <c>int</c> alternativo — para tests que requieren dos stats distintos.</summary>
    public sealed class TestHealth : BaseAttribute<int>
    {
        public TestHealth() { }
        public TestHealth(int initial) : base(initial) { }
        protected override BaseAttribute<int> CreateDuplicate() => new TestHealth(_rawValue);
    }
}
