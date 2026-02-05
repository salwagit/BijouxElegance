using System.Threading.Tasks;

namespace BijouxElegance.Services
{
    public class MockEmbeddingsService
    {
        private readonly Random _random = new();

        public async Task<float[]> CreateMockEmbeddingAsync(string text)
        {
            // dimension example 1536
            var dim = 1536;
            var embedding = new float[dim];
            for (int i = 0; i < dim; i++)
            {
                embedding[i] = (float)(_random.NextDouble() * 2 - 1);
            }
            await Task.Delay(50);
            return embedding;
        }
    }
}
