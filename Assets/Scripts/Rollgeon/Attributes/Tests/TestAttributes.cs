namespace Rollgeon.Attributes.Tests
{
    /// <summary>
    /// Stat concreto de <c>int</c> — fixture para tests. No usar en runtime.
    /// Los stats reales (Health, Energy, Speed, ...) los define T100a.
    /// </summary>
    public sealed class TestIntAttribute : BaseAttribute<int>
    {
        public TestIntAttribute() { }
        public TestIntAttribute(int initial) : base(initial) { }

        protected override BaseAttribute<int> CreateDuplicate()
        {
            return new TestIntAttribute(_rawValue);
        }
    }

    /// <summary>Stat concreto de <c>float</c> — fixture para tests.</summary>
    public sealed class TestFloatAttribute : BaseAttribute<float>
    {
        public TestFloatAttribute() { }
        public TestFloatAttribute(float initial) : base(initial) { }

        protected override BaseAttribute<float> CreateDuplicate()
        {
            return new TestFloatAttribute(_rawValue);
        }
    }

    /// <summary>Stat concreto de <c>bool</c> — fixture para tests.</summary>
    public sealed class TestBoolAttribute : BaseAttribute<bool>
    {
        public TestBoolAttribute() { }
        public TestBoolAttribute(bool initial) : base(initial) { }

        protected override BaseAttribute<bool> CreateDuplicate()
        {
            return new TestBoolAttribute(_rawValue);
        }
    }

    /// <summary>Segundo stat int — usado para tests que requieren dos atributos distintos.</summary>
    public sealed class TestIntAttributeB : BaseAttribute<int>
    {
        public TestIntAttributeB() { }
        public TestIntAttributeB(int initial) : base(initial) { }

        protected override BaseAttribute<int> CreateDuplicate()
        {
            return new TestIntAttributeB(_rawValue);
        }
    }
}
