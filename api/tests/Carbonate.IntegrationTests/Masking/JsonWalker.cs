using System.Text.Json;

namespace Carbonate.IntegrationTests.Masking;

//reads the raw json tree, so a key the api sent as null still counts as sent
public static class JsonWalker
{
    public static IEnumerable<string> PropertyNames(JsonElement element) => Objects(element)
        .SelectMany(item => item.EnumerateObject().Select(property => property.Name));

    //every object in the tree, nested ones included
    public static IEnumerable<JsonElement> Objects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
            {
                foreach (var nested in Objects(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var nested in Objects(item))
                {
                    yield return nested;
                }
            }
        }
    }
}
