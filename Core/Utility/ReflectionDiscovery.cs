namespace NicheImageRipper.Core.Utility;

internal static class ReflectionDiscovery
{
    public static IReadOnlyList<T> DiscoverImplementations<T>(Func<Type, bool>? exclude = null) where T : class
    {
        var interfaceType = typeof(T);
        return AppDomain.CurrentDomain
                        .GetAssemblies()
                        .SelectMany(a =>
                         {
                             try { return a.GetTypes(); }
                             catch { return []; }
                         })
                        .Where(t =>
                             !t.IsAbstract &&
                             !t.IsInterface &&
                             interfaceType.IsAssignableFrom(t) &&
                             (exclude is null || !exclude(t)))
                        .Select(t => (T)Activator.CreateInstance(t)!)
                        .ToList();
    }
}