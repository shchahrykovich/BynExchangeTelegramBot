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
                    var allRatesForNow = await GetRates(client, now.ToString("yyyy-MM-dd"));
                    var allRatesWeekAgo = await GetRates(client, now.AddDays(-7).ToString("yyyy-MM-dd"));
                    var allRatesMonthAgo = await GetRates(client, now.AddMonths(-1).ToString("yyyy-MM-dd"));

                    var usd = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 145);
                    var eur = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 292);
                    var rus = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 298);
                    var gbp = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 143);
                    var ukr = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 290);

                    var text = "Rates for " + now.ToString("MMMM dd") + ":\n";
                    foreach (var rate in new[] { usd, eur, gbp, rus, ukr })
                    {
                        var officialRateForToday = Math.Round(rate.Cur_OfficialRate, 2);
                        var officialRateWeekAgo = allRatesWeekAgo.SingleOrDefault(r => r.Cur_ID == rate.Cur_ID);
                        var officialRateMonthAgo = allRatesMonthAgo.SingleOrDefault(r => r.Cur_ID == rate.Cur_ID);
                        var weekGrowth = Math.Round((rate.Cur_OfficialRate / officialRateWeekAgo.Cur_OfficialRate) * 100 - 100, 2);
                        var monthGrowth = Math.Round((rate.Cur_OfficialRate / officialRateMonthAgo.Cur_OfficialRate) * 100 - 100, 2);
                        text += $"{rate.Cur_Scale} {rate.Cur_Abbreviation} = {officialRateForToday} ({weekGrowth} %, {monthGrowth} %) BYN\n";
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
                    var allRatesForNow = await GetRates(client, now.ToString("yyyy-MM-dd"));                    

                    var usd = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 145);
                    var eur = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 292);
                    var rus = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 298);
                    var gbp = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 143);
                    var ukr = allRatesForNow.FirstOrDefault(r => r.Cur_ID == 290);

                    if (0 == String.Compare(curAbbreviation, "BYN", true))
                    {
                        var text = "Rates for " + now.ToString("MMMM dd") + ":\n";
                        foreach (var rate in new[] { usd, eur, gbp, rus, ukr })
                        {
                            var result = amount * rate.Cur_Scale / rate.Cur_OfficialRate;
                            text += $"{amount} {curAbbreviation.ToUpper()} = {Math.Round(result, 2)} {rate.Cur_Abbreviation}\n";
                        }

                        await Bot.SendTextMessageAsync(message.Chat.Id, text, replyMarkup: new ReplyKeyboardHide());
                    }
                    else
                    {
                        var cur = allRatesForNow.FirstOrDefault(r => 0 == String.Compare(r.Cur_Abbreviation, curAbbreviation, true));
                        if (null != cur)
                        {
                            var text = "Rates for " + now.ToString("MMMM dd") + ":\n";
                            var amountInByn = amount * cur.Cur_OfficialRate / cur.Cur_Scale;
                            text += $"{amount} {curAbbreviation.ToUpper()} = {amountInByn} BYN\n";
                            foreach (var rate in new[] { usd, eur, gbp, rus, ukr }.Where(r => r != cur))
                            {
                                var result = amountInByn * rate.Cur_Scale / rate.Cur_OfficialRate;
                                text += $"{amount} {curAbbreviation.ToUpper()} = {Math.Round(result, 2)} {rate.Cur_Abbreviation}\n";
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