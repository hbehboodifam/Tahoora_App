using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Security;

namespace OrderManagementApi.Services
{
    public class SmsService
    {
        private readonly HttpClient _httpClient;
        private const string API_KEY = "ru0zKbKUFyfHgEtsj2xD31kNfHnzlFHo2C2qAB7TpU2AwkoX3n";
        private const string SEND_URL = "https://188.121.115.52/ws/v1/sms/simple";

        public SmsService()
        {
            // ایجاد HttpClientHandler با نادیده گرفتن SSL
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _httpClient = new HttpClient(handler);
            
            // اضافه کردن هدر Host برای سرور
            _httpClient.DefaultRequestHeaders.Add("Host", "api.iranpayamak.com");
            _httpClient.DefaultRequestHeaders.Add("Api-Key", API_KEY);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public async Task<(bool Success, string ErrorMessage)> SendSms(string phoneNumber, string message)
        {
            try
            {
                // نرمال‌سازی شماره
                var normalizedPhone = NormalizePhone(phoneNumber);
                if (string.IsNullOrEmpty(normalizedPhone))
                    return (false, "شماره تلفن نامعتبر است.");

                var payload = new
                {
                    text = message,
                    recipients = new[] { normalizedPhone },
                    line_number = "PRO",
                    number_format = "english"
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(SEND_URL, content);

                var responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"📬 پاسخ: {responseString}");

                if (response.IsSuccessStatusCode)
                    return (true, null);
                else
                    return (false, $"خطای سرور: {response.StatusCode} - {responseString}");
            }
            catch (Exception ex)
            {
                return (false, $"خطا در ارتباط با سرویس: {ex.Message}");
            }
        }

        private string NormalizePhone(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return null;
            phone = new string(phone.Where(char.IsDigit).ToArray());

            if (phone.Length == 10 && phone.StartsWith("9"))
                return "0" + phone;
            if (phone.Length == 12 && phone.StartsWith("98"))
                return "0" + phone.Substring(2);
            if (phone.Length == 11 && phone.StartsWith("09"))
                return phone;

            return null;
        }
    }
}