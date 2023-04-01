using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RBXBD
{
    class Program
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        private static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            Console.WriteLine(@"
 ▄  █ ████▄ ████▄ ▄█▄      ▄▄▄▄▄   
█   █ █   █ █   █ █▀ ▀▄   █     ▀▄ 
██▀▀█ █   █ █   █ █   ▀ ▄  ▀▀▀▀▄   
█   █ ▀████ ▀████ █▄  ▄▀ ▀▄▄▄▄▀    
   █              ▀███▀            
  ▀                                    
            ");
            string configFilePath = "config.json";
            Config config = await LoadConfig(configFilePath);
            while (true)
            {
                byte[] data = await CaptureScreenAsync();
                await SendWebhookAsync(data, config.WebhookUrl!, config.Description!);
                await Task.Delay(config.Delay * 1000);
            }
        }

        private static async Task<Config> LoadConfig(string configFilePath)
        {
            Config config = new Config();
            if (File.Exists(configFilePath))
            {
                string configJson = await File.ReadAllTextAsync(configFilePath);
                config = JsonConvert.DeserializeObject<Config>(configJson)!;
            }
            else
            {
                string webhookUrl = "";
                while (string.IsNullOrEmpty(webhookUrl))
                {
                    Console.Write("( + ) Webhook URL: ");
                    webhookUrl = Console.ReadLine()!;
                }

                int delay = 200;
                while (delay <= 0)
                {
                    Console.Write("( + ) Delay (sec): ");
                    int.TryParse(Console.ReadLine(), out delay);
                }

                string description = "";
                while (string.IsNullOrEmpty(description))
                {
                    Console.Write("Description: ");
                    description = Console.ReadLine()!;
                }

                config.WebhookUrl = webhookUrl;
                config.Delay = delay;
                config.Description = description;

                string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                await File.WriteAllTextAsync(configFilePath, configJson);
            }

            if (string.IsNullOrEmpty(config.WebhookUrl))
            {
                Console.WriteLine("( ! ) Webhook URL is missing in the config.");
                Environment.Exit(0);
            }

            if (config.Delay <= 0)
            {
                Console.WriteLine("( ! ) Delay must be greater than 0.");
                Environment.Exit(0);
            }

            return config!;
        }

        private static async Task<byte[]> CaptureScreenAsync()
        {
            return await Task.Run(() =>
            {
                IntPtr hdcScreen = GetDC(IntPtr.Zero);
                IntPtr hdcCompatible = CreateCompatibleDC(hdcScreen);
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);
                IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, screenWidth, screenHeight);
                IntPtr hOldBitmap = SelectObject(hdcCompatible, hBitmap);
                BitBlt(hdcCompatible, 0, 0, screenWidth, screenHeight, hdcScreen, 0, 0, 13369376);
                SelectObject(hdcCompatible, hOldBitmap);
                DeleteDC(hdcCompatible);
                ReleaseDC(IntPtr.Zero, hdcScreen);
                using (Bitmap bitmap = Image.FromHbitmap(hBitmap))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        byte[] data = ms.ToArray();
                        DeleteObject(hBitmap);
                        return data;
                    }
                }
            });
        }

        private static async Task SendWebhookAsync(byte[] data, string webhookUrl, string description)
        {
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = "᲼᲼᲼᲼᲼᲼᲼᲼᲼᲼᲼᲼᲼📈 VPS STAT CHECK 📊",
                        description = description,
                        image = new
                        {
                            url = "attachment://screenshot.png"
                        },
                        footer = new
                        {
                            text = $"Chụp lại màn hình lúc: {DateTime.Now.ToString()}"
                        },
                        color = 0x00FF00
                    }
                }
            };
            var json = JsonConvert.SerializeObject(payload);
            var imageContent = new ByteArrayContent(data);
            imageContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("image/png");

            using (var content = new MultipartFormDataContent())
            {
                content.Add(imageContent, "file", "screenshot.png");
                content.Add(new StringContent(json), "payload_json");

                using (var response = await client.PostAsync(webhookUrl, content))
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }
    }

    public class Config
    {
        public string? WebhookUrl { get; set; }
        public int Delay { get; set; }
        public string? Description { get; set; }
    }
}