namespace ValorantApp
{
    public class AccountJson
    {
        public string Puuid { get; set; }
        public string Region { get; set; }
        public int Account_Level { get; set; }
        public string Name { get; set; }
        public string Tag { get; set; }
        public AccountCardJson Card { get; set; }
        public string Last_Update { get; set; }
        public int Last_Update_Raw { get; set; }
    }
}