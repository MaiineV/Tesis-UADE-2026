namespace Rollgeon.Attributes.Stats
{
    public sealed class Shield : BaseAttribute<int>
    {
        public Shield() { }
        public Shield(int initial) : base(initial) { }

        public override string GetAttributeName() => "Shield";

        protected override BaseAttribute<int> CreateDuplicate() => new Shield(_rawValue);
    }
}
