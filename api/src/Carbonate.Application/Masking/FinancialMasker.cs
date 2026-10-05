using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Carbonate.Application.Platform.Auth;

namespace Carbonate.Application.Masking;

public sealed class FinancialMasker : IFinancialMasker
{
    private sealed record PropertyPlan(PropertyInfo Property, FinancialTier? Tier, bool Recurse);

    private static readonly ConcurrentDictionary<Type, PropertyPlan[]> Plans = new();

    public void Mask(object? dto, ICurrentUser user) =>
        Walk(dto, user, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static void Walk(object? node, ICurrentUser user, HashSet<object> seen)
    {
        if (node is null || node is string)
        {
            return;
        }

        if (node is IDictionary dictionary)
        {
            foreach (var value in dictionary.Values)
            {
                Walk(value, user, seen);
            }

            return;
        }

        if (node is IEnumerable items)
        {
            foreach (var item in items)
            {
                Walk(item, user, seen);
            }

            return;
        }

        var type = node.GetType();
        if (!IsOurs(type) || !seen.Add(node))
        {
            return;
        }

        foreach (var plan in PlanFor(type))
        {
            if (plan.Tier is { } tier)
            {
                if (!CanSee(tier, node, user))
                {
                    plan.Property.SetValue(node, null);
                }

                continue;
            }

            if (plan.Recurse)
            {
                Walk(plan.Property.GetValue(node), user, seen);
            }
        }
    }

    private static bool CanSee(FinancialTier tier, object owner, ICurrentUser user) => tier switch
    {
        FinancialTier.Price => user.HasPermission(PermissionCodes.FinanceViewClientPrice),
        FinancialTier.Cost => user.HasPermission(PermissionCodes.FinanceViewInternalCost),
        FinancialTier.Margin => user.HasPermission(PermissionCodes.FinanceViewMargin),
        FinancialTier.Staff => CanSeeStaffCost(owner, user),
        _ => false,
    };

    private static bool CanSeeStaffCost(object owner, ICurrentUser user)
    {
        var scope = user.ScopeOf(PermissionCodes.FinanceViewStaffCost);
        if (scope == PermissionScope.All)
        {
            return true;
        }

        // Everyone else sees only their own record. With no owner to compare, hide it.
        return scope == PermissionScope.Self
            && owner.GetType().GetProperty("UserId")?.GetValue(owner) is Guid ownerId
            && ownerId == user.UserId;
    }

    private static PropertyPlan[] PlanFor(Type type) => Plans.GetOrAdd(type, static t =>
    {
        var plans = new List<PropertyPlan>();
        foreach (var property in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var tag = property.GetCustomAttribute<FinancialFieldAttribute>();
            if (tag is not null)
            {
                RequireMaskable(property);
                plans.Add(new PropertyPlan(property, tag.Tier, false));
            }
            else
            {
                plans.Add(new PropertyPlan(property, null, MayHoldDtos(property.PropertyType)));
            }
        }

        return [.. plans];
    });

    /// <summary>A field we cannot null out would leak, so refuse loudly instead of skipping it.</summary>
    private static void RequireMaskable(PropertyInfo property)
    {
        var nullable = !property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) is not null;
        if (!nullable || property.SetMethod is null)
        {
            throw new InvalidOperationException(
                $"{property.DeclaringType?.Name}.{property.Name} is tagged [FinancialField] so it must be nullable and settable.");
        }
    }

    private static bool MayHoldDtos(Type type) =>
        !(type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
          || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
          || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(byte[])
          || Nullable.GetUnderlyingType(type) is not null);

    private static bool IsOurs(Type type) =>
        !type.IsValueType && type.Assembly.GetName().Name?.StartsWith("Carbonate", StringComparison.Ordinal) == true;
}
