using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace BynExchangeTelegramBot
{
    public class Program
    {
        private static readonly TelegramBotClient Bot = new TelegramBotClient(Environment.GetEnvironmentVariable("BYN-EXCHANGE-BOT-API-KEY"));
        private static CancellationTokenSource TokenSource = new CancellationTokenSource();
        public static void Main(string[] args)
        {            
            Console.CancelKeyPress += (o, e) =>
            {
                e.Cancel = true;
                TokenSource.Cancel();
            };

            Bot.OnMessage += BotOnMessageReceived;
            Bot.OnMessageEdited += BotOnMessageReceived;

            var me = Bot.GetMeAsync().Result;
            Console.Title = me.Username;

            Bot.StartReceiving();

            TokenSource.Token.WaitHandle.WaitOne();

            Bot.StopReceiving();
        }

        private static async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.TextMessage) return;

            if (message.Text.StartsWith("/rate"))
            {
                using (HttpClient client = new HttpClient())
                {
                    var now = DateTime.UtcNow;
                    var date = now.ToString("yyyy-MM-dd");
                    var usd = await SendRate(message, client, 145, date);
                    var eur = await SendRate(message, client, 292, date);
                    var rus = await SendRate(message, client, 298, date);
                    var gbp = await SendRate(message, client, 143, date);
                    var ukr = await SendRate(message, client, 290, date);

                    var text = "Rates for " + now.ToString("MMMM dd") + ":\n";
                    foreach(var rate in new [] { usd, eur, gbp, rus, ukr })
                    {
                        text += $"{rate.Cur_Abbreviation} {rate.Cur_Scale} = BYN {rate.Cur_OfficialRate}\n";
                    }

                    await Bot.SendTextMessageAsync(message.Chat.Id, text, replyMarkup: new ReplyKeyboardHide());
                }
            }
            else
            {
                var usage = @"Usage:
/rate - returns current excahnge rate
";
                await Bot.SendTextMessageAsync(message.Chat.Id, usage, replyMarkup: new ReplyKeyboardHide());
            }
        }

        private static async Task<ExchangeRate> SendRate(Message message, HttpClient client, int currency, string date)
        {
            var url = $"http://www.nbrb.by/API/ExRates/Rates/{currency}?onDate={date}&Periodicity=0";
            var resp = await client.GetAsync(url);
            var rawObj = await resp.Content.ReadAsStringAsync();
            ExchangeRate rate = JsonConvert.DeserializeObject<ExchangeRate>(rawObj);
            return rate;
        }
    }
}