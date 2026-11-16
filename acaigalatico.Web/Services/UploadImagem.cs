using Microsoft.AspNetCore.Http;

namespace acaigalatico.Web.Services
{
    /// <summary>
    /// Centraliza o upload de imagens do site (fotos de perfil, produtos e conteúdo).
    /// Aceita só extensões de imagem, limita o tamanho e gera o nome do arquivo no
    /// servidor, ignorando o nome enviado pelo navegador (evita path traversal e
    /// upload de arquivos .html/.js que seriam servidos pelo próprio site).
    /// </summary>
    public static class UploadImagem
    {
        public const long TamanhoMaximoBytes = 5 * 1024 * 1024; // 5 MB

        private static readonly string[] ExtensoesPermitidas = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif" };

        /// <summary>Devolve a mensagem de erro, ou null se a imagem for válida.</summary>
        public static string? Validar(IFormFile? arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return "Nenhuma imagem foi enviada.";

            if (arquivo.Length > TamanhoMaximoBytes)
                return "A imagem deve ter no máximo 5 MB.";

            var extensao = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            if (!ExtensoesPermitidas.Contains(extensao))
                return "Formato não permitido. Envie uma imagem JPG, PNG, WEBP, GIF ou AVIF.";

            return null;
        }

        /// <summary>
        /// Salva a imagem em wwwroot/uploads/{subpasta} e devolve a URL pública.
        /// Lança InvalidOperationException se a imagem for inválida.
        /// </summary>
        public static async Task<string> SalvarAsync(IFormFile arquivo, string webRootPath, string subpasta)
        {
            var erro = Validar(arquivo);
            if (erro != null)
                throw new InvalidOperationException(erro);

            var pasta = Path.Combine(webRootPath, "uploads", subpasta);
            Directory.CreateDirectory(pasta);

            var nomeArquivo = $"{Guid.NewGuid()}{Path.GetExtension(arquivo.FileName).ToLowerInvariant()}";
            var caminho = Path.Combine(pasta, nomeArquivo);

            using (var stream = new FileStream(caminho, FileMode.Create))
            {
                await arquivo.CopyToAsync(stream);
            }

            return $"/uploads/{subpasta}/{nomeArquivo}";
        }
    }
}
