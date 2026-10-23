using System.Linq;
using NUnit.Framework;

namespace UniRes.Tests
{
    public class ResourceRegistryTests
    {
        [Test]
        public void FindsClassByItsName()
        {
            Assert.AreEqual(typeof(SampleResource), ResourceRegistry.FindType("SampleResource"));
        }

        [Test]
        public void UsesResourceTypeAttributeNameInsteadOfClassName()
        {
            Assert.AreEqual("Custom_Name", ResourceRegistry.GetTypeName(typeof(NamedResource)));
            Assert.AreEqual(typeof(NamedResource), ResourceRegistry.FindType("Custom_Name"));
            Assert.Throws<UniResException>(() => ResourceRegistry.FindType("NamedResource"));
        }

        [Test]
        public void UnknownTypeNameThrows()
        {
            var ex = Assert.Throws<UniResException>(() => ResourceRegistry.FindType("DoesNotExist"));
            StringAssert.Contains("DoesNotExist", ex.Message);
        }

        [Test]
        public void CreatesAnEmptyInstance()
        {
            Resource created = ResourceRegistry.Create("SampleResource");
            Assert.IsInstanceOf<SampleResource>(created);
            Assert.IsTrue(created.IsBuiltIn);
        }

        [Test]
        public void CreatesAClassWithANonPublicConstructor()
        {
            Resource created = ResourceRegistry.Create(typeof(PrivateConstructorResource));
            Assert.IsInstanceOf<PrivateConstructorResource>(created);
        }

        [Test]
        public void ClassWithoutParameterlessConstructorGivesAClearError()
        {
            var ex = Assert.Throws<UniResException>(() => ResourceRegistry.Create(typeof(NoDefaultConstructorResource)));
            StringAssert.Contains("parameterless constructor", ex.Message);
        }

        [Test]
        public void AbstractClassCannotBeRegisteredOrCreated()
        {
            Assert.Throws<UniResException>(() => ResourceRegistry.Register(typeof(AbstractResource)));
            Assert.Throws<UniResException>(() => ResourceRegistry.Create(typeof(AbstractResource)));
        }

        [Test]
        public void NonResourceTypeCannotBeRegistered()
        {
            Assert.Throws<UniResException>(() => ResourceRegistry.Register(typeof(string)));
        }

        [Test]
        public void TwoClassesWithTheSameTypeNameAreReportedAsAmbiguous()
        {
            ResourceRegistry.RegisterAssembly(typeof(DuplicateA).Assembly);

            var ex = Assert.Throws<UniResException>(() => ResourceRegistry.FindType("Duplicate"));
            StringAssert.Contains("Duplicate", ex.Message);
        }

        private class PrivateConstructorResource : Resource
        {
            private PrivateConstructorResource()
            {
            }
        }
    }

    public class ExportedMemberTests
    {
        [Test]
        public void ListsOnlyMarkedMembersBaseClassFirst()
        {
            string[] names = ExportedMember.For(typeof(ChildResource)).Select(m => m.Name).ToArray();

            CollectionAssert.AreEqual(new[] { "ResourceName", "Health", "secret", "renamed", "Flag" }, names);
        }

        [Test]
        public void ReadsAndWritesFieldsPrivateFieldsAndProperties()
        {
            var target = new SampleResource { Health = 5, Speed = 2.5f };
            target.SetSecret("abc");
            ExportedMember[] members = ExportedMember.For(typeof(SampleResource));

            ExportedMember health = members.Single(m => m.Name == "Health");
            ExportedMember secret = members.Single(m => m.Name == "secret");
            ExportedMember speed = members.Single(m => m.Name == "renamed");

            Assert.AreEqual(5, health.GetValue(target));
            Assert.AreEqual("abc", secret.GetValue(target));
            Assert.AreEqual(2.5f, speed.GetValue(target));

            health.SetValue(target, 9);
            secret.SetValue(target, "xyz");
            speed.SetValue(target, 4f);

            Assert.AreEqual(9, target.Health);
            Assert.AreEqual("xyz", target.Secret);
            Assert.AreEqual(4f, target.Speed);
        }

        [Test]
        public void ExposesTheDeclaredValueType()
        {
            ExportedMember health = ExportedMember.For(typeof(SampleResource)).Single(m => m.Name == "Health");
            Assert.AreEqual(typeof(int), health.ValueType);
        }

        [Test]
        public void ReadOnlyPropertyIsRejected()
        {
            Assert.Throws<UniResException>(() => ExportedMember.For(typeof(ReadOnlyExportResource)));
        }

        [Test]
        public void InvalidExportNameIsRejected()
        {
            Assert.Throws<UniResException>(() => ExportedMember.For(typeof(BadNameResource)));
        }

        [Test]
        public void DuplicateExportNameIsRejected()
        {
            Assert.Throws<UniResException>(() => ExportedMember.For(typeof(DuplicateExportResource)));
        }
    }
}
