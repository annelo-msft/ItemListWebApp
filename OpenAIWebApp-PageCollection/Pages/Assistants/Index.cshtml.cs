using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Distributed;
using OpenAI;
using OpenAI.Assistants;
using System.ClientModel;
using System.Text;

namespace OpenAIWebApp.Pages.Assistants;

#pragma warning disable OPENAI001 // Assistants type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class IndexModel : PageModel
{
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

        bool voidCache = false;

        int? cachedSize = GetCachedInt("PageSize");
        if (size is null)
        {
            size = cachedSize;
        }
        else if (size != cachedSize)
        {
            voidCache = true;
            CacheInt("PageSize", size.Value);
        }

        // If order changes, void the cache and reset to the
        // first page -- it's effectively a new collection.
        string? cachedOrder = GetCachedString("Order");
        if (order is null)
        {
            order = cachedOrder;
        }
        else if (order != cachedOrder)
        {
            voidCache = true;
            CacheString("Order", order);
        }

        ListOrder? listOrder = order ?? (ListOrder?)null;
        int? pageSize = size;

        if (voidCache)
        {
            _cache.Remove("FirstPageToken");
            _cache.Remove("PageToken");
            _cache.Remove("NextPageToken");
        }

        BinaryData? cachedPageTokenBytes = pageToRender switch
        {
            "first" => GetCachedBytes("FirstPageToken"),
            "next" => GetCachedBytes("NextPageToken") ?? throw new InvalidOperationException("No next page available."),
            _ => GetCachedBytes("PageToken")
        };

        PageCollection<Assistant> assistantPages;
        
        if (cachedPageTokenBytes is null)
        {
            // We don't have a token cached for the page of results to render.
            // Request a new collection from user inputs.
            assistantPages = assistantClient.GetAssistants(new AssistantCollectionOptions()
            {
                Order = listOrder,
                PageSize = pageSize
            });
        }
        else
        {
            // We have a serialized page token that was cached when a prior
            // web app page was rendered.
            // Rehydrate the page collection from the cached page token.
            assistantPages = assistantClient.GetAssistants(ContinuationToken.FromBytes(cachedPageTokenBytes));
        }

        // Get the current page from the collection.
        PageResult<Assistant> assistantPage = assistantPages.GetCurrentPage();

        // Set the values for the web app page to render.
        Assistants = assistantPage.Values;

        // Cache the serialized page token value to use the next time
        // the web app page is rendered.
        CacheBytes("PageToken", assistantPage.PageToken.ToBytes());

        // Only store the first page token if we don't have one, since if we
        // rehydrated the collection we reset which page is first.
        if (GetCachedBytes("FirstPageToken") is null)
        {
            CacheBytes("FirstPageToken", assistantPage.PageToken.ToBytes());
        }

        // Cache the next page token to enable a hyperlink to the next page,
        // or clear it if the current page of results was the last page
        if (assistantPage.NextPageToken is not null)
        {
            CacheBytes("NextPageToken", assistantPage.NextPageToken.ToBytes());

            HasNextPage = true;
        }
        else
        {
            _cache.Remove("NextPageToken");

            HasNextPage = false;
        }
    }

    private int CacheInt(string key, int value)
    {
        byte[] encoded = [checked((byte)value)];
        _cache.Set(key, encoded);
        return value;
    }

    private int? GetCachedInt(string key)
    {
        byte[]? encoded = _cache.Get(key);
        return encoded?[0];
    }

    private string CacheString(string key, string value)
    {
        byte[] encoded = Encoding.UTF8.GetBytes(value);
        _cache.Set(key, encoded);
        return value;
    }

    private string? GetCachedString(string key)
    {
        byte[]? encoded = _cache.Get(key);
        return encoded is null ? null : Encoding.UTF8.GetString(encoded);
    }

    private byte[] CacheBytes(string key, BinaryData value)
    {
        byte[] bytes = value.ToArray();
        _cache.Set(key, bytes);
        return bytes;
    }

    private BinaryData? GetCachedBytes(string key)
    {
        byte[]? bytes = _cache.Get(key);
        return bytes is null ? null : BinaryData.FromBytes(bytes);
    }
}
#pragma warning restore OPENAI001 // Assistatns type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.