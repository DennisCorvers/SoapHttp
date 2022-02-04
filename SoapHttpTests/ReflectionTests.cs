using NUnit.Framework;
using System.Runtime.Serialization;
using SoapHttp.Reflection;
using System.Reflection;
using System;

namespace SoapHttpTests
{
    public class ReflectionTests
    {
        [Test]
        public void TestCreateConstructor()
        {
            var ctor = Utility.GetEmptyConstructor(typeof(TestObject));
            var newobj = (TestObject)ctor();

            Assert.NotNull(newobj);
        }

        [Test]
        public void TestRefFieldGetter()
        {
            string text = "123";
            TestObject obj = new TestObject
            {
                Text = text
            };

            var getter = Utility.CreateAnonymousGetter(GetPrivateField<TestObject>("m_text"));
            Assert.AreEqual(text, getter(obj));
        }

        [Test]
        public void TestRefFieldSetter()
        {
            string text = "123";
            TestObject obj = new TestObject();
            var setter = Utility.CreateAnonymousSetter(GetPrivateField<TestObject>("m_text"));
            setter(obj, text);

            Assert.AreEqual(text, obj.Text);
        }

        [Test]
        public void TestValFieldGetter()
        {
            int number = 123;
            TestObject obj = new TestObject
            {
                Number = number
            };

            var getter = Utility.CreateAnonymousGetter(GetPrivateField<TestObject>("m_number"));
            Assert.AreEqual(number, getter(obj));
        }

        [Test]
        public void TestValFieldSetter()
        {
            int number = 123;
            TestObject obj = new TestObject();
            var setter = Utility.CreateAnonymousSetter(GetPrivateField<TestObject>("m_text"));
            setter(obj, number);

            Assert.AreEqual(number, obj.Text);
        }

        [Test]
        public void TestRefPropertyGetter()
        {
            string text = "123";
            TestObject obj = new(123, text);

            var getter = Utility.CreateAnonymousGetter(GetProperty<TestObject>("Text"));
            Assert.AreEqual(text, getter(obj));
        }

        [Test]
        public void TestRefPropertySetter()
        {
            string text = "123";
            TestObject obj = new TestObject();
            var setter = Utility.CreateAnonymousSetter(GetProperty<TestObject>("Text"));
            setter(obj, text);

            Assert.AreEqual(text, obj.Text);
        }

        [Test]
        public void TestValPropertyGetter()
        {
            int number = 123;
            TestObject obj = new(number, "123");

            var getter = Utility.CreateAnonymousGetter(GetProperty<TestObject>("Number"));
            Assert.AreEqual(number, getter(obj));
        }

        [Test]
        public void TestValPropertySetter()
        {
            int number = 123;
            TestObject obj = new TestObject();
            var setter = Utility.CreateAnonymousSetter(GetProperty<TestObject>("Number"));
            setter(obj, number);

            Assert.AreEqual(number, obj.Number);
        }


        private FieldInfo? GetPrivateField<T>(string name)
            => typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        private PropertyInfo? GetProperty<T>(string name)
            => typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public);

        private class TestObject
        {
            public int Number { get => m_number; set => m_number = value; }
            public string? Text { get => m_text; set => m_text = value; }

            private int m_number;
            private string? m_text;

            public TestObject() { }

            public TestObject(int number, string text)
            {
                m_number = number;
                m_text = text;
            }
        }
    }
}