// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore;

public class PropertyValuesInMemoryTest(PropertyValuesInMemoryTest.PropertyValuesInMemoryFixture fixture)
    : PropertyValuesTestBase<PropertyValuesInMemoryTest.PropertyValuesInMemoryFixture>(fixture)
{
    public override Task Complex_current_values_can_be_accessed_as_a_property_dictionary_using_IProperty()
        => Assert.ThrowsAsync<NullReferenceException>( // In-memory database cannot query complex types
            () => base.Complex_current_values_can_be_accessed_as_a_property_dictionary_using_IProperty());

    public override Task Complex_original_values_can_be_accessed_as_a_property_dictionary_using_IProperty()
        => Assert.ThrowsAsync<NullReferenceException>( // In-memory database cannot query complex types
            () => base.Complex_original_values_can_be_accessed_as_a_property_dictionary_using_IProperty());

    public override Task Complex_store_values_can_be_accessed_as_a_property_dictionary_using_IProperty()
        => Assert.ThrowsAsync<NullReferenceException>( // In-memory database cannot query complex types
            () => base.Complex_store_values_can_be_accessed_as_a_property_dictionary_using_IProperty());

    public override Task Complex_store_values_can_be_accessed_asynchronously_as_a_property_dictionary_using_IProperty()
        => Assert.ThrowsAsync<NullReferenceException>( // In-memory database cannot query complex types
            () => base.Complex_store_values_can_be_accessed_asynchronously_as_a_property_dictionary_using_IProperty());
            
    public override Task Values_can_be_reloaded_from_database_for_entity_in_any_state_with_inheritance(EntityState state, bool async)
        => Assert.ThrowsAnyAsync<Exception>( // In-memory database cannot query complex types
            () => base.Values_can_be_reloaded_from_database_for_entity_in_any_state_with_inheritance(state, async));

    // Complex collection tests - InMemory provider doesn't support complex collections yet
    public override Task Complex_collection_current_values_can_be_accessed_as_a_property_dictionary()
        => Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Complex_collection_current_values_can_be_accessed_as_a_property_dictionary());

    public override Task Complex_collection_original_values_can_be_accessed_as_a_property_dictionary()
        => Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Complex_collection_original_values_can_be_accessed_as_a_property_dictionary());

    public override Task Complex_collection_store_values_can_be_accessed_as_a_property_dictionary()
        => Assert.ThrowsAsync<InvalidOperationException>(
            () => base.Complex_collection_store_values_can_be_accessed_as_a_property_dictionary());

    public override Task Complex_collection_store_values_can_be_accessed_asynchronously_as_a_property_dictionary()
        => Assert.ThrowsAsync<InvalidOperationException>( 
            () => base.Complex_collection_store_values_can_be_accessed_asynchronously_as_a_property_dictionary());
    public override void Setting_complex_collection_values_from_object_works()
        => Assert.Throws<InvalidOperationException>(
            () => base.Setting_complex_collection_values_from_object_works());

    public class PropertyValuesInMemoryFixture : PropertyValuesFixtureBase
    {
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(w => w.Ignore(CoreEventId.MappedComplexPropertyIgnoredWarning)
                    .Ignore(CoreEventId.MappedEntityTypeIgnoredWarning))
                .EnableSensitiveDataLogging(false);

        protected override ITestStoreFactory TestStoreFactory
            => InMemoryTestStoreFactory.Instance;

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            // In-memory database doesn't support complex type queries
            modelBuilder.Entity<Building>(
                b =>
                {
                    b.Ignore(e => e.Culture);
                    b.Ignore(e => e.Milk);
                });

            // In-memory database doesn't support complex collections
            modelBuilder.Ignore<School>();
        }
    }
}
