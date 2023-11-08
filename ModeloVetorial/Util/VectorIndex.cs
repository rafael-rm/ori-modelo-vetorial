namespace ModeloVetorial.Util;

public class VectorIndex
{
    private Dictionary<string, string> _documents = new();
    private Dictionary<string, double> _idf = new(); // IDF por Termo
    private Dictionary<string, Dictionary<string, double>> _tfidf = new(); // TF-IDF do Termo x Documento

    public void BuildIndex()
    {
        var termFrequencyInDocument = new Dictionary<string, Dictionary<string, double>>(); // tabela TF de Termo x Documento
        
        // Calculo TF - Calcula a frequência de um termo dentro de todos documento
        foreach (var doc in _documents)
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
            _idf[term.Key] = Math.Log((double) _documents.Count / term.Value.Count, 10);
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
    }
    
    public Dictionary<string, double> Search(string query)
    {
        var queryTerms = query.Split(' ').Select(term => term.ToLower()).ToArray();

        // Calcular o vetor de consulta TF-IDF
        var queryVector = new Dictionary<string, double>();
        var documentsNorma = new Dictionary<string, double>();
        foreach (var term in queryTerms)
        {
            if (_tfidf.ContainsKey(term))
            {
                queryVector[term] = _idf[term];
                
                foreach (var doc in _tfidf[term])
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
            if (_tfidf.ContainsKey(term))
            {
                var documents = _tfidf[term];
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
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}