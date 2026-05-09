using System;

namespace LelbryBalanceFixes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class BalanceFixAttribute : Attribute
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }

        public BalanceFixAttribute(string id, string title, string description)
        {
            Id = id;
            Title = title;
            Description = description;
        }
    }
}
