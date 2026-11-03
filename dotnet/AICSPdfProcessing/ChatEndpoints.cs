using ChromaDB.Client;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace AICSPdfProcessing;

public static class ChatEndpoints
{
    private const string CollectionName = "climate_documents";
    private static readonly ChromaConfigurationOptions ChromaOptions =
        new(uri: "http://localhost:8000/api/v1/");

    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/ask", async ([FromBody] AskRequest request, [FromServices] IConfiguration config) =>
        {
            var apiKey = config["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                return Results.Problem("OPENAI_API_KEY is not configured.", statusCode: 500);
            }

            List<string> retrievedDocuments = [];
            List<object> sources = [];
            try
            {
                var embeddingClient = new EmbeddingClient("text-embedding-3-small", apiKey);
                var questionEmbedding = await embeddingClient.GenerateEmbeddingAsync(request.Prompt);
                var vector = questionEmbedding.Value.ToFloats();

                using var httpClient = new HttpClient();
                var chromaClient = new ChromaClient(ChromaOptions, httpClient);
                var collection = await chromaClient.GetCollection(CollectionName);
                var collectionClient = new ChromaCollectionClient(collection, ChromaOptions, httpClient);

                var queryData = await collectionClient.Query(
                    queryEmbeddings: [vector],
                    nResults: request.TopK,
                    include: ChromaQueryInclude.Documents | ChromaQueryInclude.Metadatas);

                foreach (var item in queryData)
                {
                    foreach (var entry in item)
                    {
                        if (!string.IsNullOrWhiteSpace(entry.Document))
                        {
                            retrievedDocuments.Add(entry.Document);
                        }

                        sources.Add(new
                        {
                            id = entry.Id,
                            distance = entry.Distance,
                            metadata = entry.Metadata,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return Results.Problem($"Unable to retrieve context: {ex.Message}", statusCode: 500);
            }

            if (retrievedDocuments.Count == 0)
            {
                return Results.NotFound(new { detail = "No document context found. Run /ingest first." });
            }

            var context = string.Join("\n\n---\n\n", retrievedDocuments);
            var chatClient = new ChatClient("gpt-4o-mini", apiKey);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "Answer the user's prompt using only the supplied document context. " +
                    "If the context does not contain the answer, say that clearly instead of guessing."),
                new UserChatMessage($"Document context:\n{context}\n\nUser prompt:\n{request.Prompt}"),
            };

            var completion = await chatClient.CompleteChatAsync(messages);
            var answerText = completion.Value.Content[0].Text;

            return Results.Ok(new
            {
                prompt = request.Prompt,
                answer = answerText,
                sources,
            });
        });
    }
}
