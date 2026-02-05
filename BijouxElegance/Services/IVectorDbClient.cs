using System.Collections.Generic;
using System.Threading.Tasks;

namespace BijouxElegance.Services
{
    public class VectorMatch
    {
        public string Id { get; set; } = string.Empty;
        public float Score { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public interface IVectorDbClient
    {
        Task UpsertAsync(string indexName, string id, float[] vector, Dictionary<string, object>? metadata = null);
        Task<List<VectorMatch>> QueryAsync(string indexName, float[] vector, int topK = 10);
    }
}
