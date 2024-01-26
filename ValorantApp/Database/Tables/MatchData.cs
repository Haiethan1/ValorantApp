using Microsoft.Data.Sqlite;

namespace ValorantApp.Database.Tables
{
    public class MatchData
    {
        public string Match_Id { get; private set; }
        public string Map { get; private set; }
        public uint Game_Length { get; private set; }
        public long Game_Start { get; private set; }
        public DateTime? Game_Start_Patched { get; private set; }
        public int Rounds_Played { get; private set; }
        public string Mode { get; private set; }
        public string Mode_Id { get; private set; }
        public string Season_Id { get; private set; }

    }
}