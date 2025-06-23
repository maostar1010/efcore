// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.ChangeTracking.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class ArrayPropertyValues : PropertyValues
{
    private readonly object?[] _values;
    private IReadOnlyList<IProperty>? _properties;
    private IReadOnlyList<IComplexProperty>? _complexProperties;
    private Dictionary<int, IList<ArrayPropertyValues?>?>? _complexCollectionValues;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public ArrayPropertyValues(InternalEntryBase internalEntry, object?[] values)
        : base(internalEntry)
        => _values = values;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override object ToObject()
    {
        if (StructuralType is IEntityType entityType)
        {
            var entity = entityType.GetOrCreateMaterializer(MaterializerSource)(
                new MaterializationContext(new ValueBuffer(_values), InternalEntry.Context));

            ComplexValuesToObject(entity);

            return entity;
        }
        else
        {
            var materializationContext = new MaterializationContext(new ValueBuffer(_values), InternalEntry.Context);

            var materializationContextParam = Expression.Parameter(typeof(MaterializationContext), "materializationContext");
            var materializeExpression = MaterializerSource.CreateMaterializeExpression(
                new EntityMaterializerSourceParameters(StructuralType, "instance", null),
                materializationContextParam);

            var lambda = Expression.Lambda<Func<MaterializationContext, object>>(
                materializeExpression, materializationContextParam);

            var complexObject = lambda.Compile()(materializationContext);

            var complexType = (IComplexType)StructuralType;
            var setter = ((IRuntimeComplexProperty)complexType.ComplexProperty).MaterializationSetter;

            ComplexValuesToObject(complexObject);

            return complexObject;
        }
    }

    private object ComplexValuesToObject(object containingObject)
    {
        if (_complexCollectionValues == null)
        {
            return containingObject;
        }

        foreach (var kvp in _complexCollectionValues)
        {
            var complexPropertyIndex = kvp.Key;
            var propertyValuesList = kvp.Value;
            var complexProperty = ComplexCollectionProperties
                .First(cp => cp.GetIndex() == complexPropertyIndex);

            if (propertyValuesList == null)
            {
                continue;
            }

            var list = (IList)((IRuntimeComplexProperty)complexProperty).GetIndexedCollectionAccessor().Create(propertyValuesList.Count);
            containingObject = ((IRuntimeComplexProperty)complexProperty).GetSetter().SetClrValue(containingObject, list);

            foreach (var propertyValues in propertyValuesList)
            {
                list.Add(propertyValues?.ToObject() ?? complexProperty.ComplexType.ClrType.GetDefaultValue());
            }
        }

        return containingObject;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override void SetValues(object obj)
    {
        Check.NotNull(obj);

        if (obj.GetType() == StructuralType.ClrType)
        {
            for (var i = 0; i < _values.Length; i++)
            {
                if (!Properties[i].IsShadowProperty())
                {
                    SetValue(i, Properties[i].GetGetter().GetClrValueUsingContainingEntity(obj));
                }
            }

            foreach (var complexProperty in ComplexCollectionProperties)
            {
                if (complexProperty.IsShadowProperty())
                {
                    continue;
                }

                var complexValue = complexProperty.GetGetter().GetClrValueUsingContainingEntity(obj);
                List<ArrayPropertyValues?>? propertyValuesList = null;
                if (complexValue is IList collection)
                {
                    propertyValuesList = [];
                    var ordinal = 0;
                    foreach (var item in collection)
                    {
                        if (item != null)
                        {
                            var entry = new InternalComplexEntry((IRuntimeComplexType)complexProperty.ComplexType, InternalEntry, ordinal);
                            var complexPropertyValues = CreateComplexPropertyValues(item, entry);
                            propertyValuesList.Add(complexPropertyValues);
                        }
                        else
                        {
                            propertyValuesList.Add(null!);
                        }
                        ordinal++;
                    }
                }

                _complexCollectionValues ??= [];
                _complexCollectionValues[complexProperty.GetIndex()] = propertyValuesList;
            }
        }
        else
        {
            for (var i = 0; i < _values.Length; i++)
            {
                var getter = obj.GetType().GetAnyProperty(Properties[i].Name)?.FindGetterProperty();
                if (getter != null)
                {
                    SetValue(i, getter.GetValue(obj));
                }
            }

            foreach (var complexProperty in ComplexCollectionProperties)
            {
                var getter = obj.GetType().GetAnyProperty(complexProperty.Name)?.FindGetterProperty();
                if (getter != null)
                {
                    List<ArrayPropertyValues?>? propertyValuesList = null;
                    var complexValue = getter.GetValue(obj);
                    if (complexValue is IList collection)
                    {
                        propertyValuesList = [];
                        var ordinal = 0;
                        foreach (var item in collection)
                        {
                            if (item != null)
                            {
                                var entry = new InternalComplexEntry((IRuntimeComplexType)complexProperty.ComplexType, InternalEntry, ordinal);
                                var complexPropertyValues = CreateComplexPropertyValues(item, entry);
                                propertyValuesList.Add(complexPropertyValues);
                            }
                            else
                            {
                                propertyValuesList.Add(null!);
                            }
                            ordinal++;
                        }
                    }

                    _complexCollectionValues ??= [];
                    _complexCollectionValues[complexProperty.GetIndex()] = propertyValuesList;
                }
            }
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override PropertyValues Clone()
    {
        var copies = new object[_values.Length];
        Array.Copy(_values, copies, _values.Length);

        var clone = new ArrayPropertyValues(InternalEntry, copies);
        if (_complexCollectionValues != null)
        {
            clone._complexCollectionValues = [];
            foreach (var kvp in _complexCollectionValues)
            {
                if (kvp.Value != null)
                {
                    var clonedList = new List<ArrayPropertyValues?>();
                    foreach (var propertyValues in kvp.Value)
                    {
                        clonedList.Add((ArrayPropertyValues?)propertyValues?.Clone());
                    }
                    clone._complexCollectionValues[kvp.Key] = clonedList;
                }
            }
        }

        return clone;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override void SetValues(PropertyValues propertyValues)
    {
        Check.NotNull(propertyValues);

        for (var i = 0; i < _values.Length; i++)
        {
            SetValue(i, propertyValues[Properties[i]]);
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override IReadOnlyList<IProperty> Properties
        => _properties ??= StructuralType.GetFlattenedProperties().ToList();

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override IReadOnlyList<IComplexProperty> ComplexCollectionProperties
        => _complexProperties ??= StructuralType.GetFlattenedComplexProperties().Where(p => p.IsCollection).ToList();

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override object? this[string propertyName]
    {
        get
        {
            var property = StructuralType.FindProperty(propertyName);
            if (property != null)
            {
                return _values[property.GetIndex()];
            }

            var complexProperty = StructuralType.FindComplexProperty(propertyName);
            if (complexProperty != null)
            {
                return this[complexProperty];
            }

            // If neither found this will throw an appropriate exception
            return _values[StructuralType.GetProperty(propertyName).GetIndex()];
        }
        set
        {
            var property = StructuralType.FindProperty(propertyName);
            if (property != null)
            {
                SetValue(property.GetIndex(), value);
                return;
            }

            var complexProperty = StructuralType.FindComplexProperty(propertyName);
            if (complexProperty != null)
            {
                if (complexProperty.IsCollection && value is IList<ArrayPropertyValues?> propertyValuesList)
                {
                    _complexCollectionValues ??= [];
                    _complexCollectionValues[complexProperty.GetIndex()] = propertyValuesList;
                }
            }

            // If neither found this will throw an appropriate exception
            SetValue(StructuralType.GetProperty(propertyName).GetIndex(), value);
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override object? this[IProperty property]
    {
        get => _values[StructuralType.CheckContains(property).GetIndex()];
        set => SetValue(StructuralType.CheckContains(property).GetIndex(), value);
    }

    /// <summary>
    ///     Gets or sets the value of the complex collection.
    /// </summary>
    /// <param name="complexProperty">The complex collection property.</param>
    /// <returns>A list of complex objects, not PropertyValues.</returns>
    public override IList? this[IComplexProperty complexProperty]
    {
        get
        {
            CheckCollection(complexProperty);

            if (_complexCollectionValues == null
                || !_complexCollectionValues.TryGetValue(complexProperty.GetIndex(), out var propertyValuesList)
                || propertyValuesList == null)
            {
                return null;
            }

            var complexObjectsList = (IList)((IRuntimePropertyBase)complexProperty).GetIndexedCollectionAccessor().Create(propertyValuesList.Count);
            foreach (var propertyValues in propertyValuesList)
            {
                complexObjectsList.Add(propertyValues?.ToObject());
            }

            return complexObjectsList;
        }

        set => SetComplexCollectionValue(CheckCollection(complexProperty), GetComplexCollectionPropertyValues(complexProperty, value));
    }

    /// <inheritdoc/>
    public override void SetValues<TProperty>(IDictionary<string, TProperty> values)
    {
        Check.NotNull(values);
        foreach (var property in Properties)
        {
            if (values.TryGetValue(property.Name, out var value))
            {
                this[property] = value;
            }
        }

        foreach (var complexProperty in ComplexCollectionProperties)
        {
            if (values.TryGetValue(complexProperty.Name, out var complexValue))
            {
                if (!complexProperty.IsCollection)
                {
                    continue;
                }

                this[complexProperty] = (IList?)complexValue;
            }
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override TValue GetValue<TValue>(string propertyName)
        => (TValue)this[propertyName]!;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override TValue GetValue<TValue>(IProperty property)
        => (TValue)this[property]!;

    private void SetValue(int index, object? value)
    {
        var property = Properties[index];

        if (value != null)
        {
            if (!property.ClrType.IsInstanceOfType(value))
            {
                throw new InvalidCastException(
                    CoreStrings.InvalidType(
                        property.Name,
                        property.DeclaringType.DisplayName(),
                        value.GetType().DisplayName(),
                        property.ClrType.DisplayName()));
            }
        }
        else
        {
            if (!property.ClrType.IsNullableType())
            {
                throw new InvalidOperationException(
                    CoreStrings.ValueCannotBeNull(
                        property.Name,
                        property.DeclaringType.DisplayName(),
                        property.ClrType.DisplayName()));
            }
        }

        _values[index] = value;
    }

    private IEntityMaterializerSource MaterializerSource
        => InternalEntry.StateManager.EntityMaterializerSource;

    private ArrayPropertyValues CreateComplexPropertyValues(object complexObject, InternalComplexEntry entry)
    {
        var complexType = entry.StructuralType;
        var properties = complexType.GetFlattenedProperties().ToList();
        var values = new object?[properties.Count];

        for (var i = 0; i < properties.Count; i++)
        {
            var property = properties[i];
            var getter = property.GetGetter();
            values[i] = getter.GetClrValue(complexObject);
        }

        var complexPropertyValues = new ArrayPropertyValues(entry, values);

        foreach (var nestedComplexProperty in complexType.GetComplexProperties())
        {
            if (!nestedComplexProperty.IsCollection)
            {
                continue;
            }

            var nestedCollection = nestedComplexProperty.GetGetter().GetClrValue(complexObject) as IList;
            if (nestedCollection != null && nestedCollection.Count > 0)
            {
                var nestedPropertyValuesList = new List<ArrayPropertyValues?>();
                var ordinal = 0;
                foreach (var item in nestedCollection)
                {
                    if (item == null)
                    {
                        nestedPropertyValuesList.Add(null);
                    }
                    else
                    {
                        var nestedEntry = new InternalComplexEntry((IRuntimeComplexType)nestedComplexProperty.ComplexType, InternalEntry, ordinal);
                        var nestedPropertyValues = CreateComplexPropertyValues(item, nestedEntry);
                        nestedPropertyValuesList.Add(nestedPropertyValues);
                    }
                    ordinal++;
                }

                complexPropertyValues.SetComplexCollectionValue(nestedComplexProperty, nestedPropertyValuesList);
            }
        }

        return complexPropertyValues;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    [EntityFrameworkInternal]
    internal void SetComplexCollectionValue(IComplexProperty complexProperty, IList<ArrayPropertyValues?>? propertyValuesList)
    {
        _complexCollectionValues ??= [];
        _complexCollectionValues[complexProperty.GetIndex()] = propertyValuesList;
    }

    /// <summary>
    ///     Creates a complex object from a dictionary of property values.
    /// </summary>
    private object CreateComplexObjectFromDictionary(IComplexType complexType, Dictionary<string, object> dictionary)
    {
        var complexObject = Activator.CreateInstance(complexType.ClrType)!;

        foreach (var property in complexType.GetProperties())
        {
            if (dictionary.TryGetValue(property.Name, out var value))
            {
                if (!property.IsShadowProperty())
                {
                    var setter = ClrPropertySetterFactory.Instance.Create(property);
                    setter.SetClrValueUsingContainingEntity(complexObject, value);
                }
            }
        }

        foreach (var nestedComplexProperty in complexType.GetComplexProperties())
        {
            if (dictionary.TryGetValue(nestedComplexProperty.Name, out var nestedValue))
            {
                if (nestedComplexProperty.IsCollection && nestedValue is IList<Dictionary<string, object>> nestedList)
                {
                    var nestedCollectionType = nestedComplexProperty.ClrType;
                    var nestedCollection = Activator.CreateInstance(nestedCollectionType);
                    if (nestedCollection is IList nestedIList)
                    {
                        foreach (var nestedItemDict in nestedList)
                        {
                            if (nestedItemDict == null)
                            {
                                nestedIList.Add(null);
                            }
                            else
                            {
                                var nestedComplexObject = CreateComplexObjectFromDictionary(nestedComplexProperty.ComplexType, nestedItemDict);
                                nestedIList.Add(nestedComplexObject);
                            }
                        }

                        var nestedSetter = ClrPropertySetterFactory.Instance.Create(nestedComplexProperty);
                        nestedSetter.SetClrValueUsingContainingEntity(complexObject, nestedCollection);
                    }
                }
                else if (!nestedComplexProperty.IsCollection && nestedValue is Dictionary<string, object> nestedDict)
                {
                    var nestedComplexObject = CreateComplexObjectFromDictionary(nestedComplexProperty.ComplexType, nestedDict);
                    var nestedSetter = ClrPropertySetterFactory.Instance.Create(nestedComplexProperty);
                    nestedSetter.SetClrValueUsingContainingEntity(complexObject, nestedComplexObject);
                }
            }
        }

        return complexObject;
    }

    private IList<ArrayPropertyValues?>? GetComplexCollectionPropertyValues(IComplexProperty complexProperty, IList? collection)
    {
        if (collection == null)
        {
            return null;
        }

        var propertyValuesList = new List<ArrayPropertyValues?>();
        var ordinal = 0;
        foreach (var item in collection)
        {
            if (item != null)
            {
                var complexPropertyValues = CreateComplexPropertyValues(item,
                    new InternalComplexEntry((IRuntimeComplexType)complexProperty.ComplexType, InternalEntry, ordinal));
                propertyValuesList.Add(complexPropertyValues);
            }
            else
            {
                propertyValuesList.Add(null);
            }
            ordinal++;
        }

        return propertyValuesList;
    }
}
