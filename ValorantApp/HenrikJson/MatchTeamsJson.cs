namespace ValorantApp.HenrikJson
{
    public class MatchTeamsJson
    {
        public MatchTeamJson? Red { get; set; }
        public MatchTeamJson? Blue { get; set; }
    }

    public class MatchTeamJson
    {
        public bool? Has_Won { get; set; }
        public int? Rounds_Won { get; set; }
        public int? Rounds_Lost { get; set; }
        public MatchTeamRosterJson? Roster { get; set; }
    }

    public class MatchTeamRosterJson
    {
        public string[]? Members { get; set; }
        public string? Name { get; set; }
        public string? Tag { get; set; }
        public MatchTeamRosterCustomizationJson? Customization { get; set; }
    }

    public class MatchTeamRosterCustomizationJson
    {
        public string? Icon { get; set; }
        public string? Image { get; set; }
        public string? Primary { get; set; }
        public string? Secondary { get; set; }
        public string? Tertiary { get; set; }
    }
}
