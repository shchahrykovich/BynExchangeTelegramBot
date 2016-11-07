using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
        private static Regex Query = new Regex(@"^(?<amount>(\d)*([.,]\d)*) (?<currency>(\w)*)$", RegexOptions.Compiled, TimeSpan.FromSeconds(3));
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

            if (message == null || message.Type != MessageType.TextMessage)
            {
                return;
            }

            Console.WriteLine($"Request from {message.Chat.Username}.");

            if (message.Text.StartsWith("/rate") 
                || message.Text.Equals("rate", StringComparison.OrdinalIgnoreCase)
                || message.Text.Equals("rates", StringComparison.OrdinalIgnoreCase))
            {
                using (HttpClient client = new HttpClient())
                {
                    var now = DateTime.UtcNow;
                    var date = now.ToString("yyyy-MM-dd");
                    var allRates = await GetRates(client, date);

                    var usd = allRates.FirstOrDefault(r => r.Cur_ID == 145);
                    var eur = allRates.FirstOrDefault(r => r.Cur_ID == 292);
                    var rus = allRates.FirstOrDefault(r => r.Cur_ID == 298);
                    var gbp = allRates.FirstOrDefault(r => r.Cur_ID == 143);
                    var ukr = allRates.FirstOrDefault(r => r.Cur_ID == 290);

                    var text = "Rates for " + now.ToString("MMMM dd") + ":\n";
                    foreach (var rate in new[] { usd, eur, gbp, rus, ukr })
                    {
                        text += $"{rate.Cur_Scale} {rate.Cur_Abbreviation} = {rate.Cur_OfficialRate} BYN\n";
                    }

                    await Bot.SendTextMessageAsync(message.Chat.Id, text, replyMarkup: new ReplyKeyboardHide());
                }
                return;
            }
            else if (Query.IsMatch(message.Text))
            {
                var match = Query.Match(message.Text);
                var amount = float.Parse(match.Groups["amount"].Value);
                var curAbbreviation = match.Groups["currency"].Value;

                using (HttpClient client = new HttpClient())
                {
                    var now = DateTime.UtcNow;
                    var date = now.ToString("yyyy-MM-dd");
                    var allRates = await GetRates(client, date);

                    var usd = allRates.FirstOrDefault(r => r.Cur_ID == 145);
                    var eur = allRates.FirstOrDefault(r => r.Cur_ID == 292);
                    var rus = allRates.FirstOrDefault(r => r.Cur_ID == 298);
                    var gbp = allRates.FirstOrDefault(r => r.Cur_ID == 143);
                    var ukr = allRates.FirstOrDefault(r => r.Cur_ID == 290);

                    if (0 == String.Compare(curAbbreviation, "BYN", true))
                    {
                        var text = "Rates for " + now.ToString("MMMM dd") + ":\n";
                        foreach (var rate in new[] { usd, eur, gbp, rus, ukr })
                        {
                            text += $"{amount} {curAbbreviation.ToUpper()} = {amount * rate.Cur_Scale / rate.Cur_OfficialRate} {rate.Cur_Abbreviation}\n";
                        }

                        await Bot.SendTextMessageAsync(message.Chat.Id, text, replyMarkup: new ReplyKeyboardHide());
                    }
                    else
                    {
                        var cur = allRates.FirstOrDefault(r => 0 == String.Compare(r.Cur_Abbreviation, curAbbreviation, true));
                        if (null != cur)
                        {
                            var text = "Rates for " + now.ToString("MMMM dd") + ":\n";
                            var amountInByn = amount * cur.Cur_OfficialRate / cur.Cur_Scale;
                            text += $"{amount} {curAbbreviation.ToUpper()} = {amountInByn} BYN\n";
                            foreach (var rate in new[] { usd, eur, gbp, rus, ukr }.Where(r => r != cur))
                            {
                                text += $"{amount} {curAbbreviation.ToUpper()} = {amountInByn * rate.Cur_Scale / rate.Cur_OfficialRate} {rate.Cur_Abbreviation}\n";
                            }

                            await Bot.SendTextMessageAsync(message.Chat.Id, text, replyMarkup: new ReplyKeyboardHide());
                        }
                    }
                }
                return;
            }
            var usage = @"Usage:
/rate - returns current exchange rate
7 byn - converts to other currencies
";
            await Bot.SendTextMessageAsync(message.Chat.Id, usage, replyMarkup: new ReplyKeyboardHide());
        }

        private static async Task<ExchangeRate[]> GetRates(HttpClient client, string date)
        {
            // https://www.nbrb.by/APIHelp/ExRates
            var url = $"http://www.nbrb.by/API/ExRates/Rates?onDate={date}&Periodicity=0";
            var resp = await client.GetAsync(url);
            var rawObj = await resp.Content.ReadAsStringAsync();
            ExchangeRate[] rates = JsonConvert.DeserializeObject<ExchangeRate[]>(rawObj);

            return rates;
        }
    }
}