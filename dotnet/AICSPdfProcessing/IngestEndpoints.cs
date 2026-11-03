using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ChromaDB.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenAI.Embeddings;
using UglyToad.PdfPig;

namespace AICSPdfProcessing;

public static class IngestEndpoints
{
    private const string CollectionName = "climate_documents";
    private static readonly ChromaConfigurationOptions ChromaOptions =
        new(uri: "http://localhost:8000/api/v1/");

    public static void MapIngestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ingest", async (
            [FromServices] IConfiguration config,
            [FromServices] IHttpClientFactory httpClientFactory) =>
        {
            try
            {
                var fullText = LoadDocument("Climate.pdf");
                var chunks = await SplitDocumentViaPythonAsync(fullText, config, httpClientFactory);
                if (chunks.Count == 0)
                {
                    return Results.BadRequest(new { error = "Python splitter returned no chunks." });
                }

                var apiKey = config["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return Results.Problem("OPENAI_API_KEY is not configured.", statusCode: 500);
                }

                var embeddingClient = new EmbeddingClient("text-embedding-3-small", apiKey);
                await StoreEmbeddingsInChromaAsync(chunks, embeddingClient);
                return Results.Ok(new
                {
                    message = "Successfully ingested document chunks into vector store",
                    chunkCount = chunks.Count,
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    private static string LoadDocument(string filePath)
    {
        using var pdf = PdfDocument.Open(filePath);
        var pagesText = pdf.GetPages().Select(page => page.Text);
        return string.Join("\n", pagesText);
    }

    private static async Task<List<string>> SplitDocumentViaPythonAsync(
        string text,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient("PythonSplitter");
        using var response = await client.PostAsJsonAsync("/split", new
        {
            text,
            chunk_size = config.GetValue("PythonSplitter:ChunkSize", 1000),
            chunk_overlap = config.GetValue("PythonSplitter:ChunkOverlap", 200),
        });

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Python /split failed ({(int)response.StatusCode}): {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<PythonSplitResponse>();
        return payload?.Chunks ?? [];
    }

    private static async Task StoreEmbeddingsInChromaAsync(List<string> chunks, EmbeddingClient embeddingClient)
    {
        using var httpClient = new HttpClient();
        var chromaClient = new ChromaClient(ChromaOptions, httpClient);
        var collection = await chromaClient.GetOrCreateCollection(CollectionName);
        var collectionClient = new ChromaCollectionClient(collection, ChromaOptions, httpClient);

        var embeddings = new List<ReadOnlyMemory<float>>();
        var ids = new List<string>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var response = await embeddingClient.GenerateEmbeddingAsync(chunks[i]);
            embeddings.Add(response.Value.ToFloats());
            ids.Add(Guid.NewGuid().ToString());
        }

        await collectionClient.Add(ids, embeddings: embeddings, documents: chunks);
    }

    private sealed class PythonSplitResponse
    {
        [JsonPropertyName("chunks")]
        public List<string> Chunks { get; set; } = [];

        [JsonPropertyName("chunk_count")]
        public int ChunkCount { get; set; }
    }
}
