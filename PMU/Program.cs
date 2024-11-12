using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class WordFetcher
{
    private static readonly HttpClient client = new HttpClient();

    static WordFetcher()
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", "99c9wl3u6HIETExX+4ZvWg==08IEjW7jgHyhyIOv");
    }

    public static async Task<string> GetRandomWordAsync()
    {
        var apiUrl = "https://api.api-ninjas.com/v1/randomword";

        try
        {
            HttpResponseMessage response = await client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();
            var json = JObject.Parse(responseBody);

            
            if (json["word"] != null && json["word"].Type == JTokenType.Array && json["word"].HasValues)
            {
                return json["word"][0].ToString();
            }
            else
            {
                Console.WriteLine("Не удалось найти слово в ответе.");
                return string.Empty; 
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine($"Ошибка при запросе: {e.Message}");
            return string.Empty; 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
            return string.Empty; 
        }
    }
}

public class Translator
{
    private static readonly HttpClient client = new HttpClient();
    static Translator()
    {
        client.DefaultRequestHeaders.Add("x-rapidapi-key", "aef5df664emsh8a9454a7be1f8aap1a4307jsn3c50971166cf");
        client.DefaultRequestHeaders.Add("x-rapidapi-host", "deep-translate1.p.rapidapi.com");
    }
    public static async Task<string> TranslateAsync(string textToTranslate, string sourceLanguage = "en", string targetLanguage = "ru")
    {
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://deep-translate1.p.rapidapi.com/language/translate/v2"),
            Content = new StringContent($"{{\"q\":\"{textToTranslate}\",\"source\":\"{sourceLanguage}\",\"target\":\"{targetLanguage}\"}}", Encoding.UTF8, "application/json")
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        try
        {
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync();
            var jsonObject = JObject.Parse(body);

            string translatedText = jsonObject["data"]["translations"]["translatedText"].ToString();
            return translatedText;
        }
        catch (Exception ex) {Console.WriteLine($"Произошла ошибка: {ex.Message}");
            return string.Empty;  }
        
    }
}


class Program
{
    private static string? _currentWord;
    private static string? _correctTranslation;
    private static Dictionary<long, bool> _userSessions = new Dictionary<long, bool>();

    static async Task Main(string[] args)
    {
        var cts = new CancellationTokenSource();
        var botClient = new TelegramBotClient("7520476882:AAHzwLy_9Fnva-8pkyqaGLkUZEb5rxXbBP4");

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Start listening for @{me.Username}");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new UpdateType[] { UpdateType.Message }
        };

        botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken: cts.Token);

        Console.WriteLine("Press Enter to exit");
        Console.ReadLine();

        cts.Cancel();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message!.Text is not null)
        {
            var chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;


            if (messageText.Equals("/start", StringComparison.OrdinalIgnoreCase))
            {
                await StartNewSession(botClient, chatId, cancellationToken);
                return;
            }


            if (_userSessions.ContainsKey(chatId) && _userSessions[chatId])
            {

                if (messageText.Equals(_correctTranslation, StringComparison.OrdinalIgnoreCase))
                {
                    await botClient.SendTextMessageAsync(chatId, "Правильно! Вы угадали перевод.", cancellationToken: cancellationToken);
                }
                else
                {
                    if (string.IsNullOrEmpty(_correctTranslation)){
                        await botClient.SendTextMessageAsync(chatId, $"Не удалось получить перевод.", cancellationToken: cancellationToken);

                    }
                    else
                    await botClient.SendTextMessageAsync(chatId, $"Неправильно. Правильный перевод: {_correctTranslation}.", cancellationToken: cancellationToken);
                }


                _currentWord = null;
                _correctTranslation = null;
                _userSessions[chatId] = false; 
                await StartNewSession(botClient, chatId, cancellationToken); 
            }
        }
    }

    private static async Task StartNewSession(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        _currentWord = await WordFetcher.GetRandomWordAsync();

        if (!string.IsNullOrEmpty(_currentWord))
        {
            
            _correctTranslation = await Translator.TranslateAsync(_currentWord);
            _userSessions[chatId] = true; 
            await botClient.SendTextMessageAsync(chatId, $"Слово: {_currentWord}\nВведите перевод:", cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "Не удалось получить случайное слово.", cancellationToken: cancellationToken);
        }
    }

    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(errorMessage);
        return Task.CompletedTask;
    }
}






