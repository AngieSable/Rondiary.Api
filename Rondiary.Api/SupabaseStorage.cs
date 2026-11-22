using System.Net.Http.Json;

namespace Rondiary.Api;

public class SupabaseStorage(HttpClient http, IConfiguration cfg)
{
    private readonly string _origin = cfg["Supabase:Url"]!.TrimEnd('/');
    private readonly string _bucket = cfg["Supabase:Bucket"] ?? "videos";
    private string Api => _origin + "/storage/v1";

    public async Task UploadAsync(string path, Stream data, string contentType)
    {
        using var content = new StreamContent(data);
        content.Headers.ContentType = new(contentType);
        var res = await http.PostAsync($"{Api}/object/{_bucket}/{path}", content);
        if (!res.IsSuccessStatusCode)
            throw new Exception("Supabase: " + await res.Content.ReadAsStringAsync());
    }


    public async Task<string> SignAsync(string path)
    {
        var res = await http.PostAsJsonAsync($"{Api}/object/sign/{_bucket}/{path}", new { expiresIn = 3600 });
        if (!res.IsSuccessStatusCode)
            throw new Exception("Supabase: " + await res.Content.ReadAsStringAsync());
        var json = await res.Content.ReadFromJsonAsync<SignResponse>();
        var url = json!.SignedURL;
        return url.StartsWith("/storage/v1") ? _origin + url : Api + url;
    }

    public async Task DeleteAsync(string path)
    {
        var res = await http.DeleteAsync($"{Api}/object/{_bucket}/{path}");
        if (!res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.NotFound)
            throw new Exception("Supabase: " + await res.Content.ReadAsStringAsync());
    }

    private record SignResponse(string SignedURL);
}