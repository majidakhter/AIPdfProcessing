using ChromaDB.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OpenAI.Embeddings;
using UglyToad.PdfPig;

namespace AICSPdfProcessing;

public static class IngestEndpointsOriginal
{
    private const string CollectionName = "climate_documents";
    private static readonly ChromaConfigurationOptions ChromaOptions =
        new(uri: "http://localhost:8000/api/v1/");

    public static void MapIngestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ingest", async ([FromServices] IConfiguration config) =>
        {
            try
            {
                var fullText = LoadDocument("Climate.pdf");
                var chunks = SplitDocument(fullText);
                var apiKey = config["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return Results.Problem("OPENAI_API_KEY is not configured.", statusCode: 500);
                }

                var embeddingClient = new EmbeddingClient("text-embedding-3-small", apiKey);
                await StoreEmbeddingsInChromaAsync(chunks, embeddingClient);
                return Results.Ok(new { message = "Successfully ingested document chunks into vector store" });
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

    private static List<string> SplitDocument(string text)
    {
        var paragraphs = text.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        const int maxChars = 1000;
        const int overlap = 200;

        for (int i = 0; i < paragraphs.Length;)
        {
            var chunkBuilder = new System.Text.StringBuilder();
            var chars = 0;
            var start = i;
            while (i < paragraphs.Length && chars + paragraphs[i].Length <= maxChars)
            {
                if (chunkBuilder.Length > 0)
                    chunkBuilder.Append("\n\n");
                chunkBuilder.Append(paragraphs[i]);
                chars += paragraphs[i].Length;
                i++;
            }

            if (chunkBuilder.Length == 0 && i < paragraphs.Length)
            {
                chunkBuilder.Append(paragraphs[i][..Math.Min(maxChars, paragraphs[i].Length)]);
                i++;
            }

            chunks.Add(chunkBuilder.ToString());

            if (i < paragraphs.Length && overlap > 0)
            {
                var overlapChars = 0;
                var j = i - 1;
                while (j >= start && overlapChars < overlap)
                {
                    overlapChars += paragraphs[j].Length;
                    j--;
                }
                i = Math.Max(j + 1, start + 1);
            }
        }

        return chunks;
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

        // First argument is the id list (positional). Do not use a named "ids:" argument.
        await collectionClient.Add(ids, embeddings: embeddings, documents: chunks);
    }
}
