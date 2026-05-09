using HarmonyLib;

namespace LelbryBalanceFixes
{
    public interface IBalanceFix
    {
        string Id { get; }
        void Apply(Harmony harmony);
    }
}
