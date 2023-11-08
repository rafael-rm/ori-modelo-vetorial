using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StopWord;

namespace ModeloVetorial.Util;

public class InvertedIndex
{
    private static Dictionary<string, double>? _idf; // IDF por Termo
    private static Dictionary<string, Dictionary<string, double>>? _tfidf; // TF-IDF do Termo x Documento

    public static Task GenerateIndex(Dictionary<string, string> documents)
    {
        _tfidf = new Dictionary<string, Dictionary<string, double>>();
        _idf = new Dictionary<string, double>();

        var termFrequencyInDocument = new Dictionary<string, Dictionary<string, double>>(); // tabela TF de Termo x Documento

        // Calculo TF - Calcula a frequência de um termo dentro de todos documento
        foreach (var doc in documents)
        {
            var freqTermInDoc = new Dictionary<string, int>();
            var terms = doc.Value.Split(' ').Select(term => term.ToLower());

            // Calcula a frequencia do TERMO no DOCUMENTO
            foreach (var term in terms)
            {
                freqTermInDoc[term] = freqTermInDoc.TryGetValue(term, out var freq) ? freq + 1 : 1;
            }

            // Calcula o TF para cada termo do documento
            foreach (var term in freqTermInDoc)
            {
                var calc = term.Value / (double)freqTermInDoc.Values.Max(); // Max TF
                if (termFrequencyInDocument.ContainsKey(term.Key))
                {
                    termFrequencyInDocument[term.Key].Add(doc.Key, calc);
                }
                else
                {
                    termFrequencyInDocument.Add(term.Key, new Dictionary<string, double> { { doc.Key, calc } } );
                }
            }
        }

        // Calculo IDF
        // LOG (n documentos / freq de K na coleção)
        foreach (var term in termFrequencyInDocument)
        {
            // Resultado do logaritmo aparenta estar errado
            _idf[term.Key] = Math.Log((double) documents.Count / term.Value.Count, 10);
        }

        foreach (var termDic in termFrequencyInDocument) // Termo x Freq em cada documento
        {
            var idf = _idf[termDic.Key];
            foreach (var tfDocDic in termDic.Value) // Documento x TF
            {
                var tfidf = tfDocDic.Value * idf;
                if (_tfidf.ContainsKey(termDic.Key))
                {
                    _tfidf[termDic.Key].Add(tfDocDic.Key, tfidf);
                }
                else
                {
                    _tfidf.Add(termDic.Key, new Dictionary<string, double>{ { tfDocDic.Key, tfidf } });
                }
            }
        }

        return Task.CompletedTask;
    }

    public async Task StartIndex()
    {
        if (_idf is null || _tfidf is null)
        {
            var projectDirectory = Directory.GetParent(Environment.CurrentDirectory)?.FullName;

            var pathTfidf = projectDirectory + "\\ModeloVetorial\\Data\\tfidf.json";
            var pathIdf = projectDirectory + "\\ModeloVetorial\\Data\\idf.json";

            if (!File.Exists(pathTfidf) || !File.Exists(pathIdf))
                await SaveIndexDisk();
            else
                await ReadIndexFromDisk();
        }
    }

    public static async Task SaveIndexDisk()
    {
        var projectDirectory = Directory.GetParent(Environment.CurrentDirectory)?.FullName;

        var pathFolderHtmls = projectDirectory + "\\ModeloVetorial\\Data\\htmlFiles";
        var pathFolderIndex = projectDirectory + "\\ModeloVetorial\\Data\\";

        var textTreated = await GetTreatedTextFiles(pathFolderHtmls);
        await GenerateIndex(textTreated);

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        var tfidfPath = Path.Combine(pathFolderIndex, "tfidf.json");
        var json = JsonSerializer.Serialize(_tfidf, jsonOptions);
        await File.WriteAllTextAsync(tfidfPath, json);

        var idfPath = Path.Combine(pathFolderIndex, "idf.json");
        json = JsonSerializer.Serialize(_idf, jsonOptions);
        await File.WriteAllTextAsync(idfPath, json);
    }

    public static async Task ReadIndexFromDisk()
    {
        var projectDirectory = Directory.GetParent(Environment.CurrentDirectory)?.FullName;
        var pathFoldertfidf = projectDirectory + "\\ModeloVetorial\\Data\\tfidf.json";
        var pathFolderidf = projectDirectory + "\\ModeloVetorial\\Data\\idf.json";

        if (!File.Exists(pathFoldertfidf) || !File.Exists(pathFoldertfidf)) await SaveIndexDisk();

        var tfidfJson = await File.ReadAllTextAsync(pathFoldertfidf);
        var idfJson = await File.ReadAllTextAsync(pathFolderidf);

        _tfidf = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(tfidfJson);
        _idf = JsonSerializer.Deserialize<Dictionary<string, double>>(idfJson);
    }

    public async Task<Dictionary<string, double>> Search(string query)
    {
        await StartIndex();

        if (_tfidf is null || _idf is null)
            throw new Exception("Um erro ocorreu durante a geração do index, o mesmo se encontra como NULL");

        var queryTerms = query.Split(' ').Select(term => term.ToLower()).ToArray();

        // Calcular o vetor de consulta TF-IDF
        var queryVector = new Dictionary<string, double>();
        var documentsNorma = new Dictionary<string, double>();
        foreach (var term in queryTerms)
        {
            if (_tfidf.TryGetValue(term, out var value))
            {
                queryVector[term] = _idf[term];

                foreach (var doc in value)
                {
                    var calc = doc.Value * doc.Value;
                    if (documentsNorma.ContainsKey(doc.Key))
                    {
                        documentsNorma[doc.Key] += calc;
                    }
                    else
                    {
                        documentsNorma.Add(doc.Key, calc);
                    }
                }
            }
        }

        foreach (var doc in documentsNorma)
        {
            documentsNorma[doc.Key] = Math.Sqrt(doc.Value);
        }

        var queryNorma = Math.Sqrt(queryVector.Sum(q => q.Value * q.Value));

        var dic = new Dictionary<string, double>();
        foreach (var term in queryTerms)
        {
            if (_tfidf.TryGetValue(term, out var documents))
            {
                foreach (var doc in documents)
                {
                    if (dic.ContainsKey(doc.Key))
                    {
                        dic[doc.Key] += doc.Value * queryVector[term];
                    }
                    else
                    {
                        dic[doc.Key] = doc.Value * queryVector[term];
                    }
                }
            }
        }

        var ranking = new Dictionary<string, double>();
        foreach (var d in dic)
        {
            ranking[d.Key] = d.Value / (queryNorma * documentsNorma[d.Key]);
        }

        return ranking.OrderByDescending(s => s.Value)
            .ToDictionary(kvp => kvp.Key, kvp => Math.Round(kvp.Value, 2));
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