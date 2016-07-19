﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using Xunit;

namespace Microsoft.OData.Tests
{
    public class MultiBindingTests
    {
        private static readonly IEdmModel Model = GetModel();
        private readonly static Uri ServiceRoot = new Uri("http://host");
        private readonly static IEdmEntitySet EntitySet = Model.EntityContainer.FindEntitySet("EntitySet");
        private static readonly IEdmStructuredType EntityType = Model.FindType("NS.EntityType") as IEdmStructuredType;

        #region Reader and Writer Tests

        // Multi-binding on complex
        private readonly static string complexPayload = "{" +
                              "\"@odata.context\":\"http://host/$metadata#EntitySet/$entity\"," +
                              "\"ID\":\"TopEntity\"," +
                              "\"complexProp1\":{" +
                                  "\"Prop1\":\"complexProp1\"," +
                                  "\"CollectionOfNavOnComplex\":[" +
                                      "{\"ID\":\"NavEntity1\"}," +
                                      "{\"ID\":\"NavEntity2\"}]}," +
                              "\"complexProp2\":" +
                                  "{\"Prop1\":\"complexProp2\"," +
                                  "\"CollectionOfNavOnComplex\":[" +
                                      "{\"ID\":\"NavEntity2\"}," +
                                      "{\"ID\":\"NavEntity1\"}]" +
                              "}" +
                              "}";

        [Fact]
        public void MultiBindingOnComplexWriterTest()
        {
            ODataResource topEntity = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "TopEntity" } } };

            ODataNestedResourceInfo complex1Info = new ODataNestedResourceInfo() { Name = "complexProp1" };
            ODataResource complex1 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "Prop1", Value = "complexProp1" } } };

            ODataNestedResourceInfo complex2Info = new ODataNestedResourceInfo() { Name = "complexProp2" };
            ODataResource complex2 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "Prop1", Value = "complexProp2" } } };

            ODataNestedResourceInfo navOnComplexInfo = new ODataNestedResourceInfo() { Name = "CollectionOfNavOnComplex" , IsCollection = true };

            ODataResourceSet resourceSet = new ODataResourceSet();
            ODataResource navEntity1 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "NavEntity1" } } };
            ODataResource navEntity2 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "NavEntity2" } } };

            string actual = WriteJsonLightEntry(Model, EntitySet, (writer) =>
            {
                writer.WriteStart(topEntity);

                // complex1 and its navigation property
                writer.WriteStart(complex1Info);
                writer.WriteStart(complex1);
                writer.WriteStart(navOnComplexInfo);
                writer.WriteStart(resourceSet);
                writer.WriteStart(navEntity1);
                writer.WriteEnd();
                writer.WriteStart(navEntity2);
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();

                // complex2 and its navigation property
                writer.WriteStart(complex2Info);
                writer.WriteStart(complex2);
                writer.WriteStart(navOnComplexInfo);
                writer.WriteStart(resourceSet);
                writer.WriteStart(navEntity2);
                writer.WriteEnd();
                writer.WriteStart(navEntity1);
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();

                // end of top entity
                writer.WriteEnd();
            });

            Assert.Equal(complexPayload, actual);
        }

        [Fact]
        public void MultiBindingOnComplexReaderTest()
        {
            var entries = ReadPayload(complexPayload, Model, EntitySet, EntityType);

            entries[0].Id.Should().Be(new Uri("http://host/NavEntitySet1('NavEntity1')"));
            entries[1].Id.Should().Be(new Uri("http://host/NavEntitySet1('NavEntity2')"));
            entries[2].TypeName.Should().Be("NS.ComplexType");
            entries[3].Id.Should().Be(new Uri("http://host/NavEntitySet2('NavEntity2')"));
            entries[4].Id.Should().Be(new Uri("http://host/NavEntitySet2('NavEntity1')"));
            entries[5].TypeName.Should().Be("NS.ComplexType");
            entries[6].Id.Should().Be(new Uri("http://host/EntitySet('TopEntity')"));
        }

        [Fact]
        public void MultiBindingOnComplexWithTypeCastWriterReaderTest()
        {
            ODataResource topEntity = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "TopEntity" } } };

            ODataNestedResourceInfo complex1Info = new ODataNestedResourceInfo() { Name = "complexProp1" };
            ODataResource complex1 = new ODataResource() { TypeName = "NS.DerivedComplexType", Properties = new[]
            {
                new ODataProperty { Name = "Prop1", Value = "complexProp1" }, 
                new ODataProperty { Name = "DerivedProp", Value = "DerivedComplexProp" }
            } };

            ODataNestedResourceInfo navOnComplexInfo = new ODataNestedResourceInfo() { Name = "CollectionOfNavOnComplex", IsCollection = true };

            ODataResourceSet resourceSet = new ODataResourceSet();
            ODataResource navEntity1 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "NavEntity1" } } };
            ODataResource navEntity2 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "NavEntity2" } } };

            // Writer
            string actual = WriteJsonLightEntry(Model, EntitySet, (writer) =>
            {
                writer.WriteStart(topEntity);

                // complex1 and its navigation property
                writer.WriteStart(complex1Info);
                writer.WriteStart(complex1);
                writer.WriteStart(navOnComplexInfo);
                writer.WriteStart(resourceSet);
                writer.WriteStart(navEntity1);
                writer.WriteEnd();
                writer.WriteStart(navEntity2);
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();

                // end of top entity
                writer.WriteEnd();
            });

            string expected = "{" +
                              "\"@odata.context\":\"http://host/$metadata#EntitySet/$entity\"," +
                              "\"ID\":\"TopEntity\"," +
                              "\"complexProp1\":{" +
                                  "\"@odata.type\":\"#NS.DerivedComplexType\"," +
                                  "\"Prop1\":\"complexProp1\"," +
                                  "\"DerivedProp\":\"DerivedComplexProp\"," +
                                  "\"CollectionOfNavOnComplex\":[" +
                                      "{\"ID\":\"NavEntity1\"}," +
                                      "{\"ID\":\"NavEntity2\"}]" +
                              "}}";

            Assert.Equal(expected, actual);

            // Reader
            var entryList = ReadPayload(expected, Model, EntitySet, EntityType);

            entryList[0].Id.Should().Be(new Uri("http://host/NavEntitySet1('NavEntity1')"));
            entryList[1].Id.Should().Be(new Uri("http://host/NavEntitySet1('NavEntity2')"));
            entryList[2].Id.Should().Be(null);
            entryList[2].TypeName.Should().Be("NS.DerivedComplexType");
            entryList[3].Id.Should().Be(new Uri("http://host/EntitySet('TopEntity')"));
        }


        private readonly static string containmentPayload = "{" +
                              "\"@odata.context\":\"http://host/$metadata#EntitySet/$entity\"," +
                              "\"ID\":\"TopEntity\"," +
                              "\"ContainedNav1@odata.context\":\"http://host/$metadata#EntitySet('TopEntity')/ContainedNav1/$entity\"," +
                              "\"ContainedNav1\":{" +
                                "\"ID\":\"ContainedNav1\"," +
                                "\"NavOnContained\":{" +
                                    "\"ID\":\"NavEntity1\"" +
                                "}" +
                              "}," +
                              "\"ContainedNav2@odata.context\":\"http://host/$metadata#EntitySet('TopEntity')/ContainedNav2/$entity\"," +
                              "\"ContainedNav2\":{" +
                                "\"ID\":\"ContainedNav2\"," +
                                "\"NavOnContained\":{" +
                                    "\"ID\":\"NavEntity2\"" +
                                "}" +
                              "}" +
                              "}";

        [Fact]
        public void MultiBindingOnContainementWriterTest()
        {
            ODataResource topEntity = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "TopEntity" } } };

            ODataNestedResourceInfo contained1Info = new ODataNestedResourceInfo() { Name = "ContainedNav1", IsCollection = false };
            ODataResource contained1 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "ContainedNav1" } } };

            ODataNestedResourceInfo contained2Info = new ODataNestedResourceInfo() { Name = "ContainedNav2", IsCollection = false };
            ODataResource contained2 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "ContainedNav2" } } };

            ODataNestedResourceInfo navOnContainedInfo = new ODataNestedResourceInfo() { Name = "NavOnContained", IsCollection = false };

            ODataResource navEntity1 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "NavEntity1" } } };
            ODataResource navEntity2 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "NavEntity2" } } };

            string actual = WriteJsonLightEntry(Model, EntitySet, (writer) =>
            {
                writer.WriteStart(topEntity);

                // contained1 and its navigation property
                writer.WriteStart(contained1Info);
                writer.WriteStart(contained1);
                writer.WriteStart(navOnContainedInfo);
                writer.WriteStart(navEntity1);
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();

                // contained2 and its navigation property
                writer.WriteStart(contained2Info);
                writer.WriteStart(contained2);
                writer.WriteStart(navOnContainedInfo);
                writer.WriteStart(navEntity2);
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();
                writer.WriteEnd();

                // end of top entity
                writer.WriteEnd();
            });

            Assert.Equal(containmentPayload, actual);
        }

        [Fact]
        public void MultiBindingOnContainmentReaderTest()
        {
            var entries = ReadPayload(containmentPayload, Model, EntitySet, EntityType);

            entries[0].Id.Should().Be(new Uri("http://host/NavEntitySet1('NavEntity1')"));
            entries[1].Id.Should().Be(new Uri("http://host/EntitySet('TopEntity')/ContainedNav1"));
            entries[2].Id.Should().Be(new Uri("http://host/NavEntitySet2('NavEntity2')"));
            entries[3].Id.Should().Be(new Uri("http://host/EntitySet('TopEntity')/ContainedNav2"));
            entries[4].Id.Should().Be(new Uri("http://host/EntitySet('TopEntity')"));
        }

        [Fact]
        public void BindPathWithTypeCastWriterReaderTest()
        {
            ODataResource topEntity = new ODataResource() { TypeName = "NS.DerivedEntityType", Properties = new[] { new ODataProperty { Name = "ID", Value = "TopEntity" } } };
            ODataNestedResourceInfo navInfo = new ODataNestedResourceInfo() { Name = "NavOnDerived", IsCollection = false };
            ODataResource navEntity1 = new ODataResource() { Properties = new[] { new ODataProperty { Name = "ID", Value = "NavEntity1" } } };

            // Writer
            string actual = WriteJsonLightEntry(Model, EntitySet, (writer) =>
            {
                writer.WriteStart(topEntity);

                writer.WriteStart(navInfo);
                writer.WriteStart(navEntity1);
                writer.WriteEnd();
                writer.WriteEnd();

                // end of top entity
                writer.WriteEnd();
            });

            string expected = "{" +
                              "\"@odata.context\":\"http://host/$metadata#EntitySet/$entity\"," +
                              "\"@odata.type\":\"#NS.DerivedEntityType\"," +
                              "\"ID\":\"TopEntity\"," +
                              "\"NavOnDerived\":{\"ID\":\"NavEntity1\"}" +
                              "}";

            Assert.Equal(expected, actual);

            // Reader
            var entries = ReadPayload(expected, Model, EntitySet, EntityType);
            entries[0].Id.Should().Be(new Uri("http://host/NavEntitySet1('NavEntity1')"));
            entries[1].Id.Should().Be(new Uri("http://host/EntitySet('TopEntity')"));
            entries[1].TypeName.Should().Be("NS.DerivedEntityType");
        }
        #endregion

        #region Uri parser

        [Fact]
        public void ParseDerivedBinding()
        {
            Uri uri = new Uri(@"http://host/EntitySet/NS.DerivedEntityType('abc')/Nav");
            var path = new ODataUriParser(GetDerivedModel(), ServiceRoot, uri).ParsePath().ToList();
            path[3].TargetEdmNavigationSource.Name.Should().Be("NavEntitySet");

            uri = new Uri(@"http://host/EntitySet('abc')/NS.DerivedEntityType/Nav");
            path = new ODataUriParser(GetDerivedModel(), ServiceRoot, uri).ParsePath().ToList();
            path[3].TargetEdmNavigationSource.Name.Should().Be("NavEntitySet");
        }

        #endregion

        #region Help Method
        private static string WriteJsonLightEntry(IEdmModel model, IEdmEntitySet entitySet, Action<ODataWriter> writeAction, bool isFullMetadata = false)
        {
            var stream = new MemoryStream();
            var message = new InMemoryMessage { Stream = stream };

            var settings = new ODataMessageWriterSettings { Version = ODataVersion.V4, AutoComputePayloadMetadata = true };
            var odataUri = new ODataUri { ServiceRoot = ServiceRoot };
            odataUri.Path = new ODataUriParser(model, ServiceRoot, new Uri(ServiceRoot + "/EntitySet")).ParsePath();
            settings.ODataUri = odataUri;
            settings.SetServiceDocumentUri(ServiceRoot);

            settings.SetContentType(ODataFormat.Json);
            if (isFullMetadata)
            {
                settings.SetContentType("application/json;odata.metadata=full", null);
            }
            else
            {
                settings.SetContentType("application/json;odata.metadata=minimal", null);
            }

            var messageWriter = new ODataMessageWriter((IODataResponseMessage)message, settings, model);
            ODataWriter writer = null;
            writer = messageWriter.CreateODataResourceWriter(entitySet);

            if (writeAction != null)
            {
                writeAction(writer);
            }

            writer.Flush();

            var actual = Encoding.UTF8.GetString(stream.ToArray());
            return actual;
        }

        private List<ODataResource> ReadPayload(string payload, IEdmModel model, IEdmNavigationSource entitySet, IEdmStructuredType entityType, bool isFullMetadata = false)
        {
            InMemoryMessage message = new InMemoryMessage();
            if (isFullMetadata)
            {
                message.SetHeader("Content-Type", "application/json;odata.metadata=full");
            }
            else
            {
                message.SetHeader("Content-Type", "application/json;odata.metadata=minimal");
            }
            message.Stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            List<ODataResource> entryList = new List<ODataResource>();

            using (var messageReader = new ODataMessageReader((IODataResponseMessage)message, null, model))
            {
                var reader = messageReader.CreateODataResourceReader(entitySet, entityType);
                while (reader.Read())
                {
                    switch (reader.State)
                    {
                        case ODataReaderState.ResourceEnd:
                            entryList.Add((ODataResource)reader.Item);
                            break;
                    }
                }
            }

            return entryList;
        }

        private static IEdmModel GetModel()
        {
            var model = new EdmModel();

            var entityType = new EdmEntityType("NS", "EntityType");
            var id = entityType.AddStructuralProperty("ID", EdmCoreModel.Instance.GetString(false));
            entityType.AddKeys(id);

            var derivedEntityType = new EdmEntityType("NS", "DerivedEntityType", entityType);

            var containedEntityType = new EdmEntityType("NS", "ContainedEntityType");
            var containedId = containedEntityType.AddStructuralProperty("ID", EdmCoreModel.Instance.GetString(false));
            containedEntityType.AddKeys(containedId);

            var containedNav1 = entityType.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = "ContainedNav1",
                Target = containedEntityType,
                TargetMultiplicity = EdmMultiplicity.One,
                ContainsTarget = true
            });

            var containedNav2 = entityType.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = "ContainedNav2",
                Target = containedEntityType,
                TargetMultiplicity = EdmMultiplicity.One,
                ContainsTarget = true
            });

            var navEntityType = new EdmEntityType("NS", "NavEntityType");
            var navEntityId = navEntityType.AddStructuralProperty("ID", EdmCoreModel.Instance.GetString(false));
            navEntityType.AddKeys(navEntityId);

            var complex = new EdmComplexType("NS", "ComplexType");
            complex.AddStructuralProperty("Prop1", EdmCoreModel.Instance.GetString(false));

            var derivedComplex = new EdmComplexType("NS", "DerivedComplexType", complex);
            derivedComplex.AddStructuralProperty("DerivedProp", EdmCoreModel.Instance.GetString(false));

            var derivedNav = derivedEntityType.AddUnidirectionalNavigation(
                new EdmNavigationPropertyInfo()
                {
                    Name = "NavOnDerived",
                    Target = navEntityType,
                    TargetMultiplicity = EdmMultiplicity.One,
                });

            var complxNavP = complex.AddUnidirectionalNavigation(
                new EdmNavigationPropertyInfo()
                {
                    Name = "CollectionOfNavOnComplex",
                    Target = navEntityType,
                    TargetMultiplicity = EdmMultiplicity.Many,
                });

            entityType.AddStructuralProperty("complexProp1", new EdmComplexTypeReference(complex, false));
            entityType.AddStructuralProperty("complexProp2", new EdmComplexTypeReference(complex, false));

            var navOnContained = containedEntityType.AddUnidirectionalNavigation(
                new EdmNavigationPropertyInfo()
                {
                    Name = "NavOnContained",
                    Target = navEntityType,
                    TargetMultiplicity = EdmMultiplicity.One,
                });

            model.AddElement(entityType);
            model.AddElement(derivedEntityType);
            model.AddElement(containedEntityType);
            model.AddElement(navEntityType);
            model.AddElement(complex);
            model.AddElement(derivedComplex);

            var entityContainer = new EdmEntityContainer("NS", "Container");
            model.AddElement(entityContainer);
            var entitySet = new EdmEntitySet(entityContainer, "EntitySet", entityType);
            var navEntitySet1 = new EdmEntitySet(entityContainer, "NavEntitySet1", navEntityType);
            var navEntitySet2 = new EdmEntitySet(entityContainer, "NavEntitySet2", navEntityType);
            entitySet.AddNavigationTarget(derivedNav, navEntitySet1, new EdmPathExpression("NS.DerivedEntityType/NavOnDerived"));
            entitySet.AddNavigationTarget(complxNavP, navEntitySet1, new EdmPathExpression("complexProp1/CollectionOfNavOnComplex"));
            entitySet.AddNavigationTarget(complxNavP, navEntitySet2, new EdmPathExpression("complexProp2/CollectionOfNavOnComplex"));
            entitySet.AddNavigationTarget(navOnContained, navEntitySet1, new EdmPathExpression("ContainedNav1/NavOnContained"));
            entitySet.AddNavigationTarget(navOnContained, navEntitySet2, new EdmPathExpression("ContainedNav2/NavOnContained"));
            entityContainer.AddElement(entitySet);
            entityContainer.AddElement(navEntitySet1);
            entityContainer.AddElement(navEntitySet2);

            return model;
        }

        private static IEdmModel GetDerivedModel()
        {
            var model = new EdmModel();

            var entityType = new EdmEntityType("NS", "EntityType");
            var id = entityType.AddStructuralProperty("ID", EdmCoreModel.Instance.GetString(false));
            entityType.AddKeys(id);

            var derivedEntityType = new EdmEntityType("NS", "DerivedEntityType", entityType);

            var navEntityType = new EdmEntityType("NS", "NavEntityType");
            var navEntityId = navEntityType.AddStructuralProperty("ID", EdmCoreModel.Instance.GetString(false));
            navEntityType.AddKeys(navEntityId);

            var nav = derivedEntityType.AddUnidirectionalNavigation(new EdmNavigationPropertyInfo()
            {
                Name = "Nav",
                Target = navEntityType,
                TargetMultiplicity = EdmMultiplicity.Many,
            });

            model.AddElement(entityType);
            model.AddElement(derivedEntityType);
            model.AddElement(navEntityType);

            var entityContainer = new EdmEntityContainer("NS", "Container");
            model.AddElement(entityContainer);
            var entitySet = new EdmEntitySet(entityContainer, "EntitySet", entityType);
            var navEntitySet = new EdmEntitySet(entityContainer, "NavEntitySet", navEntityType);

            entitySet.AddNavigationTarget(nav, navEntitySet);

            entityContainer.AddElement(entitySet);
            entityContainer.AddElement(navEntitySet);

            return model;
        }

        #endregion
    }
}
