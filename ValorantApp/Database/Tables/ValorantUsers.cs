using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp.Database.Tables
{
    public class ValorantUsers
    {
        public string val_username { get; set; }
        public string val_tagname { get; set; }
        public string val_affinity { get; set; }
        public string? val_puuid { get; set; }
    }
}
