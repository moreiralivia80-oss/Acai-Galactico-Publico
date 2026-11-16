using acaiGalatico.UI.Services;

namespace acaiGalatico.UI
{
    internal static class AppServices
    {
        private static readonly Lazy<PedidosApiService> _pedidosApiService = new(CreatePedidosApiService);

        public static PedidosApiService PedidosApiService => _pedidosApiService.Value;

        // Endereço e chave da API vêm de variáveis de ambiente, para não ficarem no código:
        //   ACAI_API_URL  (opcional, padrão http://127.0.0.1:5207/)
        //   ACAI_API_KEY  (obrigatória, igual ao valor de Api:Key configurado na API)
        private static PedidosApiService CreatePedidosApiService()
        {
            var url = Environment.GetEnvironmentVariable("ACAI_API_URL");
            if (string.IsNullOrWhiteSpace(url)) url = "http://127.0.0.1:5207/";
            if (!url.EndsWith("/")) url += "/";

            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(url),
                Timeout = TimeSpan.FromSeconds(30)
            };

            var chaveApi = Environment.GetEnvironmentVariable("ACAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(chaveApi))
            {
                httpClient.DefaultRequestHeaders.Add("X-Api-Key", chaveApi);
            }

            return new PedidosApiService(httpClient);
        }
    }
}
// mudanças