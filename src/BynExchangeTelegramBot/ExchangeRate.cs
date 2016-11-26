namespace BynExchangeTelegramBot
{
    public class ExchangeRate
    {
        public int Cur_ID;
        public string Date { get; set; }
        public string Cur_Abbreviation { get; set; }
        public int Cur_Scale { get; set; }
        public string Cur_Name { get; set; }
        public float Cur_OfficialRate { get; set; }
    }
}