namespace UniRes.Tests
{
    // Shared resource classes used by the tests. UniRes finds them automatically.

    public class SampleResource : Resource
    {
        [Export] public int Health;
        [Export] private string secret = "hidden";
        [Export("renamed")] public float Speed { get; set; }
        public int NotExported;

        public string Secret => secret;
        public void SetSecret(string value) => secret = value;
    }

    public class ChildResource : SampleResource
    {
        [Export] public bool Flag;
    }

    [ResourceType("Custom_Name")]
    public class NamedResource : Resource
    {
    }

    public abstract class AbstractResource : Resource
    {
    }

    public class NoDefaultConstructorResource : Resource
    {
        public NoDefaultConstructorResource(int unused)
        {
        }
    }

    [ResourceType("Duplicate")]
    public class DuplicateA : Resource
    {
    }

    [ResourceType("Duplicate")]
    public class DuplicateB : Resource
    {
    }

    public class ReadOnlyExportResource : Resource
    {
        [Export] public int Value => 1;
    }

    public class BadNameResource : Resource
    {
        [Export("not valid")] public int Value;
    }

    public class DuplicateExportResource : Resource
    {
        [Export("same")] public int A;
        [Export("same")] public int B;
    }
}
