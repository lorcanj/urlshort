using Microsoft.EntityFrameworkCore;

namespace urlshort.Services
{
    public class UrlShorteningService
    {
        public const int NumberOfCharsInShortLink = 7;
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        private readonly Random _random = new();

        public async Task<string> GenerateUniqueCode(ApplicationDbContext dbContext)
        {
            while (true) 
            {
                var codeChars = new char[NumberOfCharsInShortLink];

                for (var i = 0; i < NumberOfCharsInShortLink; i++)
                {
                    var randomIndex = _random.Next(Alphabet.Length - 1);

                    codeChars[i] = Alphabet[randomIndex];
                }

                var code = new string(codeChars);

                if (!await dbContext.ShortenedUrls.AnyAsync(s => s.Code == code))
                {
                    return code;
                }
            }

        }
    }
}
