using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace BijouxElegance.Services
{
    public class OllamaResult
    {
        public string response { get; set; } = string.Empty;
    }

    public class OllamaService
    {
        private readonly HttpClient _http;

        public OllamaService(HttpClient http)
        {
            _http = http;
            // BaseAddress is configured in Program.cs when registering the client
        }

        public async Task<string> AskAsync(string prompt)
        {
            var body = new
            {
                model = "llama3.2", // exact model name as requested
                prompt = prompt,
                stream = false
            };

            var response = await _http.PostAsJsonAsync("/api/generate", body);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return "Ollama error: " + error;
            }

            try
            {
                var result = await response.Content.ReadFromJsonAsync<OllamaResult>();
                return result?.response ?? "Aucune réponse du modèle.";
            }
            catch (Exception ex)
            {
                var raw = await response.Content.ReadAsStringAsync();
                return "Ollama parse error: " + ex.Message + " - raw: " + raw;
            }
        }
    }
}
