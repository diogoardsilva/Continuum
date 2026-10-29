using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace Continuum.Core.Contacts;

public sealed class ICloudAuthException : Exception
{
    public ICloudAuthException(string message) : base(message) { }
}

/// <summary>
/// Reads iCloud contacts over CardDAV, Apple's official contacts protocol.
/// Needs the Apple ID and an app-specific password (account.apple.com >
/// Sign-In and Security > App-Specific Passwords), never the real password.
/// </summary>
public sealed class ICloudContactsClient : IDisposable
{
    public static readonly Uri DefaultEndpoint = new("https://contacts.icloud.com/");

    static readonly XNamespace Dav = "DAV:";
    static readonly XNamespace CardDav = "urn:ietf:params:xml:ns:carddav";
    const int MultigetBatchSize = 100;

    readonly HttpClient _http;
    readonly AuthenticationHeaderValue _auth;
    readonly Uri _endpoint;

    public ICloudContactsClient(string appleId, string appSpecificPassword, HttpMessageHandler? handler = null, Uri? endpoint = null)
    {
        // Redirects are followed by hand so the credentials survive the hop to pXX-contacts.icloud.com.
        _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Continuum/0.1");
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{appleId}:{appSpecificPassword}"));
        _auth = new AuthenticationHeaderValue("Basic", token);
        _endpoint = endpoint ?? DefaultEndpoint;
    }

    public async Task<List<Contact>> GetAllContactsAsync(bool downloadLinkedPhotos = true, CancellationToken ct = default)
    {
        var principal = await FindHrefAsync(_endpoint, Dav + "current-user-principal",
            "<d:current-user-principal/>", ct) ?? throw new InvalidOperationException("O iCloud não indicou a conta (current-user-principal).");

        var home = await FindHrefAsync(principal, CardDav + "addressbook-home-set",
            "<c:addressbook-home-set/>", ct) ?? throw new InvalidOperationException("O iCloud não indicou a pasta dos contactos (addressbook-home-set).");

        var contacts = new List<Contact>();
        foreach (var book in await ListAddressBooksAsync(home, ct))
        {
            var cards = await ListCardsAsync(book, ct);
            for (var i = 0; i < cards.Count; i += MultigetBatchSize)
            {
                contacts.AddRange(await MultigetAsync(book, cards.Skip(i).Take(MultigetBatchSize), ct));
            }
        }

        if (downloadLinkedPhotos)
        {
            foreach (var contact in contacts.Where(c => c.Photo is null && c.PhotoUri is not null))
            {
                contact.Photo = await TryDownloadAsync(new Uri(contact.PhotoUri!), ct);
            }
        }

        return contacts;
    }

    async Task<Uri?> FindHrefAsync(Uri url, XName property, string propXml, CancellationToken ct)
    {
        var (doc, baseUri) = await SendAsync("PROPFIND", url, "0", Propfind(propXml), ct);
        var href = doc.Descendants(property).Descendants(Dav + "href").FirstOrDefault()?.Value.Trim();
        return href is null ? null : new Uri(baseUri, href);
    }

    async Task<List<Uri>> ListAddressBooksAsync(Uri home, CancellationToken ct)
    {
        var (doc, baseUri) = await SendAsync("PROPFIND", home, "1", Propfind("<d:resourcetype/><d:displayname/>"), ct);
        return doc.Descendants(Dav + "response")
            .Where(r => r.Descendants(Dav + "resourcetype").Elements(CardDav + "addressbook").Any())
            .Select(r => new Uri(baseUri, r.Element(Dav + "href")!.Value.Trim()))
            .ToList();
    }

    async Task<List<string>> ListCardsAsync(Uri book, CancellationToken ct)
    {
        var (doc, _) = await SendAsync("PROPFIND", book, "1", Propfind("<d:getetag/><d:getcontenttype/>"), ct);
        return doc.Descendants(Dav + "response")
            .Where(r => r.Descendants(Dav + "getcontenttype").Any(t => t.Value.Contains("vcard", StringComparison.OrdinalIgnoreCase))
                        || r.Element(Dav + "href")!.Value.Trim().EndsWith(".vcf", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Element(Dav + "href")!.Value.Trim())
            .ToList();
    }

    async Task<List<Contact>> MultigetAsync(Uri book, IEnumerable<string> hrefs, CancellationToken ct)
    {
        var body = new StringBuilder()
            .Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>")
            .Append("<c:addressbook-multiget xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:carddav\">")
            .Append("<d:prop><d:getetag/><c:address-data/></d:prop>");
        foreach (var href in hrefs)
        {
            body.Append("<d:href>").Append(System.Security.SecurityElement.Escape(href)).Append("</d:href>");
        }
        body.Append("</c:addressbook-multiget>");

        var (doc, _) = await SendAsync("REPORT", book, "1", body.ToString(), ct);
        return doc.Descendants(CardDav + "address-data")
            .SelectMany(d => VCardParser.ParseAll(d.Value))
            .ToList();
    }

    async Task<byte[]?> TryDownloadAsync(Uri uri, CancellationToken ct)
    {
        try
        {
            using var response = await SendRawAsync(HttpMethod.Get, uri, null, null, ct);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(ct) : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    async Task<(XDocument Doc, Uri BaseUri)> SendAsync(string method, Uri url, string depth, string body, CancellationToken ct)
    {
        using var response = await SendRawAsync(new HttpMethod(method), url, depth, body, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ICloudAuthException("O iCloud recusou o acesso. Confirma a Apple ID e usa uma palavra-passe para apps (não a palavra-passe normal).");
        }
        if ((int)response.StatusCode != 207 && !response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{method} {url} falhou: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        var text = await response.Content.ReadAsStringAsync(ct);
        return (XDocument.Parse(text), response.RequestMessage?.RequestUri ?? url);
    }

    async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, Uri url, string? depth, string? body, CancellationToken ct)
    {
        for (var hop = 0; hop < 5; hop++)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = _auth;
            if (depth is not null) request.Headers.Add("Depth", depth);
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/xml");

            var response = await _http.SendAsync(request, ct);
            var status = (int)response.StatusCode;
            if (status is (301 or 302 or 307 or 308) && response.Headers.Location is { } location)
            {
                url = location.IsAbsoluteUri ? location : new Uri(url, location);
                response.Dispose();
                continue;
            }
            return response;
        }
        throw new HttpRequestException("Demasiados redirecionamentos do iCloud.");
    }

    static string Propfind(string props) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
        + "<d:propfind xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:carddav\"><d:prop>"
        + props
        + "</d:prop></d:propfind>";

    public void Dispose() => _http.Dispose();
}
