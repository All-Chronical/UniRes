using System.Collections.Generic;
using UnityEngine;

namespace UniRes.Tests
{
    public class TestAct : Resource
    {
        [Export] public float[] Samples = new float[0];
        [Export] public Vector3 Origin;
        [Export] public int Count;
    }

    public class TestTake : Resource
    {
        [Export] public string Title = "";
        [Export] public List<TestAct> Acts = new List<TestAct>();
        [Export] public Dictionary<string, TestAct> ByName = new Dictionary<string, TestAct>();
        [Export] public TestAct Featured;
        [Export] public List<Resource> Anything = new List<Resource>();
    }

    public class TestNode : Resource
    {
        [Export] public int Depth;
        [Export] public TestNode Child;
    }

    public class ThrowingSetterResource : Resource
    {
        private int value;

        [Export]
        public int Value
        {
            get => value;
            set
            {
                if (value < 0)
                    throw new System.ArgumentException("Value cannot be negative.");
                this.value = value;
            }
        }
    }
}
