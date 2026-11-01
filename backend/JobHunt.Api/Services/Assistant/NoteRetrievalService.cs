using System.Text.RegularExpressions;
using JobHunt.Api.DataAccess;
using JobHunt.Api.DataAccess.Results;
using JobHunt.Api.Models.Responses.Assistant;

namespace JobHunt.Api.Services.Assistant;

public class NoteRetrievalService(AssistantKnowledgeDataAccess knowledge)
{
    private const int ChunkSize = 1000;
    private const int Overlap = 150;
    private static readonly HashSet<string> StopWords = new(
        "a an the and or of to in on for from with about my me i we you what which how should can could would do did does have has is are was were it this that these those please tell show based saved application applications interview interviews notes feedback preparation".Split(' '));

    public async Task<AssistantRetrievalResponse> SearchAsync(string query, CancellationToken cancellationToken,
        string sourceType = "all")
    {
        var documents = await knowledge.GetDocumentsAsync(cancellationToken);
        var kind = sourceType switch
        {
            "application_notes" => "Application notes",
            "interview_feedback" => "Interview feedback",
            "interview_preparation" => "Interview preparation",
            _ => null
        };
        return Rank(query, kind is null ? documents : documents.Where(document => document.Kind == kind).ToList());
    }

    // A small-account lexical RAG baseline: overlapping chunks and BM25 ranking.
    // No embeddings, extra hosted service, or persistent cross-user index is used.
    public static AssistantRetrievalResponse Rank(string query, IReadOnlyList<AssistantKnowledgeDocument> documents)
    {
        var chunks = documents.SelectMany(document => Chunk(document.Text)
            .Select((text, index) => new SearchChunk(document, index + 1, text,
                Tokenize(document.Title + " " + document.Kind + " " + text))))
            .ToList();
        var terms = Tokenize(query).Keys.Take(40).ToArray();
        if (chunks.Count == 0 || terms.Length == 0) return new(query, documents.Count, chunks.Count, []);
        var averageLength = Math.Max(1, chunks.Average(chunk => chunk.Terms.Values.Sum()));
        var frequencies = terms.ToDictionary(term => term, term => chunks.Count(chunk => chunk.Terms.ContainsKey(term)));
        var ranked = chunks.Select(chunk =>
        {
            double score = 0;
            var length = chunk.Terms.Values.Sum();
            foreach (var term in terms)
            {
                if (!chunk.Terms.TryGetValue(term, out var count)) continue;
                var idf = Math.Log(1 + (chunks.Count - frequencies[term] + 0.5) / (frequencies[term] + 0.5));
                score += idf * count * 2.2 / (count + 1.2 * (0.25 + 0.75 * length / averageLength));
            }
            return new { Chunk = chunk, Score = score };
        }).Where(result => result.Score > 0).OrderByDescending(result => result.Score)
            .ThenBy(result => result.Chunk.Document.DocumentId, StringComparer.Ordinal)
            .ThenBy(result => result.Chunk.Number);
        // At most two chunks from one field so long notes cannot crowd out every other source.
        var perDocument = new Dictionary<string, int>();
        var sources = new List<AssistantSourceResponse>();
        foreach (var result in ranked)
        {
            var chunk = result.Chunk;
            var count = perDocument.GetValueOrDefault(chunk.Document.DocumentId);
            if (count >= 2) continue;
            perDocument[chunk.Document.DocumentId] = count + 1;
            sources.Add(new($"S{sources.Count + 1}", chunk.Document.JobApplicationId, chunk.Document.InterviewId,
                chunk.Document.Title, chunk.Document.Kind, chunk.Number, chunk.Text));
            if (sources.Count == 5) break;
        }
        return new(query, documents.Count, chunks.Count, sources);
    }

    public static IReadOnlyList<string> Chunk(string text)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;
        text = text.Trim();
        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + ChunkSize, text.Length);
            // Prefer a word boundary while preserving an overlap between chunks.
            if (end < text.Length)
            {
                var boundary = text.LastIndexOf(' ', end - 1, end - start);
                if (boundary > start + ChunkSize / 2) end = boundary;
            }
            chunks.Add(text[start..end].Trim());
            if (end == text.Length) break;
            start = end - Overlap;
        }
        return chunks;
    }

    private static Dictionary<string, int> Tokenize(string text)
    {
        var normalized = text.ToLowerInvariant().Replace(".net", " dotnet ").Replace("c#", " csharp ")
            .Replace("c++", " cpp ");
        var tokens = Regex.Matches(normalized, @"[\p{L}\p{Nd}]+", RegexOptions.None, TimeSpan.FromSeconds(1))
            .Select(match => match.Value).Where(term => term.Length > 1 && !StopWords.Contains(term));
        return tokens.GroupBy(term => term).ToDictionary(group => group.Key, group => group.Count());
    }

    private sealed record SearchChunk(AssistantKnowledgeDocument Document, int Number,
        string Text, Dictionary<string, int> Terms);
}
