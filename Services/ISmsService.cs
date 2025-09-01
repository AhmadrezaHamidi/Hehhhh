using System.Text;

namespace Demtists.Services;
using ErrorOr;

public interface ISmsService
{
    Task<ErrorOr<string>> SendSmsAsync(string messageText, List<string> mobiles);
}

public class SmsSender : ISmsService
{
    private readonly string _apiKey = "B2aMr0ha6KmmL5euzf7t3FLiRnxsQ2zLhWSz9LEk7ELCtleh";

    public async Task<ErrorOr<string>> SendSmsAsync(string messageText, List<string> mobiles)
    {
        const long lineNumber = 30007487130800;

        if (string.IsNullOrWhiteSpace(messageText))
            return Error.Validation("Message.Empty", "Message text cannot be null or empty.");

        if (mobiles == null || mobiles.Count == 0)
            return Error.Validation("Mobiles.Empty", "Mobiles list cannot be null or empty.");

        var payload = "{" +
                     "\"lineNumber\": " + lineNumber + "," +
                     "\"messageText\": \"" + messageText + "\"," +
                     "\"mobiles\": [" + string.Join(",", mobiles.ConvertAll(m => "\"" + m + "\"")) + "]," +
                     "\"sendDateTime\": null" +
                     "}";

        HttpContent content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        
        try
        {
            var response = await httpClient.PostAsync("https://api.sms.ir/v1/send/bulk", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return Error.Failure("Sms.NotSent", $"Failed to send SMS: {error}");
            }

            return "SMS sent successfully.";
        }
        catch (Exception ex)
        {
            return Error.Failure("Sms.Exception", $"SMS sending failed: {ex.Message}");
        }
    }
}

