using Continuum.Core.Contacts;
using Xunit;

namespace Continuum.Core.Tests;

public class VCardParserTests
{
    const string AppleCard = "BEGIN:VCARD\r\n"
        + "VERSION:3.0\r\n"
        + "PRODID:-//Apple Inc.//iOS 27.2//EN\r\n"
        + "N:Silva;Diogo;;;\r\n"
        + "FN:Diogo Silva\r\n"
        + "ORG:Continuum\\, Lda;\r\n"
        + "item1.TEL;type=pref:+351 912 345 678\r\n"
        + "item1.X-ABLabel:_$!<Mobile>!$_\r\n"
        + "TEL;type=HOME;type=VOICE:213 000 000\r\n"
        + "item2.EMAIL;type=INTERNET:diogo@example.com\r\n"
        + "item2.X-ABLabel:Ginásio\r\n"
        + "PHOTO;ENCODING=b;TYPE=JPEG:/9j/4AAQ\r\n"
        + " SkZJRg==\r\n"
        + "UID:abc-123\r\n"
        + "END:VCARD\r\n";

    [Fact]
    public void ParsesAppleCard()
    {
        var c = VCardParser.Parse(AppleCard)!;

        Assert.Equal("abc-123", c.Id);
        Assert.Equal("Diogo Silva", c.DisplayName);
        Assert.Equal("Continuum, Lda", c.Organization);
        Assert.Equal(new[] { "telemóvel", "casa" }, c.Phones.Select(p => p.Label));
        Assert.Equal("+351 912 345 678", c.Phones[0].Value);
        Assert.Equal(new ContactField("Ginásio", "diogo@example.com"), c.Emails.Single());
        Assert.Equal(Convert.FromBase64String("/9j/4AAQSkZJRg=="), c.Photo);
    }

    [Fact]
    public void KeepsPhotoLinksForLaterDownload()
    {
        var c = VCardParser.Parse("BEGIN:VCARD\nVERSION:3.0\nFN:Ana\nPHOTO;VALUE=uri:https://p44-contacts.icloud.com/x/photo.jpg\nEND:VCARD\n")!;

        Assert.Null(c.Photo);
        Assert.Equal("https://p44-contacts.icloud.com/x/photo.jpg", c.PhotoUri);
        Assert.Equal("Ana", c.DisplayName);
    }

    [Fact]
    public void ParsesSeveralCardsAndFallsBackToOrganization()
    {
        var all = VCardParser.ParseAll("BEGIN:VCARD\nN:;;;;\nORG:Farmácia\nEND:VCARD\nBEGIN:VCARD\nN:Costa;Rui\nEND:VCARD");

        Assert.Equal(new[] { "Farmácia", "Rui Costa" }, all.Select(c => c.DisplayName));
    }
}
