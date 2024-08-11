using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace GMTales.Sync;

public partial class Sync
{
    private readonly HttpClient _client;
    private readonly Settings _settings;

    public Sync(Settings settings)
    {
        if (settings.BaseUrl == null)
            throw new ArgumentException("BaseUrl cannot be missing");

        _settings = settings;
        _client = new HttpClient();
        _client.BaseAddress = new Uri(_settings.BaseUrl);
    }

    public async Task ProcessArticles()
    {
        if (_settings.Folder == null)
            throw new ArgumentException("Folder cannot be missing");
        if (_settings.Username == null)
            throw new ArgumentException("Username cannot be missing");
        if (_settings.Password == null)
            throw new ArgumentException("Password cannot be missing");

        var token = await AuthenticateAndGetToken(_settings.Username, _settings.Password);

        var markdownFiles = GetMarkdownFiles(_settings.Folder);

        foreach (var file in markdownFiles)
        {
            var markdownContent = await File.ReadAllTextAsync(file);

            var imageLinks = ExtractImageLinks(markdownContent);
            foreach (var imageLink in imageLinks)
            {
                var localImagePath = Path.Combine(_settings.Folder, imageLink.TrimStart('/'));

                var imageExists = await CheckImageExists(imageLink, token);
                if (!imageExists)
                {
                    await UploadImage(localImagePath, token);
                }
            }

            var title = GetTitle(markdownContent);
            var articleExists = await CheckArticleExists(title);

            if (articleExists)
            {
                await SendPutAsync($"api/campaign/{_settings.Campaign}/article", title, markdownContent, token);
            }
            else
            {
                await SendPostAsync($"api/campaign/{_settings.Campaign}/article", title, markdownContent, token);
            }
        }
    }

    private async Task<string> AuthenticateAndGetToken(string username, string password)
    {
        var loginInfo = new
        {
            Username = username,
            Password = password
        };
        var response = await _client.PostAsJsonAsync("api/auth/login", loginInfo);
        if (response.IsSuccessStatusCode)
        {
            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse == null)
                throw new Exception("Could not read token from response");

            return tokenResponse.AccessToken;
        }

        throw new Exception("Authentication failed.");
    }

    private async Task SendPutAsync(string url, string title, string content, string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PutAsJsonAsync(url, content);
        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to update article {title}. Response: {responseContent}");
        }
    }

    private async Task SendPostAsync(string url, string title, string content, string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync(url, content);
        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to create article {title}. Response: {responseContent}");
        }
    }

    private async Task UploadImage(string imagePath, string token)
    {
        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var imageName = Path.GetFileName(imagePath);

        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(imageBytes), "image", Path.GetFileName(imageName));
        content.Add(new StringContent(imageName), "fileName");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsync($"api/campaign/{_settings.Campaign}/images", content);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to upload image {imagePath}. Response: {responseContent}");
        }
    }

    private async Task<bool> CheckArticleExists(string title)
    {
        var existingArticle = await _client.GetAsync($"api/campaign/{_settings.Campaign}/article/title?title={title}");
        return existingArticle.IsSuccessStatusCode;
    }

    private async Task<bool> CheckImageExists(string imagePath, string token)
    {
        var imageName = Path.GetFileName(imagePath);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync($"api/campaign/{_settings.Campaign}/images/path/{imageName}");
        return response.IsSuccessStatusCode;
    }

    private static List<string> ExtractImageLinks(string markdownContent)
    {
        var imageLinks = new List<string>();
        var regex = ImageRegex();
        var matches = regex.Matches(markdownContent);

        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                imageLinks.Add(match.Groups[1].Value);
            }
        }

        return imageLinks;
    }

    private static string GetTitle(string content)
    {
        var titleRegex = TitleRegex();
        var titleMatch = titleRegex.Match(content);
        if (!titleMatch.Success)
            throw new Exception("Article does not have title, refusing to continue");
        return titleMatch.Groups[1].Value.Trim();
    }

    private static IEnumerable<string> GetMarkdownFiles(string folderPath)
    {
        return Directory.EnumerateFiles(folderPath, "*.md", SearchOption.AllDirectories);
    }

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"!\[.*?\]\((.*?)\)")]
    private static partial Regex ImageRegex();
}

public class TokenResponse
{
    public string AccessToken { get; set; }
}
