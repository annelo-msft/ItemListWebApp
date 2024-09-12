using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenAI;
using OpenAI.FineTuning;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;

namespace OpenAIWebApp.Pages.FineTuning;

#pragma warning disable OPENAI001 // FineTuning type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public class IndexModel : PageModel
{
    public IReadOnlyList<FineTuningJob> Jobs { get; set; } = default!;

    public class FineTuningJob
    {
        public FineTuningJob(JsonElement jsonElement)
        {
            Id = jsonElement.GetProperty("id").GetString()!;
            Model = jsonElement.GetProperty("model").GetString()!;
            Status = jsonElement.GetProperty("status").GetString()!;
        }

        public string Id { get;  }
        public string Model { get;  }
        public string Status { get;  }
    }

    public void OnGet()
    {
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ??
            throw new InvalidOperationException("No API key.");

        OpenAIClient oaClient = new(apiKey);

        FineTuningClient fineTuningClient = oaClient.GetFineTuningClient();

        IEnumerable<ClientResult> jobPages = fineTuningClient.GetJobs(after: null, limit: 10, new RequestOptions());

        ClientResult jobPage = jobPages.First();

        using JsonDocument doc = JsonDocument.Parse(jobPage.GetRawResponse().Content);
        IEnumerable<JsonElement> jobElements = doc.RootElement.GetProperty("data").EnumerateArray();

        Jobs = jobElements.Select(el => new FineTuningJob(el)).ToList().AsReadOnly();
    }
}
#pragma warning restore OPENAI001 // Assistatns type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.