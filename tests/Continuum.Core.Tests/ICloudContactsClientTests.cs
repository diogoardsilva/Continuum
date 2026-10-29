using System.Net;
using System.Text;
using Continuum.Core.Contacts;
using Xunit;

namespace Continuum.Core.Tests;

public class ICloudContactsClientTests
{
    sealed class FakeICloud : HttpMessageHandler
    {
        public List<(string Method, string Url, string? Auth)> Requests { get; } = new();
        public bool RejectAuth { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var url = request.RequestUri!.ToString();
            Requests.Add((request.Method.Method, url, request.Headers.Authorization?.Parameter));

            if (RejectAuth) return new HttpResponseMessage(HttpStatusCode.Unauthorized);

            return (request.Method.Method, url) switch
            {
                ("PROPFIND", "https://contacts.icloud.com/") => Redirect("https://contacts.icloud.com/123/principal/"),
                ("PROPFIND", "https://contacts.icloud.com/123/principal/") when body.Contains("current-user-principal") =>
                    Multi("<d:response><d:href>/</d:href><d:propstat><d:prop><d:current-user-principal><d:href>/123/principal/</d:href></d:current-user-principal></d:prop></d:propstat></d:response>"),
                ("PROPFIND", "https://contacts.icloud.com/123/principal/") =>
                    Multi("<d:response><d:href>/123/principal/</d:href><d:propstat><d:prop><c:addressbook-home-set><d:href>https://p44-contacts.icloud.com:443/123/carddavhome/</d:href></c:addressbook-home-set></d:prop></d:propstat></d:response>"),
                ("PROPFIND", "https://p44-contacts.icloud.com/123/carddavhome/") =>
                    Multi("<d:response><d:href>/123/carddavhome/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>"
                        + "<d:response><d:href>/123/carddavhome/card/</d:href><d:propstat><d:prop><d:resourcetype><d:collection/><c:addressbook/></d:resourcetype></d:prop></d:propstat></d:response>"),
                ("PROPFIND", "https://p44-contacts.icloud.com/123/carddavhome/card/") =>
                    Multi("<d:response><d:href>/123/carddavhome/card/</d:href><d:propstat><d:prop/></d:propstat></d:response>"
                        + "<d:response><d:href>/123/carddavhome/card/a.vcf</d:href><d:propstat><d:prop><d:getcontenttype>text/vcard</d:getcontenttype></d:prop></d:propstat></d:response>"
                        + "<d:response><d:href>/123/carddavhome/card/b.vcf</d:href><d:propstat><d:prop><d:getetag>\"1\"</d:getetag></d:prop></d:propstat></d:response>"),
                ("REPORT", "https://p44-contacts.icloud.com/123/carddavhome/card/") when body.Contains("a.vcf") && body.Contains("b.vcf") =>
                    Multi("<d:response><d:href>/123/carddavhome/card/a.vcf</d:href><d:propstat><d:prop><c:address-data>BEGIN:VCARD&#13;\nN:Silva;Diogo&#13;\nTEL:912345678&#13;\nEND:VCARD</c:address-data></d:prop></d:propstat></d:response>"
                        + "<d:response><d:href>/123/carddavhome/card/b.vcf</d:href><d:propstat><d:prop><c:address-data>BEGIN:VCARD\nFN:Ana\nPHOTO;VALUE=uri:https://p44-contacts.icloud.com/123/photo/b\nEND:VCARD</c:address-data></d:prop></d:propstat></d:response>"),
                ("GET", "https://p44-contacts.icloud.com/123/photo/b") =>
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 0xFF, 0xD8 }) },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        }

        static HttpResponseMessage Redirect(string to)
        {
            var r = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
            r.Headers.Location = new Uri(to);
            return r;
        }

        static HttpResponseMessage Multi(string responses) => new((HttpStatusCode)207)
        {
            Content = new StringContent(
                "<?xml version=\"1.0\"?><d:multistatus xmlns:d=\"DAV:\" xmlns:c=\"urn:ietf:params:xml:ns:carddav\">" + responses + "</d:multistatus>",
                Encoding.UTF8, "application/xml"),
        };
    }

    [Fact]
    public async Task DiscoversAddressBookAndDownloadsContacts()
    {
        var fake = new FakeICloud();
        using var client = new ICloudContactsClient("diogo@icloud.com", "abcd-efgh-ijkl-mnop", fake);

        var contacts = await client.GetAllContactsAsync();

        Assert.Equal(new[] { "Diogo Silva", "Ana" }, contacts.Select(c => c.DisplayName));
        Assert.Equal("912345678", contacts[0].Phones.Single().Value);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, contacts[1].Photo);
        // Credentials must survive the redirect to the pXX host.
        Assert.All(fake.Requests, r => Assert.NotNull(r.Auth));
    }

    [Fact]
    public async Task ReportsWrongPasswordClearly()
    {
        using var client = new ICloudContactsClient("diogo@icloud.com", "errada", new FakeICloud { RejectAuth = true });

        await Assert.ThrowsAsync<ICloudAuthException>(() => client.GetAllContactsAsync());
    }
}
