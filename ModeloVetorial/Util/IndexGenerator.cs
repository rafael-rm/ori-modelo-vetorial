using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StopWord;

namespace ModeloVetorial.Util;

public class InvertedIndex
{
    private Dictionary<string, HashSet<string>>? _index;

    public static Task<Dictionary<string, HashSet<string>>> GenerateIndex(Dictionary<string, string> documents)
    {
        var index = new Dictionary<string, HashSet<string>>();

        // Develop logic

        return Task.FromResult(index);
    }

    public async Task StartIndex()
    {
        if (_index is null)
        {
            var projectDirectory = Directory.GetParent(Environment.CurrentDirectory)?.FullName;

            var pathIndex = projectDirectory + "\\ModeloVetorial\\Data\\index.json";

            if (!File.Exists(pathIndex)) await SaveIndexDisk();

            _index = await ReadIndexFromDisk();
        }
    }

    public static async Task SaveIndexDisk()
    {
        var projectDirectory = Directory.GetParent(Environment.CurrentDirectory)?.FullName;

        var pathFolderHtmls = projectDirectory + "\\ModeloVetorial\\Data\\htmlFiles";
        var pathFolderIndex = projectDirectory + "\\ModeloVetorial\\Data\\";

        var textTreated = await GetTreatedTextFiles(pathFolderHtmls);
        var invertedIndex = await GenerateIndex(textTreated);

        var indexPath = Path.Combine(pathFolderIndex, "index.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(invertedIndex, jsonOptions);

        await File.WriteAllTextAsync(indexPath, json);
    }

    public static async Task<Dictionary<string, HashSet<string>>> ReadIndexFromDisk()
    {
        var projectDirectory = Directory.GetParent(Environment.CurrentDirectory)?.FullName;
        var pathFolderIndex = projectDirectory + "\\ModeloVetorial\\Data\\index.json";

        if (!File.Exists(pathFolderIndex)) await SaveIndexDisk();

        var json = await File.ReadAllTextAsync(pathFolderIndex);

        var index = JsonSerializer.Deserialize<Dictionary<string, HashSet<string>>>(json);

        return index;
    }

    public async Task<List<string>> Search(string query)
    {
        await StartIndex();

        // Develop logic

        return new List<string>();
    }

    public static async Task<Dictionary<string, string>> GetPlainTextFiles(string pathFolder)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var filesHtml = Directory.GetFiles(pathFolder, "*.htm*");

        var filesText = new Dictionary<string, string>();
        foreach (var file in filesHtml)
        {
            var fileName = Path.GetFileName(file);
            var fileText = await File.ReadAllTextAsync(file, Encoding.GetEncoding(1252));
            filesText.TryAdd(fileName, fileText);

            Console.WriteLine($"[INFO] Arquivo {fileName} carregado com sucesso.");
        }

        Console.WriteLine($"[INFO] Total de arquivos carregados: {filesText.Count}");

        return filesText;
    }

    public static async Task<Dictionary<string, string>> GetTreatedTextFiles(string filePath)
    {
        var dictionaryFiles = await GetPlainTextFiles(filePath);

        foreach (var file in dictionaryFiles)
        {
            var text = file.Value;
            text = Regex.Replace(text, "<.*?>", string.Empty);
            text = WebUtility.HtmlDecode(text);
            text = text.Replace("-", " ");
            text = Regex.Replace(text, @"\s+", " ");
            text = text.ToLower();
            text = Regex.Replace(text, @"[:;()\[\].,?!<>]", string.Empty);
            text = Regex.Replace(text, @"[^\p{L}\d\sÀ-ÖØ-öø-ÿ]", string.Empty);
            text = text.Normalize(NormalizationForm.FormD);
            text = new string(text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());

            Console.WriteLine($"[INFO] Texto do arquivo {file.Key} limpo e normalizado com sucesso.");

            text = text.RemoveStopWords("pt");
            text = text.RemoveStopWords("en");

            Console.WriteLine($"[INFO] Removido stop-words do texto do arquivo {file.Key} com sucesso.");

            dictionaryFiles[file.Key] = text;
        }

        Console.WriteLine(
            $"[INFO] Todos os textos dos {dictionaryFiles.Count} foram limpos e normalizados com sucesso.");

        return dictionaryFiles;
    }

    public static Task<Dictionary<string, int>> GetRepetitionCount(string text)
    {
        var wordCount = new Dictionary<string, int>();

        Console.WriteLine("[INFO] Iniciando contagem das repetições de palavras.");

        foreach (var word in text.Split(' '))
        {
            if (string.IsNullOrEmpty(word))
                continue;

            if (wordCount.ContainsKey(word))
            {
                wordCount[word]++;
                Console.WriteLine($"[INFO] Palavra {word} somada no dicionario, contagem atual: {wordCount[word]}");
            }
            else
            {
                wordCount[word] = 1;
                Console.WriteLine($"[INFO] Palavra {word} adicionada ao dicionario, contagem atual: {wordCount[word]}");
            }
        }

        Console.WriteLine("[INFO] Contagem de repetições de palavras finalizada com sucesso.");

        return Task.FromResult(wordCount);
    }

    public static Task<Dictionary<string, int>> OrderRepetition(Dictionary<string, int> dictionary,
        bool descending = true)
    {
        Dictionary<string, int> orderedDictionary;

        orderedDictionary = descending
            ? dictionary.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value)
            : dictionary.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

        Console.WriteLine("[INFO] Dicionario ordenado com sucesso.");

        return Task.FromResult(orderedDictionary);
    }

    public static Task<string> GetAllTextDictonary(Dictionary<string, string> dictionary)
    {
        var allText = string.Empty;

        foreach (var file in dictionary) allText += file.Value;

        Console.WriteLine($"$[INFO] O texto de {dictionary.Count} arquivos foi mesclado com sucesso.");
        Console.WriteLine($"$[INFO] Caracteres totais: {allText.Length}");

        return Task.FromResult(allText);
    }
}