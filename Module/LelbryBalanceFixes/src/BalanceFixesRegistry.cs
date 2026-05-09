using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LelbryBalanceFixes
{
    public static class BalanceFixesRegistry
    {
        public static IReadOnlyList<IBalanceFix> Discover()
        {
            var result = new List<IBalanceFix>();
            var assembly = typeof(BalanceFixesRegistry).Assembly;

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IBalanceFix).IsAssignableFrom(type)) continue;
                if (type.GetCustomAttribute<BalanceFixAttribute>() == null) continue;

                try
                {
                    var instance = (IBalanceFix)Activator.CreateInstance(type);
                    result.Add(instance);
                }
                catch (Exception ex)
                {
                    ModLog.Error($"Failed to instantiate fix '{type.FullName}': {ex.Message}");
                }
            }

            return result;
        }

        public static IEnumerable<BalanceFixAttribute> GetAllMetadata()
        {
            return typeof(BalanceFixesRegistry).Assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IBalanceFix).IsAssignableFrom(t))
                .Select(t => t.GetCustomAttribute<BalanceFixAttribute>())
                .Where(a => a != null);
        }
    }
}
