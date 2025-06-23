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
public abstract class EntryPropertyValues : PropertyValues
{
    private IReadOnlyList<IProperty>? _properties;
    private IReadOnlyList<IComplexProperty>? _complexCollectionProperties;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected EntryPropertyValues(InternalEntryBase internalEntry)
        : base(internalEntry)
    {
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override object ToObject()
        => Clone().ToObject();

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
            SetValuesFromInstance(InternalEntry, obj);
        }
        else if (obj is Dictionary<string, object> dictionary)
        {
            SetValues(dictionary);
        }
        else
        {
            SetValuesFromDto(InternalEntry, obj);
        }
    }

    private void SetValuesFromInstance(IInternalEntry entry, object obj)
    {
        var structuralType = entry.StructuralType;
        foreach (var property in structuralType.GetFlattenedProperties().Where(p => !p.IsShadowProperty()))
        {
            SetValueInternal(entry, property, property.GetGetter().GetClrValue(obj));
        }

        foreach (var complexProperty in structuralType.GetFlattenedComplexProperties())
        {
            if (complexProperty.IsShadowProperty()
                || !complexProperty.IsCollection)
            {
                continue;
            }

            var complexList = (IList?)complexProperty.GetGetter().GetClrValue(obj);
            SetValueInternal(entry, complexProperty, complexList);
            for (var i = 0; i < complexList?.Count; i++)
            {
                var complexObject = complexList[i];
                if (complexObject == null)
                {
                    continue;
                }

                var complexEntry = entry.GetComplexCollectionEntry(complexProperty, i);
                SetValuesFromInstance(complexEntry, complexObject);
            }
        }
    }

    private void SetValuesFromDto(IInternalEntry entry, object obj)
    {
        var structuralType = entry.StructuralType;
        foreach (var property in structuralType.GetFlattenedProperties())
        {
            var getter = obj.GetType().GetAnyProperty(property.Name)?.FindGetterProperty();
            if (getter != null)
            {
                SetValueInternal(entry, property, getter.GetValue(obj));
            }
        }

        foreach (var complexProperty in structuralType.GetFlattenedComplexProperties())
        {
            if (!complexProperty.IsCollection)
            {
                continue;
            }

            var getter = obj.GetType().GetAnyProperty(complexProperty.Name)?.FindGetterProperty();
            if (getter != null)
            {
                var dtoList = (IList?)getter.GetValue(obj);
                IList? complexList = null;
                if (dtoList != null)
                {
                    complexList = (IList)((IRuntimePropertyBase)complexProperty).GetIndexedCollectionAccessor().Create(dtoList.Count);
                    foreach (var item in dtoList)
                    {
                        if (item == null)
                        {
                            complexList.Add(null);
                        }
                        else
                        {
                            var complexObject = Activator.CreateInstance(complexProperty.ComplexType.ClrType)!;
                            foreach (var prop in complexProperty.ComplexType.GetProperties())
                            {
                                if (!prop.IsShadowProperty())
                                {
                                    var dtoGetter = item.GetType().GetAnyProperty(prop.Name)?.FindGetterProperty();
                                    if (dtoGetter != null && prop.PropertyInfo != null && prop.PropertyInfo.CanWrite)
                                    {
                                        prop.PropertyInfo.SetValue(complexObject, dtoGetter.GetValue(item));
                                    }
                                }
                            }

                            complexList.Add(complexObject);
                        }
                    }
                }

                SetValueInternal(entry, complexProperty, complexList);
                for (var i = 0; i < complexList?.Count; i++)
                {
                    var complexObject = complexList[i];
                    if (complexObject == null)
                    {
                        continue;
                    }

                    var complexEntry = entry.GetComplexCollectionEntry(complexProperty, i);
                    SetValuesFromInstance(complexEntry, complexObject);
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
        var values = new object?[Properties.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = GetValueInternal(InternalEntry, Properties[i]);
        }

        var cloned = new ArrayPropertyValues(InternalEntry, values);
        foreach (var complexProperty in ComplexCollectionProperties)
        {
            var collection = (IList?)GetValueInternal(InternalEntry, complexProperty);
            if (collection != null)
            {
                cloned[complexProperty] = collection;
            }
        }

        return cloned;
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

        foreach (var property in Properties)
        {
            SetValueInternal(InternalEntry, property, propertyValues[property]);
        }
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override void SetValues<TProperty>(IDictionary<string, TProperty> values)
    {
        Check.NotNull(values);

        var entry = InternalEntry;
        SetValuesFromDictionary(entry, values);
    }

    private void SetValuesFromDictionary<TProperty>(InternalEntryBase entry, IDictionary<string, TProperty> values)
    {
        var structuralType = entry.StructuralType;
        foreach (var property in structuralType.GetFlattenedProperties())
        {
            if (values.TryGetValue(property.Name, out var value))
            {
                SetValueInternal(entry, property, value);
            }
        }

        foreach (var complexProperty in structuralType.GetFlattenedComplexProperties())
        {
            if (!complexProperty.IsCollection)
            {
                continue;
            }

            if (values.TryGetValue(complexProperty.Name, out var complexValue))
            {
                var dictionaryList = (IList?)complexValue;
                IList? complexList = null;
                if (dictionaryList != null)
                {
                    complexList = (IList)((IRuntimePropertyBase)complexProperty).GetIndexedCollectionAccessor().Create(dictionaryList.Count);
                    foreach (var item in dictionaryList)
                    {
                        complexList.Add(CreateComplexObjectFromDictionary(complexProperty.ComplexType, (Dictionary<string, TProperty>?)item));
                    }
                }

                SetValueInternal(entry, complexProperty, complexList);
                for (var i = 0; i < complexList?.Count; i++)
                {
                    var complexObject = complexList[i];
                    if (complexObject == null)
                    {
                        continue;
                    }

                    var complexEntry = entry.GetComplexCollectionEntry(complexProperty, i);
                    SetValuesFromInstance(complexEntry, complexObject);
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
    public override IReadOnlyList<IProperty> Properties
    {
        [DebuggerStepThrough]
        get => _properties ??= StructuralType.GetFlattenedProperties().ToList();
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public override IReadOnlyList<IComplexProperty> ComplexCollectionProperties
    {
        [DebuggerStepThrough]
        get => _complexCollectionProperties ??= StructuralType.GetFlattenedComplexProperties().Where(p => p.IsCollection).ToList();
    }

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
                return GetValueInternal(InternalEntry, property);
            }

            var complexProperty = StructuralType.FindComplexProperty(propertyName);
            if (complexProperty != null)
            {
                return GetValueInternal(InternalEntry, complexProperty);
            }

            // If neither found, this will throw an appropriate exception
            return GetValueInternal(InternalEntry, StructuralType.GetProperty(propertyName));
        }
        set
        {
            var property = StructuralType.FindProperty(propertyName);
            if (property != null)
            {
                SetValueInternal(InternalEntry, property, value);
                return;
            }

            var complexProperty = StructuralType.FindComplexProperty(propertyName);
            if (complexProperty != null)
            {
                InternalEntry[complexProperty] = value;
                return;
            }

            // If neither found, this will throw an appropriate exception
            SetValueInternal(InternalEntry, StructuralType.GetProperty(propertyName), value);
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
        get => GetValueInternal(InternalEntry, StructuralType.CheckContains(property));
        set => SetValueInternal(InternalEntry, StructuralType.CheckContains(property), value);
    }

    /// <summary>
    ///     Gets or sets the value of the complex collection.
    /// </summary>
    /// <param name="complexProperty">The complex collection property.</param>
    /// <returns>A list of complex objects, not PropertyValues.</returns>
    public override IList? this[IComplexProperty complexProperty]
    {
        get => (IList?)GetValueInternal(InternalEntry, CheckCollection(complexProperty));
        set => SetValueInternal(InternalEntry, CheckCollection(complexProperty), value);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected abstract void SetValueInternal(IInternalEntry entry, IPropertyBase property, object? value);

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    [EntityFrameworkInternal]
    protected abstract object? GetValueInternal(IInternalEntry entry, IPropertyBase property);

    /// <summary>
    ///     Creates a complex object from a dictionary of property values using EF's property accessors.
    /// </summary>
    private object? CreateComplexObjectFromDictionary<TProperty>(IComplexType complexType, Dictionary<string, TProperty>? dictionary)
    {
        if (dictionary == null)
        {
            return null;
        }

        var complexObject = Activator.CreateInstance(complexType.ClrType)!;
        foreach (var property in complexType.GetProperties())
        {
            if (dictionary.TryGetValue(property.Name, out var value))
            {
                if (!property.IsShadowProperty())
                {
                    var propertyInfo = property.PropertyInfo;
                    if (propertyInfo != null && propertyInfo.CanWrite)
                    {
                        propertyInfo.SetValue(complexObject, value);
                    }
                }
            }
        }

        foreach (var nestedComplexProperty in complexType.GetComplexProperties())
        {
            if (dictionary.TryGetValue(nestedComplexProperty.Name, out var nestedValue))
            {
                if (nestedComplexProperty.IsCollection && nestedValue is IEnumerable<Dictionary<string, object>> nestedList)
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

                        if (!nestedComplexProperty.IsShadowProperty())
                        {
                            var propertyInfo = nestedComplexProperty.PropertyInfo;
                            if (propertyInfo != null && propertyInfo.CanWrite)
                            {
                                propertyInfo.SetValue(complexObject, nestedCollection);
                            }
                        }
                    }
                }
                else if (!nestedComplexProperty.IsCollection && nestedValue is Dictionary<string, object> nestedDict)
                {
                    var nestedComplexObject = CreateComplexObjectFromDictionary(nestedComplexProperty.ComplexType, nestedDict);
                    if (!nestedComplexProperty.IsShadowProperty())
                    {
                        var propertyInfo = nestedComplexProperty.PropertyInfo;
                        if (propertyInfo != null && propertyInfo.CanWrite)
                        {
                            propertyInfo.SetValue(complexObject, nestedComplexObject);
                        }
                    }
                }
            }
        }

        return complexObject;
    }
}
