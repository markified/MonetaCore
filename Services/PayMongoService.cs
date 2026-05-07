using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MonetaCore.Models;
using MonetaCore.ViewModels;

namespace MonetaCore.Services;

public class PayMongoOptions
{
    public string BaseUrl { get; set; } = "https://api.paymongo.com";
    public string SecretKey { get; set; } = string.Empty;
}

public sealed class PayMongoPaymentResult
{
    public bool IsSuccess { get; init; }
    public string TransactionId { get; init; } = string.Empty;
    public string RedirectUrl { get; init; } = string.Empty;
    public string ClientKey { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = DomainValues.PaymentStatus.Pending;
    public string Message { get; init; } = string.Empty;
}

public interface IPayMongoService
{
    Task<PayMongoPaymentResult> CreateCheckoutSessionAsync(
        PaymentCreateViewModel model,
        int paymentId,
        Invoice invoice,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    Task<PayMongoPaymentResult> CreateCardPaymentAsync(
        PaymentCreateViewModel model,
        int paymentId,
        Invoice invoice,
        string returnUrl,
        CancellationToken cancellationToken = default);
}

public class PayMongoService : IPayMongoService
{
    private readonly HttpClient _httpClient;
    private readonly PayMongoOptions _options;
    private readonly ISystemConfigurationService _systemConfigurationService;

    public PayMongoService(
        HttpClient httpClient,
        IOptions<PayMongoOptions> options,
        ISystemConfigurationService systemConfigurationService)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _systemConfigurationService = systemConfigurationService;
    }

    public async Task<PayMongoPaymentResult> CreateCheckoutSessionAsync(
        PaymentCreateViewModel model,
        int paymentId,
        Invoice invoice,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        string secretKey = await ResolveSecretKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return BuildSimulationResult("Simulated PayMongo checkout session. Configure PayMongo:SecretKey via user secrets, environment variables, or System Configuration for live API calls.");
        }

        string endpoint = _options.BaseUrl.TrimEnd('/') + "/v1/checkout_sessions";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = BuildAuthHeader(secretKey);

        var body = new
        {
            data = new
            {
                attributes = new
                {
                    cancel_url = cancelUrl,
                    success_url = successUrl,
                    description = $"MonetaCore invoice {invoice.InvoiceNumber}",
                    payment_method_types = new[] { "card", "gcash", "paymaya" },
                    line_items = new[]
                    {
                        new
                        {
                            name = $"Invoice {invoice.InvoiceNumber}",
                            amount = (long)(model.Amount * 100),
                            currency = "PHP",
                            quantity = 1
                        }
                    },
                    metadata = new
                    {
                        paymentId,
                        invoiceId = invoice.Id
                    }
                }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new PayMongoPaymentResult
            {
                IsSuccess = false,
                Message = $"PayMongo error {(int)response.StatusCode}: {payload}"
            };
        }

        using JsonDocument document = JsonDocument.Parse(payload);
        string sessionId = GetString(document.RootElement, "data", "id");
        string checkoutUrl = GetString(document.RootElement, "data", "attributes", "checkout_url");

        return new PayMongoPaymentResult
        {
            IsSuccess = true,
            TransactionId = sessionId,
            RedirectUrl = checkoutUrl,
            PaymentStatus = DomainValues.PaymentStatus.Pending,
            Message = "PayMongo checkout session created."
        };
    }

    public async Task<PayMongoPaymentResult> CreateCardPaymentAsync(
        PaymentCreateViewModel model,
        int paymentId,
        Invoice invoice,
        string returnUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.CardNumber)
            || !model.CardExpMonth.HasValue
            || !model.CardExpYear.HasValue
            || string.IsNullOrWhiteSpace(model.CardCvc))
        {
            return new PayMongoPaymentResult
            {
                IsSuccess = false,
                Message = "Card details are required for PayMongo card payments."
            };
        }

        string secretKey = await ResolveSecretKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return BuildSimulationResult("Simulated PayMongo card payment. Configure PayMongo:SecretKey via user secrets, environment variables, or System Configuration for live API calls.");
        }

        var intentResult = await CreatePaymentIntentAsync(model, paymentId, invoice, new[] { "card" }, cancellationToken);
        if (!intentResult.IsSuccess)
        {
            return intentResult;
        }

        var methodResult = await CreatePaymentMethodAsync(model, secretKey, cancellationToken);
        if (!methodResult.IsSuccess)
        {
            return methodResult;
        }

        return await AttachPaymentIntentAsync(
            intentResult.TransactionId,
            methodResult.TransactionId,
            returnUrl,
            secretKey,
            cancellationToken);
    }

    private async Task<string> ResolveSecretKeyAsync(CancellationToken cancellationToken)
    {
        string secretKey = _options.SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            SystemConfigurationSettings settings = await _systemConfigurationService.GetAsync(cancellationToken);
            secretKey = settings.PayMongoSecretKey;
        }

        return secretKey;
    }

    private static AuthenticationHeaderValue BuildAuthHeader(string secretKey)
    {
        string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{secretKey}:"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }

    private static PayMongoPaymentResult BuildSimulationResult(string message)
    {
        return new PayMongoPaymentResult
        {
            IsSuccess = true,
            TransactionId = $"SIM-{Guid.NewGuid():N}"[..16],
            PaymentStatus = DomainValues.PaymentStatus.Completed,
            Message = message
        };
    }

    private async Task<PayMongoPaymentResult> CreatePaymentIntentAsync(
        PaymentCreateViewModel model,
        int paymentId,
        Invoice invoice,
        string[] allowedMethods,
        CancellationToken cancellationToken)
    {
        string secretKey = await ResolveSecretKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return BuildSimulationResult("Simulated PayMongo payment intent.");
        }

        string endpoint = _options.BaseUrl.TrimEnd('/') + "/v1/payment_intents";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = BuildAuthHeader(secretKey);

        var body = new
        {
            data = new
            {
                attributes = new
                {
                    amount = (long)(model.Amount * 100),
                    payment_method_allowed = allowedMethods,
                    payment_method_options = new { card = new { request_three_d_secure = "automatic" } },
                    currency = "PHP",
                    capture_type = "automatic",
                    description = $"MonetaCore invoice payment {invoice.InvoiceNumber}",
                    metadata = new
                    {
                        paymentId,
                        invoiceId = invoice.Id
                    }
                }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new PayMongoPaymentResult
            {
                IsSuccess = false,
                Message = $"PayMongo error {(int)response.StatusCode}: {payload}"
            };
        }

        using JsonDocument document = JsonDocument.Parse(payload);
        string paymentIntentId = GetString(document.RootElement, "data", "id");
        string clientKey = GetString(document.RootElement, "data", "attributes", "client_key");

        return new PayMongoPaymentResult
        {
            IsSuccess = true,
            TransactionId = paymentIntentId,
            ClientKey = clientKey,
            PaymentStatus = DomainValues.PaymentStatus.Pending,
            Message = "PayMongo payment intent created."
        };
    }

    private async Task<PayMongoPaymentResult> CreatePaymentMethodAsync(
        PaymentCreateViewModel model,
        string secretKey,
        CancellationToken cancellationToken)
    {
        string cardNumber = StripToDigits(model.CardNumber);
        string cardCvc = StripToDigits(model.CardCvc);

        if (string.IsNullOrWhiteSpace(cardNumber) || string.IsNullOrWhiteSpace(cardCvc))
        {
            return new PayMongoPaymentResult
            {
                IsSuccess = false,
                Message = "Card number and CVC must contain digits only."
            };
        }

        string endpoint = _options.BaseUrl.TrimEnd('/') + "/v1/payment_methods";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = BuildAuthHeader(secretKey);

        var body = new
        {
            data = new
            {
                attributes = new
                {
                    type = "card",
                    details = new
                    {
                        card_number = cardNumber,
                        exp_month = model.CardExpMonth,
                        exp_year = model.CardExpYear,
                        cvc = cardCvc
                    },
                    billing = new
                    {
                        name = string.IsNullOrWhiteSpace(model.CardholderName) ? "MonetaCore" : model.CardholderName
                    }
                }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new PayMongoPaymentResult
            {
                IsSuccess = false,
                Message = $"PayMongo error {(int)response.StatusCode}: {payload}"
            };
        }

        using JsonDocument document = JsonDocument.Parse(payload);
        string paymentMethodId = GetString(document.RootElement, "data", "id");

        return new PayMongoPaymentResult
        {
            IsSuccess = true,
            TransactionId = paymentMethodId,
            PaymentStatus = DomainValues.PaymentStatus.Pending,
            Message = "PayMongo payment method created."
        };
    }

    private async Task<PayMongoPaymentResult> AttachPaymentIntentAsync(
        string paymentIntentId,
        string paymentMethodId,
        string returnUrl,
        string secretKey,
        CancellationToken cancellationToken)
    {
        string endpoint = _options.BaseUrl.TrimEnd('/') + $"/v1/payment_intents/{paymentIntentId}/attach";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = BuildAuthHeader(secretKey);

        var attributes = new Dictionary<string, object?>
        {
            ["payment_method"] = paymentMethodId,
            ["return_url"] = returnUrl
        };

        var body = new { data = new { attributes } };

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new PayMongoPaymentResult
            {
                IsSuccess = false,
                Message = $"PayMongo error {(int)response.StatusCode}: {payload}"
            };
        }

        using JsonDocument document = JsonDocument.Parse(payload);
        string status = GetString(document.RootElement, "data", "attributes", "status");
        string redirectUrl = GetString(document.RootElement, "data", "attributes", "next_action", "redirect", "url");
        string paymentStatus = NormalizeStatus(status);

        return new PayMongoPaymentResult
        {
            IsSuccess = true,
            TransactionId = paymentIntentId,
            RedirectUrl = redirectUrl,
            PaymentStatus = paymentStatus,
            Message = string.IsNullOrWhiteSpace(status)
                ? "PayMongo payment intent attached."
                : $"PayMongo payment status: {status}."
        };
    }

    private static string NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return DomainValues.PaymentStatus.Pending;
        }

        string normalized = status.ToLowerInvariant();
        if (normalized.Contains("succeed") || normalized.Contains("paid"))
        {
            return DomainValues.PaymentStatus.Completed;
        }

        if (normalized.Contains("fail"))
        {
            return DomainValues.PaymentStatus.Failed;
        }

        if (normalized.Contains("refund"))
        {
            return DomainValues.PaymentStatus.Refunded;
        }

        return DomainValues.PaymentStatus.Pending;
    }

    private static string StripToDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string digitsSource = value;
        var builder = new StringBuilder(digitsSource.Length);
        foreach (char ch in digitsSource)
        {
            if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string GetString(JsonElement element, params string[] path)
    {
        foreach (string segment in path)
        {
            if (!element.TryGetProperty(segment, out element))
            {
                return string.Empty;
            }
        }

        return element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : string.Empty;
    }
}
