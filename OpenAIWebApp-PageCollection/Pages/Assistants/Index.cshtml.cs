using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Distributed;
using OpenAI;
using OpenAI.Assistants;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text;
using System.Text.Json;

namespace OpenAIWebApp.Pages.Assistants;

#pragma warning disable OPENAI001 // Assistants type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class IndexModel : PageModel
{
    internal int DefaultAssistantCount = 20;

    private readonly IDistributedCache _cache;

    public bool IsFirstPage { get; set; }

    public bool HasNextPage { get; set; }

    public IReadOnlyList<Assistant> Assistants { get; set; } = default!;

    public IndexModel(IDistributedCache cache)
    {
        _cache = cache;
    }

    public void OnGet(int? size = default,
        string? order = default,
        string? pageToRender = default)
    {
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
            throw new InvalidOperationException("No API key.");

        // Note: we would get this from the DI container in a real implementation.
        OpenAIClient oaClient = new(apiKey);
        AssistantClient assistantClient = oaClient.GetAssistantClient();

        bool cacheChanged = false;

        int pageSize = GetPageSize(size, out bool changedCachedValue);
        cacheChanged = cacheChanged || changedCachedValue;

        AssistantCollectionOrder? collectionOrder = GetOrder(order, out changedCachedValue);
        cacheChanged = cacheChanged || changedCachedValue;

        if (cacheChanged)
        {
            ClearCachedTokens();
        }

        BinaryData? pageToken = GetPageToken(pageToRender);

        CollectionResult<Assistant> assistants;

        if (pageToken is null)
        {
            // We don't have a token cached for the page of results to render.
            // Request a new collection from user inputs.
            assistants = assistantClient.GetAssistants(new AssistantCollectionOptions()
            {
                Order = collectionOrder,
                PageSizeLimit = pageSize
            });
        }
        else
        {
            // We have a serialized page token that was cached when a prior
            // web app page was rendered.
            // Rehydrate the page collection from the cached page token.
            assistants = assistantClient.GetAssistants(ContinuationToken.FromBytes(pageToken));
        }

        // Get the current page of results.
        // TODO: Validate that this only makes a single request.
        ClientResult pageResult = assistants.GetRawPages().First();

        // Set the values for the web app page to render.
        // TODO: Set values to render on page from pageResult.

        // First, do it the hard way
        Assistants = GetAssistants(pageResult);

        // Cache the next page token to enable a hyperlink to the next page,
        // or clear it if the current page of results was the last page
        ContinuationToken? nextPageToken = assistants.GetContinuationToken(pageResult);
        if (nextPageToken is null)
        {
            _cache.Remove("NextPageToken");

            HasNextPage = false;
        }
        else
        {
            CacheBytes("NextPageToken", nextPageToken.ToBytes());

            HasNextPage = true;
        }
    }

    private static IReadOnlyList<Assistant> GetAssistants(ClientResult page)
    {
        PipelineResponse response = page.GetRawResponse();
        using JsonDocument doc = JsonDocument.Parse(response.Content);
        IEnumerable<JsonElement> els = doc.RootElement.GetProperty("data").EnumerateArray();

        List<Assistant> assistants = [];
        foreach (JsonElement el in els)
        {
            // TODO: improve perf
            BinaryData json = BinaryData.FromString(el.GetRawText());
            Assistant assistant = ModelReaderWriter.Read<Assistant>(json)!;
            assistants.Add(assistant);
        }

        return assistants.AsReadOnly();
    }

    private BinaryData? GetPageToken(string? pageToRender)
    {
        if (pageToRender == "first")
        {
            return null;
        }

        if (pageToRender == "next")
        {
            if (!TryGetCachedBytes("NextPageToken", out BinaryData value))
            {
                throw new InvalidOperationException("Continuation token was not found in cache");
            }

            return value;
        }

        // No specific page requested -- get the first one
        return null;
    }

    private void ClearCachedTokens()
    {
        _cache.Remove("NextPageToken");
    }

    private int GetPageSize(int? size, out bool changedCachedValue)
    {
        if (size != null)
        {
            CacheInt("PageSize", size.Value);
            changedCachedValue = true;
            return size.Value;
        }

        if (TryGetCachedInt("PageSize", out int value))
        {
            changedCachedValue = false;
            return value;
        }

        changedCachedValue = false;
        return DefaultAssistantCount;
    }

    private AssistantCollectionOrder? GetOrder(string? order, out bool changedCachedValue)
    {
        if (order != null)
        {
            CacheString("Order", order);
            changedCachedValue = true;
            return order;
        }

        if (TryGetCachedString("Order", out string value))
        {
            changedCachedValue = false;
            return value;
        }

        changedCachedValue = false;
        return null;
    }
   
    private int CacheInt(string key, int value)
    {
        byte[] encoded = [checked((byte)value)];
        _cache.Set(key, encoded);
        return value;
    }

    private bool TryGetCachedInt(string key, out int value)
    {
        byte[]? encoded = _cache.Get(key);
        if (encoded == null)
        {
            value = default;
            return false;
        }

        value = encoded[0];
        return true;
    }

    private string CacheString(string key, string value)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(value);
        _cache.Set(key, encoded);
        return value;
    }

    private bool TryGetCachedString(string key, out string value)
    {
        byte[]? encoded = _cache.Get(key);
        if (encoded is null)  
        {
            value = default!;
            return false;
        }

        value = Encoding.UTF8.GetString(encoded);
        return true;
    }

    private byte[] CacheBytes(string key, BinaryData value)
    {
        byte[] bytes = value.ToArray();
        _cache.Set(key, bytes);
        return bytes;
    }

    private bool TryGetCachedBytes(string key, out BinaryData value)
    {
        byte[]? bytes = _cache.Get(key);
        if (bytes is null)
        {
            value = default!;
            return false;
        }

        value = BinaryData.FromBytes(bytes);
        return true;
    }
}
#pragma warning restore OPENAI001 // Assistatns type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.