namespace EcsCodeGen.World;

using Microsoft.CodeAnalysis;

public static class WorldExtensionsMethodsTemplate
{
    public static string CreateForUniqueComponent(string componentName)
    {
        var code = $$"""
                     public static Entity {{componentName}}(this IWorld world) => world.GetSingle(in new QueryDescription().WithAll<{{componentName}}>());
                     """;

        return code;
    }

    public static string CreateForPrimaryEntityIndex(string componentName, string fieldName, ITypeSymbol symbol)
    {
        var code = $$"""
                     public static Entity? GetEntityWith{{symbol.Name}}(this IWorld world, {{symbol.ToDisplayString()}} value)
                     {
                         using var _ = GetEntities(world, out var entities, in new QueryDescription().WithAll<{{componentName}}>(),
                             e => e.{{componentName}}().{{fieldName}} == value);
                         return entities.Count switch
                         {
                             0 => null,
                             > 1 => throw new SingleEntityException(entities.Count),
                             _ => entities[0]
                         };
                     }
                     """;

        return code;
    }

    public static string CreateForEntityIndex(string componentName, string fieldName, ITypeSymbol symbol)
    {
        var code = $$"""
                     public static List<Entity> GetEntitiesWith{{componentName}}(this IWorld world, {{symbol.ToDisplayString()}} value)
                     {
                         using var _ = GetEntities(world, out var entities, in new QueryDescription().WithAll<{{componentName}}>(),
                             e => e.{{componentName}}().{{fieldName}} == value);
                         return entities;
                     }
                     """;

        return code;
    }

    public static string CreateGetEntities()
    {
        return $$"""
                     public static IDisposable GetEntities(
                         this IWorld world,
                         out List<Entity> buffer,
                         in QueryDescription queryDescription,
                         Func<Entity, bool> baseFilter,
                         Func<Entity, bool>? filter = null)
                     {
                         var pooledObject = ListPool<Entity>.Get(out buffer);
                         world.GetEntities(in queryDescription, buffer);

                         if (filter != null)
                         {
                             buffer.RemoveAllWithSwap(e => !(baseFilter(e) && filter(e)));
                         }
                         else
                         {
                             buffer.RemoveAllWithSwap(e => !baseFilter(e));
                         }

                         return pooledObject;
                     }
                     """;
    }
}
